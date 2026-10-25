using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

// Isolated source/authority fixture clocks advance together. The actual authenticator still rejects expired bytes;
// no historical validation clock or success override is supplied to production recovery or independent verification.
public sealed class QualificationEnvelopeEvaluationRecoveryTests
{
    [Theory]
    [InlineData("S2.canonical-hmac","outer-before-retain")]
    [InlineData("S2.canonical-hmac","inner-after-retain")]
    [InlineData("S2.canonical-hmac","inner-before-retain")]
    [InlineData("S2.canonical-hmac","legacy-issuance-only")]
    [InlineData("S2.byte-identical-replay","outer-before-retain")]
    [InlineData("S2.byte-identical-replay","inner-after-retain")]
    [InlineData("S2.byte-identical-replay","inner-before-retain")]
    [InlineData("S2.byte-identical-replay","legacy-issuance-only")]
    public async Task LostEvaluationRecoveryAfterExpiryReadsExactBodyWithoutIssuingRegisteringOrVerifyingAgain(string caseId,string loss)
    {
        var clock=new SourceClock();using var authority=new IndependentAuthorityReceiverTests.Fixture(clock:clock);
        authority.State=authority.State with { Callers=authority.State.Callers.Select(value=>value with { Methods=["qualification-begin-original","qualification-retain-result","qualification-lookup-original"] }).ToArray() };
        var originals=new InstalledQualificationOriginalStore(authority.Client());var lossy=new LostAcknowledgementStore(originals,loss);
        DateTimeOffset started=clock.GetUtcNow();int profileReads=0,keyReads=0,registerCalls=0,replayReads=0;
        var profile=new PlatformSigningProfile("fixture-r1","fixture-issuer","fixture-audience",started.AddHours(-1),started.AddHours(1),
            TimeSpan.FromSeconds(300),TimeSpan.FromSeconds(30),TimeSpan.FromSeconds(600),TimeSpan.FromDays(1),TimeSpan.FromDays(7));
        var profiles=Substitute.For<IPlatformSigningProfileProvider>();profiles.GetCurrent().Returns(_=> { profileReads++;return profile; });
        var keys=Substitute.For<IPlatformHmacKeyProvider>();
        keys.ResolveAsync(Arg.Any<PlatformHmacScope>(),Arg.Any<string?>(),Arg.Any<CancellationToken>()).Returns(call=>
        {
            keyReads++;
            return ValueTask.FromResult(new PlatformHmacKeyResolution(CustodyStatus.Succeeded,new(call.ArgAt<PlatformHmacScope>(0),
                new("v1",PlatformHmacKeyState.Active,started.AddHours(-1),started.AddHours(1),null),Enumerable.Repeat((byte)42,32).ToArray())));
        });
        TrustedEnvelopeReplayReceipt? firstSeen=null;var registrar=Substitute.For<ITrustedEnvelopeReplayRegistrar>();
        registrar.RegisterAsync(Arg.Any<TrustedEnvelopeReplayIntent>(),Arg.Any<DateTimeOffset>(),Arg.Any<TimeSpan>(),Arg.Any<CancellationToken>()).Returns(call=>
        {
            registerCalls++;var intent=call.ArgAt<TrustedEnvelopeReplayIntent>(0);DateTimeOffset evaluatedAt=call.ArgAt<DateTimeOffset>(1);
            firstSeen??=new(intent,evaluatedAt,evaluatedAt.Add(call.ArgAt<TimeSpan>(2)),1,"isolated-retained-replay-original");
            return firstSeen.Intent==intent?firstSeen:null;
        });
        registrar.LookupAsync(Arg.Any<TrustedEnvelopeReplayIntent>(),Arg.Any<CancellationToken>()).Returns(call=>
        { replayReads++;return firstSeen?.Intent==call.ArgAt<TrustedEnvelopeReplayIntent>(0)?firstSeen:null; });
        var authenticator=new TrustedEnvelopeAuthenticator(keys,profiles,clock);var replay=new TrustedEnvelopeReplayVerifier(authenticator,registrar,profiles,clock);
        var identity=new TrustedEnvelopeIdentity(1,"fixture-r1","fixture-issuer",new(TrustedPrincipalKind.User,"tenant-a","actor-a","party-a",1,"roles",null,null,null,null),
            "Contracts.EditProposal","EditProposal","tenant-a","interaction-a","correlation","cause",["tenant-a","User","actor-a","EditProposal","client-key"],"fingerprint","digest-v1","fixture-audience","logical-command");
        var request=new JsonObject { ["caseId"]=caseId,["requestId"]=new string('a',64),["enrolledScope"]=new JsonObject { ["tenantId"]="tenant-a" } };
        QualificationEnvelopeCaseWorkflow Source()=>new(authenticator,replay,authority.Client(),new Dictionary<string,TrustedEnvelopeIdentity>{{caseId,identity}},registrar,lossy);
        QualificationSourceWorkflow Workflow()=>QualificationRuntimeWorkflowCatalog.Create(lossy,[caseId],null!,Source()).Single();
        if(loss=="legacy-issuance-only")
        {
            // Actual prior issuance survives, but no evaluation original does. Its bytes are not evidence
            // that the lost evaluation succeeded or permission to run that evaluation a second time.
            var issued=await authenticator.IssueAsync(identity,TimeSpan.FromSeconds(300),TestContext.Current.CancellationToken);issued.Status.ShouldBe(CustodyStatus.Succeeded);
            string requestId=request["requestId"]!.GetValue<string>();
            var begin=await authority.Client().InvokeAsync<JsonElement>("qualification-begin-original",new { role="executor",requestId=requestId+":envelope",original=new { requestId,caseId,identity } },TestContext.Current.CancellationToken);
            begin.GetProperty("First").GetBoolean().ShouldBeTrue();
            var retained=await authority.Client().InvokeAsync<JsonElement>("qualification-retain-result",new { role="executor",requestId=requestId+":envelope",result=issued },TestContext.Current.CancellationToken);
            retained.GetProperty("Exact").GetBoolean().ShouldBeTrue();
            var beforeUnknown=(profileReads,keyReads,registerCalls,replayReads);
            (await Workflow().Execute(request,TestContext.Current.CancellationToken)).ShouldBeNull();
            clock.Advance(TimeSpan.FromSeconds(301));authority.Restart();
            (await Workflow().Lookup(request,TestContext.Current.CancellationToken)).ShouldBeNull();
            (await Workflow().Execute(request,TestContext.Current.CancellationToken)).ShouldBeNull();
            (profileReads,keyReads,registerCalls,replayReads).ShouldBe(beforeUnknown);lossy.AttemptedInnerResult.ShouldBeNull();
            return;
        }
        await Should.ThrowAsync<IOException>(()=>Workflow().Execute(request,TestContext.Current.CancellationToken));
        var exact=lossy.AttemptedInnerResult.ShouldNotBeNull();string originalBytes=exact.ToJsonString();
        exact["firstStatus"]!.GetValue<string>().ShouldBe("Succeeded");exact["exactRetryStatus"]!.GetValue<string>().ShouldBe("Succeeded");
        keyReads.ShouldBeGreaterThan(0);profileReads.ShouldBeGreaterThan(0);registerCalls.ShouldBe(caseId=="S2.byte-identical-replay"?2:0);
        var beforeRecovery=(profileReads,keyReads,registerCalls,replayReads);
        clock.Advance(TimeSpan.FromSeconds(301));authority.Restart();
        var recovered=await Workflow().Lookup(request,TestContext.Current.CancellationToken);
        if(loss=="inner-before-retain") recovered.ShouldBeNull();else recovered.ShouldNotBeNull().ToJsonString().ShouldBe(originalBytes);
        var retried=await Workflow().Execute(request,TestContext.Current.CancellationToken);
        if(loss=="inner-before-retain") retried.ShouldBeNull();else retried.ShouldNotBeNull().ToJsonString().ShouldBe(originalBytes);
        (profileReads,keyReads,registerCalls,replayReads).ShouldBe(beforeRecovery);
        var changed=(JsonObject)request.DeepClone();changed["substituted-target"]="other";
        (await Source().LookupAsync(changed,TestContext.Current.CancellationToken)).ShouldBeNull();
        (profileReads,keyReads,registerCalls,replayReads).ShouldBe(beforeRecovery);
        // Independent current verification remains an actual current verification. Retained issuance/evaluation
        // supplies no missing historical native MAC evidence and cannot make an expired envelope authenticate now.
        var current=await Source().ReadVerifyAsync(request,TestContext.Current.CancellationToken);
        current.ShouldNotBeNull()["firstStatus"]!.GetValue<string>().ShouldBe("Expired");
        current["exactRetryStatus"]!.GetValue<string>().ShouldBe("Expired");
        keyReads.ShouldBe(beforeRecovery.keyReads);registerCalls.ShouldBe(beforeRecovery.registerCalls);replayReads.ShouldBe(beforeRecovery.replayReads);
        profileReads.ShouldBe(beforeRecovery.profileReads+2);
        authority.State=authority.State with { Callers=authority.State.Callers.Select(value=>value with { Revoked=true }).ToArray() };
        (await Source().LookupAsync(request,TestContext.Current.CancellationToken)).ShouldBeNull();
    }
    private sealed class SourceClock:TimeProvider
    {
        private DateTimeOffset _now=DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow()=>_now;
        internal void Advance(TimeSpan elapsed)=>_now+=elapsed;
    }
    private sealed class LostAcknowledgementStore(IQualificationOriginalStore store,string loss):IQualificationOriginalStore
    {
        private bool _lost;internal JsonObject? AttemptedInnerResult;
        public Task<bool> BeginAsync(string role,string id,JsonObject original,CancellationToken token)=>store.BeginAsync(role,id,original,token);
        public Task<QualificationOriginalAdmissionState> ReadAdmissionAsync(string role,string id,JsonObject original,CancellationToken token)=>store.ReadAdmissionAsync(role,id,original,token);
        public Task<JsonObject?> LookupResultAsync(string role,string id,CancellationToken token)=>store.LookupResultAsync(role,id,token);
        public async Task RetainAsync(string role,string id,JsonObject result,CancellationToken token)
        {
            bool inner=id.EndsWith(":envelope-evaluation-result",StringComparison.Ordinal);if(inner) AttemptedInnerResult=(JsonObject)result.DeepClone();
            bool lose=!_lost && (loss.StartsWith("inner-",StringComparison.Ordinal)?inner:id.EndsWith(":case-result",StringComparison.Ordinal));
            if(lose) { _lost=true;if(loss=="inner-after-retain") await store.RetainAsync(role,id,result,token);throw new IOException("Isolated evaluation-result acknowledgement loss."); }
            await store.RetainAsync(role,id,result,token);
        }
    }
}
