using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class QualificationChallengeRetryTests
{
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task RegisteredChallengeV2ActuallyVerifiesTheSecondChangedDeliveryAndPreservesFirstSeen(bool retryUnavailable)
    {
        using var authority=new IndependentAuthorityReceiverTests.Fixture();authority.State=authority.State with { Callers=authority.State.Callers.Select(value=>value with { Methods=["qualification-begin-original","qualification-retain-result","qualification-lookup-original"] }).ToArray() };
        var originals=new InstalledQualificationOriginalStore(authority.Client());var now=DateTimeOffset.UtcNow;
        var profile=new PlatformSigningProfile("fixture-r1","fixture-issuer","fixture-audience",now.AddHours(-1),now.AddHours(1),TimeSpan.FromSeconds(300),TimeSpan.FromSeconds(30),TimeSpan.FromSeconds(600),TimeSpan.FromDays(1),TimeSpan.FromDays(7));
        var profiles=Substitute.For<IPlatformSigningProfileProvider>();profiles.GetCurrent().Returns(profile);var keys=Substitute.For<IPlatformHmacKeyProvider>();
        keys.ResolveAsync(Arg.Any<PlatformHmacScope>(),Arg.Any<string?>(),Arg.Any<CancellationToken>()).Returns(call=>ValueTask.FromResult(new PlatformHmacKeyResolution(CustodyStatus.Succeeded,new(call.ArgAt<PlatformHmacScope>(0),new("v1",PlatformHmacKeyState.Active,now.AddHours(-1),now.AddHours(1),null),Enumerable.Repeat((byte)42,32).ToArray()))));
        var auth=new TrustedEnvelopeAuthenticator(keys,profiles,TimeProvider.System);var registrar=Substitute.For<ITrustedEnvelopeReplayRegistrar>();TrustedEnvelopeReplayReceipt? firstSeen=null;int changedCalls=0;
        registrar.RegisterAsync(Arg.Any<TrustedEnvelopeReplayIntent>(),Arg.Any<DateTimeOffset>(),Arg.Any<TimeSpan>(),Arg.Any<CancellationToken>()).Returns(call=>
        {
            var intent=call.ArgAt<TrustedEnvelopeReplayIntent>(0);if(firstSeen is not null) { if(intent!=firstSeen.Intent)changedCalls++;return firstSeen.Intent==intent?firstSeen:null; }
            var at=call.ArgAt<DateTimeOffset>(1);return firstSeen=new(intent,at,at.Add(call.ArgAt<TimeSpan>(2)),1,"immutable-first-seen");
        });
        registrar.LookupAsync(Arg.Any<TrustedEnvelopeReplayIntent>(),Arg.Any<CancellationToken>()).Returns(call=>firstSeen?.Intent==call.ArgAt<TrustedEnvelopeReplayIntent>(0)?firstSeen:null);
        registrar.LookupFirstSeenAsync(Arg.Any<TrustedEnvelopeReplayIntent>(),Arg.Any<CancellationToken>()).Returns(_=>retryUnavailable && changedCalls>=2?null:firstSeen);
        var replay=new TrustedEnvelopeReplayVerifier(auth,registrar,profiles,TimeProvider.System);
        var identity=new TrustedEnvelopeIdentity(1,"fixture-r1","fixture-issuer",new(TrustedPrincipalKind.User,"tenant-a","actor-a","party-a",1,"roles",null,null,null,null),"Contracts.EditProposal","EditProposal","tenant-a","interaction","correlation","cause",["tenant-a","User","actor-a","EditProposal","client-key"],"fingerprint","digest-v1","fixture-audience","logical-command");
        const string caseId="S2.replay-reject.changed-fields";var request=new JsonObject { ["caseId"]=caseId,["requestId"]=new string('a',64),["enrolledScope"]=new JsonObject { ["tenantId"]="tenant-a" } };
        var source=new QualificationEnvelopeCaseWorkflow(auth,replay,authority.Client(),new Dictionary<string,TrustedEnvelopeIdentity>{{caseId,identity}},registrar,originals,new Dictionary<string,QualificationReplayChallengeInput>{{caseId,new(ChangedIdentity:identity with { PayloadFingerprint="changed-fingerprint" })}});
        var workflow=QualificationRuntimeWorkflowCatalog.Create(originals,[caseId],null!,source).Single();var result=await workflow.Execute(request,TestContext.Current.CancellationToken);
        changedCalls.ShouldBe(2);firstSeen.ShouldNotBeNull();string exact=JsonSerializer.Serialize(firstSeen,CustodyBindingContract.Json);
        if(retryUnavailable) result.ShouldBeNull();else { result.ShouldNotBeNull()["firstStatus"]!.GetValue<string>().ShouldBe("Denied");result["exactRetryStatus"]!.GetValue<string>().ShouldBe("Denied"); }
        var recovered=await workflow.Lookup(request,TestContext.Current.CancellationToken);if(retryUnavailable) recovered.ShouldBeNull();else JsonNode.DeepEquals(result,recovered).ShouldBeTrue();
        changedCalls.ShouldBe(2);JsonSerializer.Serialize(await registrar.LookupAsync(firstSeen.Intent,TestContext.Current.CancellationToken),CustodyBindingContract.Json).ShouldBe(exact);
    }
}
