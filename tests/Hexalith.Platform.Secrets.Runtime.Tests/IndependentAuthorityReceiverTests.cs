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
            ["targetCommit"] = "\"accepted-target\"", ["isolatedTargetId"] = "\"isolated-target\"", ["planSha256"] = "\"accepted-plan\"" },
            QualificationCases = new() { ["S1.test"] = JsonSerializer.SerializeToElement(new { caseId = "S1.test", caseTargetId = "target-a", enrolledScope = "scope-a" }) },
            Callers = fixture.State.Callers.Select(c => c with { Methods = ["qualification-authorize"] }).ToArray() };
        var request = new System.Text.Json.Nodes.JsonObject { ["targetCommit"] = "accepted-target", ["isolatedTargetId"] = "isolated-target", ["planSha256"] = "accepted-plan", ["caseId"] = "S1.test", ["caseTargetId"] = "target-a", ["enrolledScope"] = "scope-a" };
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

    private sealed record Permission(bool Allowed);

    private sealed class Fixture : IDisposable
    {
        private readonly string _channel = Path.Combine(Path.GetTempPath(), "story54-authority-" + Guid.NewGuid().ToString("N"));
        private readonly ECDsa _signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        private readonly DaprClient _dapr = Substitute.For<DaprClient>();
        private readonly IndependentAuthorityReceiverInstallation _installation;
        private IndependentAuthorityReceiver _receiver;
        private int _revision;
        internal IndependentAuthorityState State;
        internal int Saves;
        internal IPrivateRecordingEventStore RecordingSource { get; } = Substitute.For<IPrivateRecordingEventStore>();
        internal string Bearer = "isolated-caller";
        internal ReplicatedSecuritySpoolTarget Target { get; } = new("isolated-spool", "isolated-epoch", "current-1", DateTimeOffset.UtcNow.AddHours(1));
        internal string Scope => Target.ComponentName + "|system/security-observations/" + Target.InstallationEpoch;
        internal Fixture()
        {
            File.WriteAllText(_channel, "isolated-channel");
            string archive = _channel + "-archive"; Directory.CreateDirectory(archive);
            File.WriteAllText(Path.Combine(archive, "enrollment.json"), "isolated-archive-enrollment");
            _installation = new("isolated-enrollment", "isolated-owner", "current-1", "independent-test-store", "head", "unused", _signer.ExportSubjectPublicKeyInfoPem(), "file:" + _channel, OriginalArchiveDirectory: archive, ArchiveEnrollmentSha256: Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(archive, "enrollment.json")))), CompletionReserveBytes: 1024 * 1024);
            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Bearer)));
            State = new("isolated-enrollment", "isolated-owner", "current-1", 0, DateTimeOffset.UtcNow.AddHours(1),
                [new(hash, "worker", "scope", "executor", ["spool-current", "spool-admit-transition", "spool-record-transition", "spool-recover-transition", "spool-verify-transition", "spool-validate", "qualification-begin-original"], DateTimeOffset.UtcNow.AddHours(1), false)],
                Target, 0, new string('A', 64), [], [], [], [], [], []);
            _dapr.GetStateAndETagAsync<IndependentAuthorityState>("independent-test-store", "head", Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
                .Returns(_ => Task.FromResult((State, _revision.ToString())));
            _dapr.TrySaveStateAsync("independent-test-store", "head", Arg.Any<IndependentAuthorityState>(), Arg.Any<string>(), Arg.Any<StateOptions>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
                .Returns(call => { if (call.ArgAt<string>(3) != _revision.ToString()) return false; State = call.ArgAt<IndependentAuthorityState>(2); _revision++; Saves++; return true; });
            _receiver = new(_dapr, _installation, _signer, TimeProvider.System, RecordingSource);
        }
        internal void Restart() => _receiver = new(_dapr, _installation, _signer, TimeProvider.System, RecordingSource);
        internal IndependentDaprAuthority Client() => new(new Transport(this), new("isolated-owner", "file:/isolated-bearer", _signer.ExportSubjectPublicKeyInfoPem(), "isolated-enrollment", "isolated-owner", "current-1"), TimeProvider.System);
        public void Dispose() { _signer.Dispose(); _dapr.Dispose(); File.Delete(_channel); Directory.Delete(_channel + "-archive", true); }
        private sealed class Transport(Fixture fixture) : ISecretsCredentialBoundDaprInvoker
        {
            public Task<string?> InvokeAsync(string appId, string method, string jsonBody, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
            public async Task<string?> InvokeCredentialBoundAsync(string appId, string credentialReference, string method, string jsonBody, CancellationToken cancellationToken)
            {
                var context = new DefaultHttpContext(); context.RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(); context.Request.Headers["dapr-api-token"] = "isolated-channel";
                context.Request.Headers.Authorization = "Bearer " + fixture.Bearer;
                context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(jsonBody)); context.Response.Body = new MemoryStream();
                var result = await fixture._receiver.HandleAsync(context, method[(method.LastIndexOf('/') + 1)..], cancellationToken);
                await result.ExecuteAsync(context);
                return context.Response.StatusCode == 200 ? Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray()) : null;
            }
        }
    }
}
