using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class IndependentAuthorityReceiverTests
{
    [Fact]
    public async Task ExactRoleWorkloadIssuanceUsesTheActualIndependentRevocationHeadAndBoundedGrantLifetime()
    {
        using var fixture=new Fixture();var token=TestContext.Current.CancellationToken;
        string bearer=new string('B',64);var intent=new CustodyCallIntent("read-current","tenant","owner","operation",new string('A',64),"{}","attempt","challenge");
        var workload=new IndependentCallerGrant(bearer,"workload","tenant|owner","authorizer",["custody-intent:read-current"],DateTimeOffset.UtcNow.AddSeconds(20),false);
        fixture.State=fixture.State with { Callers=[fixture.State.Callers[0] with { Methods=["custody-role-authorize"] },workload] };
        object Request(CustodyRoleWorkloadGrant? grant=null)=>new { role="authorizer",intent,bearerSha256=bearer,method="prepare",grant,responseSha256=new string('C',64) };
        var exact=await fixture.Client().InvokeAsync<CustodyRoleWorkloadGrant>("custody-role-authorize",Request(),token);exact.ShouldNotBeNull();exact.Identity.ShouldBe("workload");exact.ValidUntil.ShouldBe(workload.ValidUntil);
        var issued=await fixture.Client().InvokeAsync<CustodyRoleIssuance>("custody-role-authorize",Request(exact),token);issued.ShouldNotBeNull();issued.Grant.ShouldBe(exact);fixture.MutationSaves.ShouldBe(0);fixture.OrderingSaves.ShouldBe(3);
        fixture.RejectHeadPublication=_=>
        {
            fixture.State=fixture.State with { Callers=fixture.State.Callers.Select(value=>value.Identity=="workload"?value with { Revoked=true }:value).ToArray() };return true;
        };
        (await fixture.Client().InvokeAsync<CustodyRoleIssuance>("custody-role-authorize",Request(exact),token)).ShouldBeNull();fixture.MutationSaves.ShouldBe(0);fixture.OrderingSaves.ShouldBe(3);
    }

    [Theory]
    [InlineData("qualification-observation-original-preflight")][InlineData("spool-current")]
    public async Task ActualArchivePreflightAndSpoolReadUseOneCallerBudgetAndRetainStalledWorker(string method)
    {
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var finished=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);using var release=new ManualResetEventSlim();
        var storage=Substitute.For<IIndependentArchiveStorage>();storage.AvailableBytes(Arg.Any<string>()).Returns(_=> { entered.TrySetResult();try { release.Wait();return long.MaxValue; }finally { finished.TrySetResult(); } });
        using var fixture=new Fixture(storage);using var budget=new CancellationTokenSource();
        if(method.StartsWith("qualification",StringComparison.Ordinal)) fixture.State=fixture.State with { Callers=fixture.State.Callers.Select(value=>value with { Role="observer",Methods=[method,"qualification-begin-original","qualification-retain-result","qualification-lookup-original"] }).ToArray() };
        try
        {
            var pending=fixture.Client().InvokeAsync<JsonElement>(method,new { role="observer" },budget.Token);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2),TestContext.Current.CancellationToken);budget.Cancel();
            await Should.ThrowAsync<OperationCanceledException>(()=>pending.WaitAsync(TimeSpan.FromSeconds(1),TestContext.Current.CancellationToken));
            finished.Task.IsCompleted.ShouldBeFalse();fixture.Saves.ShouldBe(0);release.Set();await finished.Task.WaitAsync(TimeSpan.FromSeconds(2),TestContext.Current.CancellationToken);
            (await fixture.Client().InvokeAsync<JsonElement>(method,new { role="observer" },TestContext.Current.CancellationToken)).ValueKind.ShouldBe(JsonValueKind.Object);fixture.MutationSaves.ShouldBe(0);fixture.OrderingSaves.ShouldBe(1);
        }
        finally { release.Set(); }
    }
    [Theory]
    [InlineData("none")][InlineData("revoke")][InlineData("case-remove")][InlineData("scope")][InlineData("target")][InlineData("plan")][InlineData("journal")]
    public async Task ActualQualificationLookupResultOrdersCurrentRevocationAndPreservesRetainedProof(string mutation)
    {
        using var signing=ECDsa.Create(ECCurve.NamedCurves.nistP256);using var originals=new Fixture();
        originals.State=originals.State with { Callers=originals.State.Callers.Select(value=>value with { Methods=["qualification-begin-original","qualification-retain-result","qualification-lookup-original"] }).ToArray() };
        var request=QualificationHostTests.Request();string id=request["requestId"]!.GetValue<string>();
        originals.State=originals.State with { QualificationBindings=new[]{"runId","targetCommit","isolatedTargetId","planSha256"}.ToDictionary(name=>name,name=>request[name]!.ToJsonString()),
            QualificationCases=new Dictionary<string,JsonElement>{{request["caseId"]!.GetValue<string>(),JsonSerializer.SerializeToElement(new { enrolledScope=request["enrolledScope"],caseTargetId=request["caseTargetId"] })}} };
        var retained=new InstalledQualificationOriginalStore(originals.Client());var driver=new ReceivingCaseDriver();
        var immutable=(System.Text.Json.Nodes.JsonObject)(await new QualificationOwnerOperationHandler(new("executor","fixture-executor","v1",signing.ExportSubjectPublicKeyInfoPem()),signing,retained,new Dictionary<string,IQualificationOwnerCaseDriver>{{driver.CaseId,driver}},TimeProvider.System)
            .HandleAsync("qualification-executor-execute",JsonSerializer.SerializeToElement(request),originals.State.Callers[0],originals.State,TestContext.Current.CancellationToken))!;
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store=Substitute.For<IQualificationOriginalStore>();store.LookupResultAsync("executor",id+":signed-proof",Arg.Any<CancellationToken>()).Returns(async call=>
        { var exact=await retained.LookupResultAsync("executor",id+":signed-proof",call.Arg<CancellationToken>());entered.TrySetResult();await release.Task;return exact; });
        var handler=new QualificationOwnerOperationHandler(new("executor","fixture-executor","v1",signing.ExportSubjectPublicKeyInfoPem()),signing,store,new Dictionary<string,IQualificationOwnerCaseDriver>(),TimeProvider.System);
        using var receiving=new Fixture(handlers:[handler]);
        receiving.State=receiving.State with { Callers=receiving.State.Callers.Select(value=>value with { Methods=["qualification-executor-lookup"] }).ToArray(),
            QualificationBindings=new[]{"runId","targetCommit","isolatedTargetId","planSha256"}.ToDictionary(name=>name,name=>request[name]!.ToJsonString()),
            QualificationCases=new Dictionary<string,JsonElement>{{request["caseId"]!.GetValue<string>(),JsonSerializer.SerializeToElement(new { enrolledScope=request["enrolledScope"],caseTargetId=request["caseTargetId"] })}} };
        var pending=receiving.Client().InvokeAsync<JsonElement>("qualification-executor-lookup",request,TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2),TestContext.Current.CancellationToken);
        if(mutation=="revoke") receiving.RevokeCaller();
        else if(mutation=="case-remove") receiving.State=receiving.State with { QualificationCases=[] };
        else if(mutation=="scope") receiving.State=receiving.State with { QualificationCases=new Dictionary<string,JsonElement>{{request["caseId"]!.GetValue<string>(),JsonSerializer.SerializeToElement(new { enrolledScope=new { changed=true },caseTargetId=request["caseTargetId"] })}} };
        else if(mutation=="target" || mutation=="plan") receiving.State=receiving.State with { QualificationBindings=receiving.State.QualificationBindings!.ToDictionary(value=>value.Key,value=>value.Key==(mutation=="target"?"targetCommit":"planSha256")?"\"changed\"":value.Value) };
        else if(mutation=="journal") receiving.State=receiving.State with { Revision=receiving.State.Revision+7,Originals=new(receiving.State.Originals) { ["unrelated:journal"]=new(JsonSerializer.SerializeToElement(new { changed=true }),null) } };
        release.TrySetResult();var result=await pending;
        if(mutation is not ("none" or "journal")) result.ValueKind.ShouldBe(JsonValueKind.Undefined);else System.Text.Json.Nodes.JsonNode.DeepEquals(System.Text.Json.Nodes.JsonNode.Parse(result.GetRawText()),immutable).ShouldBeTrue();
        System.Text.Json.Nodes.JsonNode.DeepEquals(await retained.LookupResultAsync("executor",id+":signed-proof",TestContext.Current.CancellationToken),immutable).ShouldBeTrue();
    }
    [Fact]
    public async Task ActualSignedResponseCannotOutliveItsCallerGrantDuringDelayedTransport()
    {
        var clock=new ResponseClock();using var fixture=new Fixture(clock:clock);
        fixture.State=fixture.State with { Callers=fixture.State.Callers.Select(value=>value with { ValidUntil=clock.GetUtcNow().AddSeconds(2) }).ToArray() };
        (await new DaprReplicatedSpoolAuthority(fixture.Client()).GetCurrentAsync(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        fixture.AfterResponse=()=>clock.Now=clock.Now.AddSeconds(2);
        (await new DaprReplicatedSpoolAuthority(fixture.Client()).GetCurrentAsync(TestContext.Current.CancellationToken)).ShouldBeNull();
    }
    private sealed class ResponseClock:TimeProvider
    { internal DateTimeOffset Now=DateTimeOffset.UtcNow;public override DateTimeOffset GetUtcNow()=>Now; }
    [Fact]
    public async Task IndependentTransitionSurvivesRestartAndRejectsChangedRestoreState()
    {
        using var fixture = new Fixture();
        var authority = new DaprReplicatedSpoolAuthority(fixture.Client());
        var target = await authority.GetCurrentAsync(TestContext.Current.CancellationToken);
        target.ShouldBe(fixture.Target);
        byte[] bytes = Encoding.UTF8.GetBytes("isolated-state-one");
        var transition = new AnchoredStateTransition(fixture.Scope, 0, 1, new string('A', 64), Convert.ToHexString(SHA256.HashData(bytes)), bytes);
        (await authority.AdmitTransitionAsync(transition, TestContext.Current.CancellationToken)).ShouldBeTrue();
        // A fresh service object reads the independent surviving head, rather than process admission dictionaries.
        fixture.Restart();
        authority = new DaprReplicatedSpoolAuthority(fixture.Client());
        (await authority.RecoverTransitionAsync(transition, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await authority.ValidateStateAsync(fixture.Target, 1, transition.TargetDigest, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await authority.ValidateStateAsync(fixture.Target, 0, new string('A', 64), TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await authority.VerifyTransitionAsync(transition with { TargetBytes = [1, 2] }, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task UnenrolledRevokedAndWrongMethodCredentialsDenyWithoutDurableChanges()
    {
        using var fixture = new Fixture();
        int before = fixture.Saves;
        fixture.Bearer = "wrong-caller";
        (await new DaprReplicatedSpoolAuthority(fixture.Client()).GetCurrentAsync(TestContext.Current.CancellationToken)).ShouldBeNull();
        fixture.Bearer = "isolated-caller";
        fixture.State = fixture.State with { Callers = fixture.State.Callers.Select(c => c with { Revoked = true }).ToArray() };
        (await new DaprReplicatedSpoolAuthority(fixture.Client()).GetCurrentAsync(TestContext.Current.CancellationToken)).ShouldBeNull();
        fixture.Saves.ShouldBe(before);
    }

    [Fact]
    public async Task OriginalPublicationAfterLostAcknowledgementCannotBecomeFirstAgain()
    {
        using var fixture = new Fixture();
        var originals = new InstalledQualificationOriginalStore(fixture.Client());
        var request = new System.Text.Json.Nodes.JsonObject { ["requestId"] = "original", ["caseId"] = "S1.test" };
        (await originals.BeginAsync("executor", "original", request, TestContext.Current.CancellationToken)).ShouldBeTrue();
        fixture.Restart();
        originals = new(fixture.Client());
        (await originals.BeginAsync("executor", "original", request, TestContext.Current.CancellationToken)).ShouldBeFalse();
        var changed = (System.Text.Json.Nodes.JsonObject)request.DeepClone(); changed["caseId"] = "changed";
        await Should.ThrowAsync<InvalidOperationException>(() => originals.BeginAsync("executor", "original", changed, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RestrictedReplayRetainsOriginalAndRejectsCrossTenantChangedFieldsAndGenericAppend()
    {
        using var fixture = new Fixture();
        fixture.State = fixture.State with { Callers = fixture.State.Callers.Select(c => c with {
            Role = "replay-registrar", Methods = ["replay-register-exact", "replay-lookup-exact"] }).ToArray() };
        TrustedEnvelopeReplayReceipt? stored = null;
        fixture.RecordingSource.RegisterAsync(Arg.Any<TrustedEnvelopeReplayIntent>(), Arg.Any<DateTimeOffset>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(call => { var value = call.ArgAt<TrustedEnvelopeReplayIntent>(0); if (stored is not null) return stored.Intent == value ? stored : null;
                stored = new(value, call.ArgAt<DateTimeOffset>(1), call.ArgAt<DateTimeOffset>(1).Add(call.ArgAt<TimeSpan>(2)), 1, "isolated-native-source-event"); return stored; });
        fixture.RecordingSource.LookupAsync(Arg.Any<TrustedEnvelopeReplayIntent>(), Arg.Any<CancellationToken>())
            .Returns(call => stored?.Intent == call.ArgAt<TrustedEnvelopeReplayIntent>(0) ? stored : null);
        var registrar = new DaprIndependentReplayRegistrar(fixture.Client());
        var intent = new TrustedEnvelopeReplayIntent("issuer", "nonce", "tenant-a", "logical", "v1", new string('A', 64));
        DateTimeOffset first = DateTimeOffset.UtcNow;
        var receipt = await registrar.RegisterAsync(intent, first, TimeSpan.FromSeconds(604800), TestContext.Current.CancellationToken);
        receipt.ShouldNotBeNull();
        fixture.Restart();
        var retry = await registrar.RegisterAsync(intent, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(604800), TestContext.Current.CancellationToken);
        retry.ShouldBe(receipt);
        (await registrar.RegisterAsync(intent with { TargetTenantId = "tenant-b" }, first, TimeSpan.FromSeconds(604800), TestContext.Current.CancellationToken)).ShouldBeNull();
        (await registrar.LookupAsync(intent with { LogicalCommandId = "changed" }, TestContext.Current.CancellationToken)).ShouldBeNull();
        var appended = await fixture.Client().InvokeAsync<TrustedEnvelopeReplayReceipt>("replay-register-exact",
            new { intent, evaluatedAt = first, retentionTicks = TimeSpan.FromSeconds(604800).Ticks, stream = "foreign", @event = "arbitrary" }, TestContext.Current.CancellationToken);
        appended.ShouldBeNull();
        stored.ShouldNotBeNull();
    }

    [Fact]
    public async Task CurrentQualificationScopeRejectsTargetAndPlanSubstitution()
    {
        using var fixture = new Fixture();
        fixture.State = fixture.State with { QualificationBindings = new() {
            ["runId"] = "\"fixture-run\"", ["targetCommit"] = "\"accepted-target\"", ["isolatedTargetId"] = "\"isolated-target\"", ["planSha256"] = "\"accepted-plan\"" },
            QualificationCases = new() { ["S1.test"] = JsonSerializer.SerializeToElement(new { caseId = "S1.test", caseTargetId = "target-a", enrolledScope = "scope-a" }) },
            Callers = fixture.State.Callers.Select(c => c with { Methods = ["qualification-authorize"] }).ToArray() };
        var request = new System.Text.Json.Nodes.JsonObject { ["runId"]="fixture-run", ["targetCommit"] = "accepted-target", ["isolatedTargetId"] = "isolated-target", ["planSha256"] = "accepted-plan", ["caseId"] = "S1.test", ["caseTargetId"] = "target-a", ["enrolledScope"] = "scope-a" };
        var caller = new QualificationCallerContext("worker", "scope");
        var allowed = await fixture.Client().InvokeAsync<Permission>("qualification-authorize", new { role = "executor", request, caller }, TestContext.Current.CancellationToken);
        allowed.ShouldNotBeNull(); allowed.Allowed.ShouldBeTrue();
        request["targetCommit"] = "substituted";
        var denied = await fixture.Client().InvokeAsync<Permission>("qualification-authorize", new { role = "executor", request, caller }, TestContext.Current.CancellationToken);
        denied.ShouldNotBeNull(); denied.Allowed.ShouldBeFalse();
        request["targetCommit"] = "accepted-target"; request["caseTargetId"] = "foreign-target";
        var foreign = await fixture.Client().InvokeAsync<Permission>("qualification-authorize", new { role = "executor", request, caller }, TestContext.Current.CancellationToken);
        foreign.ShouldNotBeNull(); foreign.Allowed.ShouldBeFalse();
    }

    [Fact]
    public void LifecycleCompositionKeepsRawRootClientAndPrivateCallSourcesOutOfGeneralDi()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var dapr = Substitute.For<DaprClient>();
        var services = new ServiceCollection();
        string enrollment = JsonSerializer.Serialize(new AuthorityEnrollmentV1("authority", "incarnation", "store", "profile", "trust", "anchors", new string('A', 64)), CustodyBindingContract.Json);
        var binding = new IndependentDaprAuthorityBinding("independent", "file:/isolated", key.ExportSubjectPublicKeyInfoPem(), "enrollment", "scope", "trust-1");
        services.AddInstalledCustodyLifecycle(dapr, new("tenant-a", "scope", enrollment, key.ExportSubjectPublicKeyInfoPem(), "binding", "authorizer", "file:/authorizer", binding, binding));
        services.Any(service => service.ServiceType == typeof(CustodyLifecycleClient) || service.ServiceType == typeof(ICustodyOwnerCallSource)
            || service.ServiceType == typeof(ICustodyLifecycleCallSource) || service.ServiceType == typeof(ISecretsCredentialBoundDaprInvoker)).ShouldBeFalse();
        services.Count.ShouldBe(2);
    }

    [Theory]
    [InlineData("issuer")][InlineData("audience")][InlineData("operation")][InlineData("principal")][InlineData("actor")][InlineData("binding")][InlineData("roles")]
    [InlineData("target")][InlineData("schema")][InlineData("logical-id")][InlineData("tuple")][InlineData("fingerprint")][InlineData("digest-key-version")]
    [InlineData("nonce")][InlineData("issued-at")][InlineData("expires-at")][InlineData("key-version")]
    public async Task ShippedEnvelopeWorkflowUsesRealHmacAndExactRetainedIssuanceForEverySubstitution(string field)
    {
        using var fixture=new Fixture();
        fixture.State=fixture.State with { Callers=fixture.State.Callers.Select(value=>value with { Methods=["qualification-begin-original","qualification-retain-result","qualification-lookup-original"] }).ToArray() };
        var profiles=Substitute.For<IPlatformSigningProfileProvider>(); var keys=Substitute.For<IPlatformHmacKeyProvider>();
        DateTimeOffset now=DateTimeOffset.UtcNow;
        profiles.GetCurrent().Returns(new PlatformSigningProfile("fixture-r1","fixture-issuer","fixture-audience",now.AddHours(-1),now.AddHours(1),
            TimeSpan.FromSeconds(300),TimeSpan.FromSeconds(30),TimeSpan.FromSeconds(600),TimeSpan.FromSeconds(86400),TimeSpan.FromSeconds(604800)));
        keys.ResolveAsync(Arg.Any<PlatformHmacScope>(),Arg.Any<string?>(),Arg.Any<CancellationToken>()).Returns(call=>ValueTask.FromResult(new PlatformHmacKeyResolution(CustodyStatus.Succeeded,
            new(call.ArgAt<PlatformHmacScope>(0),new("v1",PlatformHmacKeyState.Active,now.AddHours(-1),now.AddHours(1),null),Enumerable.Repeat((byte)42,32).ToArray()))));
        var authenticator=new TrustedEnvelopeAuthenticator(keys,profiles,TimeProvider.System);
        var registrar=Substitute.For<ITrustedEnvelopeReplayRegistrar>();
        var replay=new TrustedEnvelopeReplayVerifier(authenticator,registrar,profiles,TimeProvider.System);
        var identity=new TrustedEnvelopeIdentity(1,"fixture-r1","fixture-issuer",new(TrustedPrincipalKind.User,"tenant-a","actor-a","party-a",1,"roles",null,null,null,null),
            "Contracts.EditProposal","EditProposal","tenant-a","interaction-a","correlation","cause",["tenant-a","User","actor-a","EditProposal","client-key"],"fingerprint","digest-v1","fixture-audience","logical-command");
        string caseId="S2.substitution."+field;
        var driver=new QualificationEnvelopeCaseWorkflow(authenticator,replay,fixture.Client(),new Dictionary<string,TrustedEnvelopeIdentity>{{caseId,identity}});
        var request=new System.Text.Json.Nodes.JsonObject { ["caseId"]=caseId,["requestId"]="isolated-"+field,["enrolledScope"]=new System.Text.Json.Nodes.JsonObject { ["tenantId"]="tenant-a",["logicalId"]="logical",["targetIds"]=new System.Text.Json.Nodes.JsonArray(),["providerCopyIds"]=new System.Text.Json.Nodes.JsonArray() } };
        var first=await driver.ExecuteAsync(request,TestContext.Current.CancellationToken); first.ShouldNotBeNull();
        first["firstStatus"]!.GetValue<string>().ShouldNotBe("Succeeded");
        if(field is not ("issued-at" or "expires-at"))
        {
            first.ContainsKey("originalAuthenticationStatus").ShouldBeFalse();
            // Existing non-timestamp result bodies retain their source20 shape and remain
            // independently verifiable through the actual retained-original reader.
            var store=new InstalledQualificationOriginalStore(fixture.Client());await store.BeginAsync("executor","isolated-"+field+":case-result",request,TestContext.Current.CancellationToken);await store.RetainAsync("executor","isolated-"+field+":case-result",first,TestContext.Current.CancellationToken);
            var audit=Substitute.For<IQualificationNativeCaseAuditReader>();audit.ReadAsync(request,Arg.Any<CancellationToken>()).Returns(new QualificationNativeCaseAudit(1,[],[],[],[],new System.Text.Json.Nodes.JsonObject()));
            (await new QualificationDeniedOriginalReader(store,audit,driver.ReadVerifyAsync).ReadAsync(request,TestContext.Current.CancellationToken)).ShouldNotBeNull();
        }
        if(field is "issued-at" or "expires-at")
        {
            first["originalAuthenticationStatus"]!.GetValue<string>().ShouldBe("Succeeded");first["firstStatus"]!.GetValue<string>().ShouldBe("InvalidTag");first["exactRetryStatus"]!.GetValue<string>().ShouldBe("InvalidTag");
            var store=new InstalledQualificationOriginalStore(fixture.Client());await store.BeginAsync("executor","isolated-"+field+":case-result",request,TestContext.Current.CancellationToken);await store.RetainAsync("executor","isolated-"+field+":case-result",first,TestContext.Current.CancellationToken);
            var audit=Substitute.For<IQualificationNativeCaseAuditReader>();audit.ReadAsync(request,Arg.Any<CancellationToken>()).Returns(new QualificationNativeCaseAudit(1,[],[],[],[],new System.Text.Json.Nodes.JsonObject()));
            var reader=new QualificationDeniedOriginalReader(store,audit,driver.ReadVerifyAsync);(await reader.ReadAsync(request,TestContext.Current.CancellationToken)).ShouldNotBeNull();
            var falsePolicy=(System.Text.Json.Nodes.JsonObject)first.DeepClone();falsePolicy["firstStatus"]="Invalid";falsePolicy["exactRetryStatus"]="Invalid";
            var forgedStore=Substitute.For<IQualificationOriginalStore>();forgedStore.LookupResultAsync("executor","isolated-"+field+":case-result",Arg.Any<CancellationToken>()).Returns(falsePolicy);
            (await new QualificationDeniedOriginalReader(forgedStore,audit,(_,_)=>Task.FromResult<System.Text.Json.Nodes.JsonObject?>(falsePolicy)).ReadAsync(request,TestContext.Current.CancellationToken)).ShouldBeNull();
        }
        fixture.Restart();
        var retried=await new QualificationEnvelopeCaseWorkflow(authenticator,replay,fixture.Client(),new Dictionary<string,TrustedEnvelopeIdentity>{{caseId,identity}}).ExecuteAsync(request,TestContext.Current.CancellationToken);
        System.Text.Json.Nodes.JsonNode.DeepEquals(first,retried).ShouldBeTrue();
        registrar.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task ActualQualificationOwnerReceiverSignsExactOriginalAndUncertainRestartUsesLookupOnly()
    {
        using var fixture=new Fixture();using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var full=QualificationHostTests.Request();var request=(System.Text.Json.Nodes.JsonObject)full.DeepClone();
        fixture.State=fixture.State with {
            Callers=fixture.State.Callers.Select(value=>value with { Methods=["qualification-begin-original","qualification-retain-result","qualification-lookup-original"] }).ToArray(),
            QualificationBindings=new Dictionary<string,string>(new[]{"runId","targetCommit","isolatedTargetId","planSha256"}.Select(name=>new KeyValuePair<string,string>(name,request[name]!.ToJsonString()))),
            QualificationCases=new() { ["S1.reject.missing-config"]=JsonSerializer.SerializeToElement(new { caseId="S1.reject.missing-config",caseTargetId=request["caseTargetId"]!.GetValue<string>(),enrolledScope=request["enrolledScope"] },CustodyBindingContract.Json) } };
        var role=new QualificationOwnerRole("executor","isolated-executor","v1",key.ExportSubjectPublicKeyInfoPem());
        var driver=new ReceivingCaseDriver();var drivers=new Dictionary<string,IQualificationOwnerCaseDriver>{{driver.CaseId,driver}};
        var store=new InstalledQualificationOriginalStore(fixture.Client());
        var caller=fixture.State.Callers[0];
        QualificationOwnerOperationHandler Receiver()=>new(role,key,store,drivers,TimeProvider.System);
        driver.Lose=true;
        await Should.ThrowAsync<TimeoutException>(()=>Receiver().HandleAsync("qualification-executor-execute",JsonSerializer.SerializeToElement(request),caller,fixture.State,TestContext.Current.CancellationToken));
        fixture.Restart();driver.Lose=false;
        var result=(System.Text.Json.Nodes.JsonObject?)await Receiver().HandleAsync("qualification-executor-execute",JsonSerializer.SerializeToElement(request),caller,fixture.State,TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();driver.Executions.ShouldBe(1);driver.Lookups.ShouldBe(1);
        byte[] anchor=Encoding.ASCII.GetBytes(key.ExportSubjectPublicKeyInfoPem());
        var verifier=new QualificationProofVerifier(new Dictionary<string,QualificationProofBinding>{{"executor",new("isolated-executor","v1",anchor,Convert.ToHexStringLower(SHA256.HashData(anchor)))}});
        verifier.Verify("executor",full,result,TestContext.Current.CancellationToken).ShouldBeTrue();
        var foreign=(System.Text.Json.Nodes.JsonObject)request.DeepClone();foreign["caseTargetId"]=new string('f',64);
        await Should.ThrowAsync<InvalidOperationException>(()=>Receiver().HandleAsync("qualification-executor-execute",JsonSerializer.SerializeToElement(foreign),caller,fixture.State,TestContext.Current.CancellationToken));
        await Should.ThrowAsync<InvalidOperationException>(()=>Receiver().HandleAsync("qualification-executor-execute",JsonSerializer.SerializeToElement(request),caller with { Revoked=true },fixture.State,TestContext.Current.CancellationToken));
        driver.Executions.ShouldBe(1);
    }
    [Fact]
    public async Task SettledSpoolCannotAdmitReadinessOrObservationWithoutActualWritableArchiveAndReserve()
    {
        var storage=Substitute.For<IIndependentArchiveStorage>();long available=long.MaxValue;
        storage.AvailableBytes(Arg.Any<string>()).Returns(_=>available);
        using var fixture=new Fixture(storage);
        fixture.State=fixture.State with { Callers=fixture.State.Callers.Select(value=>value with { Methods=value.Methods.Concat(["spool-authorize"]).ToArray() }).ToArray() };
        var authority=new DaprReplicatedSpoolAuthority(fixture.Client());
        (await authority.GetCurrentAsync(TestContext.Current.CancellationToken)).ShouldBe(fixture.Target);
        (await authority.AuthorizeAsync(fixture.Target,"Readiness",null,TestContext.Current.CancellationToken)).ShouldBeTrue();
        storage.ReceivedCalls().Where(value=>value.GetMethodInfo().Name==nameof(IIndependentArchiveStorage.AvailableBytes)).ShouldAllBe(value=>value.GetArguments()[0]!.Equals(fixture.ArchiveDirectory));
        available=0;(await authority.GetCurrentAsync(TestContext.Current.CancellationToken)).ShouldBeNull();
        available=1024*1024; // Completion space remains, but no new maximum-sized denial original can be admitted.
        (await authority.GetCurrentAsync(TestContext.Current.CancellationToken)).ShouldBe(fixture.Target);
        (await authority.AuthorizeAsync(fixture.Target,"Readiness",null,TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await authority.AuthorizeAsync(fixture.Target,"Drain",null,TestContext.Current.CancellationToken)).ShouldBeTrue();
        byte[] bytes="fixture-completion-state"u8.ToArray();var transition=new AnchoredStateTransition(fixture.Scope,0,1,new string('A',64),Convert.ToHexString(SHA256.HashData(bytes)),bytes);
        available=long.MaxValue;(await authority.AdmitTransitionAsync(transition,TestContext.Current.CancellationToken)).ShouldBeTrue();available=1024*1024;
        (await authority.RecoverTransitionAsync(transition,TestContext.Current.CancellationToken)).ShouldBeTrue();
        string moved=fixture.ArchiveDirectory+"-offline";Directory.Move(fixture.ArchiveDirectory,moved);
        try { (await authority.GetCurrentAsync(TestContext.Current.CancellationToken)).ShouldBeNull();(await authority.ValidateStateAsync(fixture.Target,1,transition.TargetDigest,TestContext.Current.CancellationToken)).ShouldBeFalse(); }
        finally { Directory.Move(moved,fixture.ArchiveDirectory); }
    }
    [Fact]
    public async Task ActualReadOnlyArchiveDirectoryBlocksSettledReadinessBeforeNativeEffects()
    {
        if(!OperatingSystem.IsLinux()) return;
        using var fixture=new Fixture();var authority=new DaprReplicatedSpoolAuthority(fixture.Client());
        var mode=File.GetUnixFileMode(fixture.ArchiveDirectory);File.SetUnixFileMode(fixture.ArchiveDirectory,UnixFileMode.UserRead|UnixFileMode.UserExecute);
        try { (await authority.GetCurrentAsync(TestContext.Current.CancellationToken)).ShouldBeNull();fixture.Saves.ShouldBe(0); }
        finally { File.SetUnixFileMode(fixture.ArchiveDirectory,mode); }
    }

    [Fact]
    public async Task ReceivingEnrollmentAdmitsItsOwnOriginalAndRetainsExactSignedProofAcrossLostReplyAndLaterClock()
    {
        var clock=new ProofClock();using var fixture=new Fixture(clock:clock);using var signer=ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request=QualificationHostTests.Request();foreach(string field in new[]{"caseId","caseTargetId","enrolledScope","scenario","requestId"}) request.Remove(field);
        request["operation"]="enroll";request["requestId"]=QualificationProtocolSchema.Digest(request);
        var cases=new Dictionary<string,JsonElement>();var drivers=new Dictionary<string,IQualificationOwnerCaseDriver>();
        foreach(string id in QualificationProtocolSchema.CaseIds)
        {
            var scope=QualificationHostTests.Request()["enrolledScope"]!.DeepClone();string target=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(id)));
            scope["logicalId"]=target;scope["targetIds"]=new System.Text.Json.Nodes.JsonArray(target);
            cases[id]=JsonSerializer.SerializeToElement(new { caseTargetId=target,enrolledScope=scope },CustodyBindingContract.Json);drivers[id]=new EnrollmentDriver(id);
        }
        var bindings=new Dictionary<string,string>(new[]{"runId","targetCommit","isolatedTargetId","planSha256"}.Select(name=>new KeyValuePair<string,string>(name,request[name]!.ToJsonString())));
        foreach(var pair in new[]{("executorIdentity","executor"),("executorVersion","v1"),("observerIdentity","observer"),("observerVersion","v1"),("qualifiedReferencesSha256",new string('b',64)),("currentProfileSha256",new string('c',64))}) bindings[pair.Item1]=JsonSerializer.Serialize(pair.Item2);
        fixture.State=fixture.State with { QualificationBindings=bindings,QualificationCases=cases,Callers=fixture.State.Callers.Select(value=>value with { Role="enrollment",Methods=["qualification-begin-original","qualification-retain-result","qualification-lookup-original"] }).ToArray() };
        var store=new InstalledQualificationOriginalStore(fixture.Client());var role=new QualificationOwnerRole("enrollment","enrollment","v1",signer.ExportSubjectPublicKeyInfoPem());
        QualificationOwnerOperationHandler Receiver()=>new(role,signer,store,drivers,clock);
        var first=(System.Text.Json.Nodes.JsonObject)(await Receiver().HandleAsync("qualification-enrollment-enroll",JsonSerializer.SerializeToElement(request),fixture.State.Callers[0],fixture.State,TestContext.Current.CancellationToken))!;
        clock.Now=clock.Now.AddMinutes(10);fixture.Restart();
        var recovered=(System.Text.Json.Nodes.JsonObject)(await Receiver().HandleAsync("qualification-enrollment-enroll",JsonSerializer.SerializeToElement(request),fixture.State.Callers[0],fixture.State,TestContext.Current.CancellationToken))!;
        System.Text.Json.Nodes.JsonNode.DeepEquals(first,recovered).ShouldBeFalse();
        byte[] pem=Encoding.ASCII.GetBytes(role.PublicKeyPem);var verifier=new QualificationProofVerifier(new Dictionary<string,QualificationProofBinding>{{"enrollment",new(role.Identity,role.Version,pem,Convert.ToHexStringLower(SHA256.HashData(pem)))}},clock);
        verifier.Verify("enrollment",request,recovered,TestContext.Current.CancellationToken).ShouldBeTrue();
        verifier.VerifyOriginal("enrollment",request,first,first["payload"]!["observedAt"]!.GetValue<long>(),TestContext.Current.CancellationToken).ShouldBeTrue();
        System.Text.Json.Nodes.JsonNode.DeepEquals(first,await store.LookupResultAsync("enrollment",request["requestId"]!.GetValue<string>()+":signed-proof",TestContext.Current.CancellationToken)).ShouldBeTrue();
        var repeated=await Receiver().HandleAsync("qualification-enrollment-enroll",JsonSerializer.SerializeToElement(request),fixture.State.Callers[0],fixture.State,TestContext.Current.CancellationToken);
        System.Text.Json.Nodes.JsonNode.DeepEquals(recovered,JsonSerializer.SerializeToNode(repeated,CustodyBindingContract.Json)).ShouldBeTrue();
        fixture.State=fixture.State with { Callers=fixture.State.Callers.Select(value=>value with { Methods=value.Methods.Concat(["qualification-authorize"]).ToArray() }).ToArray() };
        using var owner=new Fixture(clock:clock,handlers:[Receiver()]);owner.State=owner.State with { QualificationBindings=bindings,QualificationCases=cases,Callers=owner.State.Callers.Select(value=>value with { Role="enrollment",Methods=["qualification-enrollment-enroll"] }).ToArray() };
        var provider=new InstalledQualificationEnrollment(fixture.Client(),owner.Client());
        Task<QualificationHostResult> Enroll()=>QualificationHost.HandleAsync("enrollment","enroll",QualificationHostTests.Configuration(),request,provider,store,verifier,TestContext.Current.CancellationToken,new("worker","scope"));
        var publicFirst=await Enroll();publicFirst.StatusCode.ShouldBe(200,publicFirst.Body.ToJsonString());clock.Now=clock.Now.AddSeconds(31);owner.Restart();fixture.Restart();
        owner.DropNextResponseMethod="qualification-enrollment-enroll";
        (await Enroll()).StatusCode.ShouldBe(503);owner.DropNextResponseMethod.ShouldBeNull();var afterLost=await Enroll();afterLost.StatusCode.ShouldBe(200,afterLost.Body.ToJsonString());verifier.Verify("enrollment",request,afterLost.Body,TestContext.Current.CancellationToken).ShouldBeTrue();
        var concurrent=await Task.WhenAll(Enumerable.Range(0,4).Select(_=>provider.InvokeAsync("enroll",request,TestContext.Current.CancellationToken)));
        concurrent.Where(value=>value!=null).ShouldNotBeEmpty();concurrent.Where(value=>value!=null).ShouldAllBe(value=>System.Text.Json.Nodes.JsonNode.DeepEquals(value,afterLost.Body));
        foreach(var refused in concurrent.Where(value=>value==null)) System.Text.Json.Nodes.JsonNode.DeepEquals(await provider.InvokeAsync("enroll",request,TestContext.Current.CancellationToken),afterLost.Body).ShouldBeTrue();
        var changedBindings=new Dictionary<string,string>(bindings) { ["currentProfileSha256"]=JsonSerializer.Serialize(new string('f',64)) };
        owner.State=owner.State with { QualificationBindings=changedBindings };(await Enroll()).StatusCode.ShouldBe(503);
        owner.State=owner.State with { QualificationBindings=bindings };owner.RevokeCaller();(await Enroll()).StatusCode.ShouldBe(503);
        System.Text.Json.Nodes.JsonNode.DeepEquals(first,await store.LookupResultAsync("enrollment",request["requestId"]!.GetValue<string>()+":signed-proof",TestContext.Current.CancellationToken)).ShouldBeTrue();
        await Should.ThrowAsync<InvalidOperationException>(()=>Receiver().HandleAsync("qualification-enrollment-enroll",JsonSerializer.SerializeToElement(request),fixture.State.Callers[0] with { Revoked=true },fixture.State,TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task FullSpoolSnapshotAndCompletionRoundTripThroughReceivingSerializationAndLowerSidecarCapacityBlocksAdmission()
    {
        using var fixture=new Fixture();var authority=new DaprReplicatedSpoolAuthority(fixture.Client());
        fixture.State=fixture.State with { Callers=fixture.State.Callers.Select(value=>value with { Methods=value.Methods.Concat(["spool-authorize"]).ToArray() }).ToArray() };
        var original=fixture.State.TransportCapacity!;fixture.State=fixture.State with { TransportCapacity=original with { MaximumDaprInvokeBytes=1024*1024 } };
        (await authority.AuthorizeAsync(fixture.Target,"Readiness",null,TestContext.Current.CancellationToken)).ShouldBeFalse();
        byte[] body=new byte[RecoverableAnchoredState.MaximumPendingBytes];body.AsSpan().Fill(42);
        var transition=new AnchoredStateTransition(fixture.Scope,0,1,new string('A',64),Convert.ToHexString(SHA256.HashData(body)),body);
        (await authority.AdmitTransitionAsync(transition,TestContext.Current.CancellationToken)).ShouldBeFalse();fixture.Saves.ShouldBe(0);
        fixture.State=fixture.State with { TransportCapacity=original };
        (await authority.AdmitTransitionAsync(transition,TestContext.Current.CancellationToken)).ShouldBeTrue();fixture.Restart();
        (await authority.RecoverTransitionAsync(transition,TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await authority.VerifyTransitionAsync(transition,TestContext.Current.CancellationToken)).ShouldBeTrue();
        var complete=transition with { ExpectedRevision=1,TargetRevision=2,PredecessorDigest=transition.TargetDigest,TargetBytes=body.ToArray() };
        complete.TargetBytes[^1]=43;complete=complete with { TargetDigest=Convert.ToHexString(SHA256.HashData(complete.TargetBytes)) };
        (await authority.AdmitTransitionAsync(complete,TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await authority.RecordTransitionAsync(complete,TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await authority.ValidateStateAsync(fixture.Target,2,complete.TargetDigest,TestContext.Current.CancellationToken)).ShouldBeTrue();
    }
    private sealed class ProofClock:TimeProvider { internal DateTimeOffset Now=DateTimeOffset.UtcNow;public override DateTimeOffset GetUtcNow()=>Now; }
    private sealed class EnrollmentDriver(string id):IQualificationOwnerCaseDriver
    {
        public string CaseId=>id;
        public Task<IReadOnlyList<string>> PreflightAsync(System.Text.Json.Nodes.JsonObject request,CancellationToken token)=>Task.FromResult<IReadOnlyList<string>>([]);
        public Task<System.Text.Json.Nodes.JsonObject?> ExecuteAsync(System.Text.Json.Nodes.JsonObject request,CancellationToken token)=>throw new InvalidOperationException("Enrollment cannot execute cases.");
        public Task<System.Text.Json.Nodes.JsonObject?> LookupAsync(System.Text.Json.Nodes.JsonObject request,CancellationToken token)=>throw new InvalidOperationException("Enrollment cannot read cases.");
        public Task<System.Text.Json.Nodes.JsonObject?> ObserveAsync(System.Text.Json.Nodes.JsonObject request,CancellationToken token)=>throw new InvalidOperationException("Enrollment cannot observe cases.");
    }

    private sealed class ReceivingCaseDriver:IQualificationOwnerCaseDriver
    {
        public string CaseId=>"S1.reject.missing-config";
        internal int Executions,Lookups;internal bool Lose;
        public Task<IReadOnlyList<string>> PreflightAsync(System.Text.Json.Nodes.JsonObject original,CancellationToken token)=>Task.FromResult<IReadOnlyList<string>>([]);
        public Task<System.Text.Json.Nodes.JsonObject?> ExecuteAsync(System.Text.Json.Nodes.JsonObject original,CancellationToken token)
        { Executions++;if(Lose) throw new TimeoutException("isolated lost owner acknowledgement");return LookupResult(); }
        public Task<System.Text.Json.Nodes.JsonObject?> LookupAsync(System.Text.Json.Nodes.JsonObject original,CancellationToken token) { Lookups++;return LookupResult(); }
        public Task<System.Text.Json.Nodes.JsonObject?> ObserveAsync(System.Text.Json.Nodes.JsonObject original,CancellationToken token)=>Task.FromResult<System.Text.Json.Nodes.JsonObject?>(null);
        private static Task<System.Text.Json.Nodes.JsonObject?> LookupResult()=>Task.FromResult<System.Text.Json.Nodes.JsonObject?>(new() { ["outcome"]="Denied",["resultSha256"]=new string('b',64) });
    }

    private sealed record Permission(bool Allowed);

    [Fact]
    public async Task ActualReplayReadinessReceiverChecksCurrentGrantAndTrustWithoutRegisteringOrReadingAnInventedNonce()
    {
        using var fixture=new Fixture();var token=TestContext.Current.CancellationToken;
        var granted=fixture.State.Callers.Select(value=>value with { Role="replay-registrar",Methods=["replay-read-current-ready","replay-register-exact","replay-lookup-exact","replay-lookup-first-seen"] }).ToArray();
        fixture.State=fixture.State with { Callers=granted };var client=fixture.Client();
        (await client.InvokeAsync<Permission>("replay-read-current-ready",new { },token))!.Allowed.ShouldBeTrue();
        fixture.State=fixture.State with { Callers=granted.Select(value=>value with { Revoked=true }).ToArray() };
        (await client.InvokeAsync<Permission>("replay-read-current-ready",new { },token)).ShouldBeNull();
        fixture.State=fixture.State with { Callers=granted,ValidUntil=DateTimeOffset.UtcNow.AddSeconds(-1) };
        (await client.InvokeAsync<Permission>("replay-read-current-ready",new { },token)).ShouldBeNull();
        fixture.State=fixture.State with { ValidUntil=DateTimeOffset.UtcNow.AddHours(1),Callers=granted.Select(value=>value with { Methods=["replay-read-current-ready"] }).ToArray() };
        (await client.InvokeAsync<Permission>("replay-read-current-ready",new { },token))!.Allowed.ShouldBeFalse();
        fixture.State=fixture.State with { Callers=granted };fixture.Restart();
        (await client.InvokeAsync<Permission>("replay-read-current-ready",new { },token))!.Allowed.ShouldBeTrue();
        fixture.RecordingSource.ReceivedCalls().ShouldBeEmpty();fixture.State.Originals.ShouldBeEmpty();fixture.State.PrivateOriginalsCount.ShouldBe(0);
    }

    internal sealed class Fixture : IDisposable
    {
        private readonly string _channel = Path.Combine(Path.GetTempPath(), "story54-authority-" + Guid.NewGuid().ToString("N"));
        private readonly ECDsa _signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        private readonly DaprClient _dapr = Substitute.For<DaprClient>();
        private readonly IndependentAuthorityReceiverInstallation _installation;
        private IndependentAuthorityReceiver _receiver;
        private readonly IIndependentArchiveStorage? _archiveStorage;
        private readonly IIndependentContentArchive? _privateArchive;
        private readonly TimeProvider _clock;
        private readonly Func<IndependentArchiveCapacityInstallation,string,string,IndependentArchiveCapacity>? _capacityFactory;
        private readonly IReadOnlyList<IIndependentOwnerOperationHandler>? _handlers;
        internal string ArchiveDirectory=>_channel+"-archive";
        internal string PrivateArchiveDirectory=>Path.Combine(ArchiveDirectory,"private-originals",Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { _installation.EnrollmentId,_installation.Scope,_installation.TrustRevision },CustodyBindingContract.Json))));
        internal IndependentAuthorityReceiver Receiver=>_receiver;
        private int _revision;
        internal IndependentAuthorityState State;
        internal int Saves,OrderingSaves;
        internal int MutationSaves=>Saves-OrderingSaves;
        private void CountSave(IndependentAuthorityState next)
        { if(SameAuthorityHead(State with { Revision=next.Revision },next)) OrderingSaves++;Saves++; }
        private static bool SameAuthorityHead(IndependentAuthorityState a,IndependentAuthorityState b)=>JsonSerializer.Serialize(a,CustodyBindingContract.Json)==JsonSerializer.Serialize(b,CustodyBindingContract.Json);
        internal string? DropNextResponseMethod,DropNextRequestMethod,DropRequestStage;
        internal Func<IndependentAuthorityState,bool>? RejectHeadPublication;
        internal Func<IndependentAuthorityState,string,bool?>? HeadSaveOverride;
        internal bool CompleteDelayedHead(IndependentAuthorityState next,string etag)
        { lock(_headLock) { if(etag!=_revision.ToString()) return false;CountSave(next);State=next;_revision++;return true; } }
        internal Action? AfterResponse;
        internal Func<Task>? BeforeRead;
        private readonly object _headLock=new();
        internal IPrivateRecordingEventStore RecordingSource { get; } = Substitute.For<IPrivateRecordingEventStore>();
        internal string Bearer = "isolated-caller";
        internal ReplicatedSecuritySpoolTarget Target { get; } = new("isolated-spool", "isolated-epoch", "current-1", DateTimeOffset.UtcNow.AddHours(1));
        internal string Scope => Target.ComponentName + "|system/security-observations/" + Target.InstallationEpoch;
        internal Fixture(IIndependentArchiveStorage? archiveStorage=null,IIndependentContentArchive? privateArchive=null,TimeProvider? clock=null,IReadOnlyList<IIndependentOwnerOperationHandler>? handlers=null,IndependentArchiveCapacityInstallation? capacity=null,Func<IndependentArchiveCapacityInstallation,string,string,IndependentArchiveCapacity>? capacityFactory=null,string stateKey="head")
        {
            _capacityFactory=capacityFactory;_handlers=handlers;
            _archiveStorage=archiveStorage??(capacity is null?Substitute.For<IIndependentArchiveStorage>():new IndependentArchiveStorage());if(archiveStorage is null && capacity is null) { _archiveStorage.AvailableBytes(Arg.Any<string>()).Returns(long.MaxValue);_archiveStorage.ProbeWritableAsync(Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(call=>new IndependentArchiveStorage().ProbeWritableAsync(call.Arg<string>(),call.Arg<CancellationToken>())); }_privateArchive=privateArchive;_clock=clock??TimeProvider.System;
            File.WriteAllText(_channel, "isolated-channel");
            string archive = _channel + "-archive"; Directory.CreateDirectory(archive);
            File.WriteAllText(Path.Combine(archive, "enrollment.json"), "isolated-archive-enrollment");
            _installation = new("isolated-enrollment", "isolated-owner", "current-1", "independent-test-store", stateKey, "unused", _signer.ExportSubjectPublicKeyInfoPem(), "file:" + _channel, OriginalArchiveDirectory: archive, ArchiveEnrollmentSha256: Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(archive, "enrollment.json")))), CompletionReserveBytes: 1024 * 1024,ArchiveCapacity:capacity);
            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Bearer)));
            State = new("isolated-enrollment", "isolated-owner", "current-1", 0, DateTimeOffset.UtcNow.AddHours(1),
                [new(hash, "worker", "scope", "executor", ["spool-current", "spool-admit-transition", "spool-record-transition", "spool-recover-transition", "spool-verify-transition", "spool-validate", "qualification-begin-original"], DateTimeOffset.UtcNow.AddHours(1), false)],
                Target, 0, new string('A', 64), [], [], [], [], [], [],TransportCapacity:new("fixture-current-capacity","fixture-sidecar",new string('a',64),128L*1024*1024,128L*1024*1024,128L*1024*1024,128L*1024*1024,DateTimeOffset.UtcNow.AddHours(1)));
            _dapr.GetStateAndETagAsync<IndependentAuthorityState>("independent-test-store", stateKey, Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
                .Returns(async _ => { if(BeforeRead is { } pending) await pending();lock(_headLock) return (State, _revision.ToString()); });
            _dapr.TrySaveStateAsync("independent-test-store", stateKey, Arg.Any<IndependentAuthorityState>(), Arg.Any<string>(), Arg.Any<StateOptions>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
                .Returns(call => { lock(_headLock) { if (call.ArgAt<string>(3) != _revision.ToString()) return false;var next=call.ArgAt<IndependentAuthorityState>(2);
                    if(HeadSaveOverride?.Invoke(next,call.ArgAt<string>(3)) is { } overridden) return overridden;
                    if(RejectHeadPublication?.Invoke(next)==true) { State=State with { Revision=checked(State.Revision+1) };_revision++;return false; }
                    CountSave(next);State=next;_revision++;return true; } });
            _receiver = new(_dapr, _installation, _signer, _clock, RecordingSource,ownerHandlers:_handlers,archiveStorage:_archiveStorage,privateOriginalArchive:_privateArchive) { CapacityFactory=_capacityFactory };
        }
        internal void Restart() => _receiver = new(_dapr, _installation, _signer, _clock, RecordingSource,ownerHandlers:_handlers,archiveStorage:_archiveStorage,privateOriginalArchive:_privateArchive) { CapacityFactory=_capacityFactory };
        internal void RevokeCaller() { lock(_headLock) { State=State with { Revision=checked(State.Revision+1),Callers=State.Callers.Select(value=>value with { Revoked=true }).ToArray() };_revision++; } }
        internal ISecretsCredentialBoundDaprInvoker PrivateTransport()=>new Transport(this);
        internal IndependentDaprAuthorityBinding PublicBinding=>new("isolated-owner", "file:/isolated-bearer", _signer.ExportSubjectPublicKeyInfoPem(), "isolated-enrollment", "isolated-owner", "current-1");
        internal IndependentDaprAuthority Client() => new(new Transport(this), new("isolated-owner", "file:/isolated-bearer", _signer.ExportSubjectPublicKeyInfoPem(), "isolated-enrollment", "isolated-owner", "current-1"), _clock);
        internal IndependentDaprAuthority ReplicaClient() => new(new Transport(this,new IndependentAuthorityReceiver(_dapr,_installation,_signer,_clock,RecordingSource,ownerHandlers:_handlers,archiveStorage:_archiveStorage,privateOriginalArchive:_privateArchive) { CapacityFactory=_capacityFactory }),PublicBinding,_clock);
        public void Dispose() { _signer.Dispose(); _dapr.Dispose(); File.Delete(_channel); Directory.Delete(_channel + "-archive", true); }
        private sealed class Transport(Fixture fixture,IndependentAuthorityReceiver? replica=null) : ISecretsCredentialBoundDaprInvoker
        {
            public Task<string?> InvokeAsync(string appId, string method, string jsonBody, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
            public async Task<string?> InvokeCredentialBoundAsync(string appId, string credentialReference, string method, string jsonBody, CancellationToken cancellationToken)
            {
                if(fixture.DropNextRequestMethod is { } unsent && method.EndsWith(unsent,StringComparison.Ordinal))
                {
                    using var frame=JsonDocument.Parse(jsonBody);
                    if(fixture.DropRequestStage is null || frame.RootElement.GetProperty("Payload").GetProperty("observationContext").GetProperty("Stage").GetString()==fixture.DropRequestStage)
                    { fixture.DropNextRequestMethod=null;return null; }
                }
                var context = new DefaultHttpContext(); context.RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(); context.Request.Headers["dapr-api-token"] = "isolated-channel";
                context.Request.Headers.Authorization = "Bearer " + fixture.Bearer;
                context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(jsonBody)); context.Response.Body = new MemoryStream();
                var result = await (replica??fixture._receiver).HandleAsync(context, method[(method.LastIndexOf('/') + 1)..], cancellationToken);
                await result.ExecuteAsync(context);fixture.AfterResponse?.Invoke();
                if(context.Response.StatusCode==200 && fixture.DropNextResponseMethod is { } dropped && method.EndsWith(dropped,StringComparison.Ordinal)) { fixture.DropNextResponseMethod=null;return null; }
                return context.Response.StatusCode == 200 ? Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray()) : null;
            }
        }
    }
}
