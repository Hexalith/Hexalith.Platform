using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class QualificationEnvelopeSubstitutionTests
{
    [Theory]
    [InlineData("principal",true)][InlineData("principal",false)][InlineData("fingerprint",true)][InlineData("fingerprint",false)]
    public async Task SubstitutionAlwaysChangesAValidComponentIncludingWorkflowAndZeroBaselines(string field,bool edge)
    {
        using var originals=new IndependentAuthorityReceiverTests.Fixture();originals.State=originals.State with { Callers=originals.State.Callers.Select(v=>v with { Methods=["qualification-begin-original","qualification-retain-result","qualification-lookup-original"] }).ToArray() };
        var now=DateTimeOffset.UtcNow;var profiles=Substitute.For<IPlatformSigningProfileProvider>();profiles.GetCurrent().Returns(new PlatformSigningProfile("profile","issuer","audience",now.AddHours(-1),now.AddHours(1),TimeSpan.FromSeconds(300),TimeSpan.FromSeconds(30),TimeSpan.FromSeconds(600),TimeSpan.FromDays(1),TimeSpan.FromDays(7)));
        var keys=Substitute.For<IPlatformHmacKeyProvider>();keys.ResolveAsync(Arg.Any<PlatformHmacScope>(),Arg.Any<string?>(),Arg.Any<CancellationToken>()).Returns(call=>ValueTask.FromResult(new PlatformHmacKeyResolution(CustodyStatus.Succeeded,new(call.ArgAt<PlatformHmacScope>(0),new("v1",PlatformHmacKeyState.Active,now.AddHours(-1),now.AddHours(1),null),new byte[32]))));
        var principal=edge?new TrustedPrincipal(TrustedPrincipalKind.Workflow,null,null,null,null,null,"SystemTimer","workflow","activity",null):new(TrustedPrincipalKind.User,"tenant","actor","party",1,"roles",null,null,null,null);
        var identity=new TrustedEnvelopeIdentity(1,"profile","issuer",principal,"contract","operation","tenant","resource","correlation","cause",["tuple"],new string(edge?'0':'a',64),"digest","audience","logical");
        var auth=new TrustedEnvelopeAuthenticator(keys,profiles,TimeProvider.System);var replay=new TrustedEnvelopeReplayVerifier(auth,Substitute.For<ITrustedEnvelopeReplayRegistrar>(),profiles,TimeProvider.System);string id="S2.substitution."+field;
        var request=new JsonObject { ["caseId"]=id,["requestId"]=new string('a',64),["enrolledScope"]=new JsonObject { ["tenantId"]="tenant" } };
        var workflow=new QualificationEnvelopeCaseWorkflow(auth,replay,originals.Client(),new Dictionary<string,TrustedEnvelopeIdentity>{{id,identity}});
        var result=(await workflow.ExecuteAsync(request,TestContext.Current.CancellationToken)).ShouldNotBeNull();result["firstStatus"]!.GetValue<string>().ShouldBe("ScopeMismatch");
        var expected=result["expectedIdentity"]!.Deserialize<TrustedEnvelopeIdentity>(CustodyBindingContract.Json).ShouldNotBeNull();expected.Principal.IsValid("tenant").ShouldBeTrue();
        if(field=="principal") { expected.Principal.Kind.ShouldNotBe(principal.Kind);expected.PayloadFingerprint.ShouldBe(identity.PayloadFingerprint); }
        else { expected.PayloadFingerprint.ShouldNotBe(identity.PayloadFingerprint);expected.Principal.ShouldBe(principal); }
        expected.TargetTenantId.ShouldBe(identity.TargetTenantId);expected.LogicalCommandId.ShouldBe(identity.LogicalCommandId);
    }
}
