using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Client;
using Hexalith.Platform.Custody;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class QualificationResolutionSourceTests
{
    [Theory]
    [InlineData("")][InlineData("0")][InlineData("current")][InlineData("latest")][InlineData("alias")][InlineData("+1")][InlineData("-1")][InlineData("01")][InlineData("1 ")][InlineData("18446744073709551616")]
    public async Task ProviderCredentialRejectsUnpinnedNativeVersionBeforeAnyRead(string version)
    {
        using var client=Substitute.For<DaprClient>();var actor=Substitute.For<IPlatformKeyInventoryActor>();var authority=Substitute.For<IPlatformKeyInventoryAuthority>();
        var request=QualificationHostTests.Request();request["caseId"]="S1.exact-provider-credential";
        var key=new PlatformKeyVersion("tenant",PlatformKeyPurpose.ProviderCredential,"provider-credential","logical-v7","native-provider","outside-authority",null,null);
        var workflow=new QualificationProviderCredentialWorkflow(client,actor,authority,new(key,"exact-store","isolated/provider/token","token",version,QualificationProtocolSchema.Digest(request["enrolledScope"]!)));
        (await workflow.ResolveAsync(request,TestContext.Current.CancellationToken)).ShouldBeNull();client.ReceivedCalls().ShouldBeEmpty();actor.ReceivedCalls().ShouldBeEmpty();
    }
    [Fact]
    public async Task ExactPrivateProviderCredentialUsesCurrentActorAuthorityAndOnlyOnePinnedSecretRead()
    {
        var client=Substitute.For<DaprClient>();var actor=Substitute.For<IPlatformKeyInventoryActor>();var authority=Substitute.For<IPlatformKeyInventoryAuthority>();
        var request=QualificationHostTests.Request();request["caseId"]="S1.exact-provider-credential";
        var key=new PlatformKeyVersion("tenant",PlatformKeyPurpose.ProviderCredential,"provider-credential","v7","native-provider","outside-authority",null,null);
        var state=new PlatformKeyInventorySnapshot("tenant",PlatformKeyPurpose.ProviderCredential,key.KeyAlias,9,[new(key,PlatformHmacKeyState.Active,9)],[]);
        actor.ReadAsync(key).Returns(state);authority.AuthorizeOperationAsync(key,"ResolveProviderCredential",Arg.Any<CancellationToken>()).Returns(true);
        authority.ValidateStateAsync(key,9,CustodyBindingContract.Hash(JsonSerializer.SerializeToUtf8Bytes(state)),Arg.Any<CancellationToken>()).Returns(true);
        var response=new Dictionary<string,string>{{"token","fixture-private-provider-value"}};
        client.GetSecretAsync("exact-store","isolated/provider/token",Arg.Any<IReadOnlyDictionary<string,string>>(),Arg.Any<CancellationToken>()).Returns(call=>
        { call.ArgAt<IReadOnlyDictionary<string,string>>(2).ShouldBe(new Dictionary<string,string>{{"version_id","7"}});return response; });
        var workflow=new QualificationProviderCredentialWorkflow(client,actor,authority,new(key,"exact-store","isolated/provider/token","token","7",QualificationProtocolSchema.Digest(request["enrolledScope"]!)));
        var result=await workflow.ResolveAsync(request,TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();result["exactResolvedReferenceCount"]!.GetValue<int>().ShouldBe(1);result.ToJsonString().ShouldNotContain("fixture-private-provider-value");response.ShouldBeEmpty();
        await actor.Received(2).ReadAsync(key);await client.Received(1).GetSecretAsync("exact-store","isolated/provider/token",Arg.Any<IReadOnlyDictionary<string,string>>(),Arg.Any<CancellationToken>());
        var changed=(JsonObject)request.DeepClone();changed["enrolledScope"]!["tenantId"]="foreign";
        (await workflow.ResolveAsync(changed,TestContext.Current.CancellationToken)).ShouldBeNull();
        await client.Received(1).GetSecretAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<IReadOnlyDictionary<string,string>>(),Arg.Any<CancellationToken>());
    }
    [Fact]
    public async Task RevisionChangeDuringPinnedCredentialResponseDeniesAndClearsResponse()
    {
        var client=Substitute.For<DaprClient>();var actor=Substitute.For<IPlatformKeyInventoryActor>();var authority=Substitute.For<IPlatformKeyInventoryAuthority>();
        var request=QualificationHostTests.Request();request["caseId"]="S1.exact-provider-credential";
        var key=new PlatformKeyVersion("tenant",PlatformKeyPurpose.ProviderCredential,"provider-credential","v7","native-provider","outside-authority",null,null);
        var before=new PlatformKeyInventorySnapshot("tenant",PlatformKeyPurpose.ProviderCredential,key.KeyAlias,9,[new(key,PlatformHmacKeyState.Active,9)],[]);
        var after=before with { Revision=10 };bool advanced=false;
        actor.ReadAsync(key).Returns(_=>advanced?after:before);
        authority.AuthorizeOperationAsync(key,"ResolveProviderCredential",Arg.Any<CancellationToken>()).Returns(true);
        foreach(var snapshot in new[]{before,after}) authority.ValidateStateAsync(key,snapshot.Revision,CustodyBindingContract.Hash(JsonSerializer.SerializeToUtf8Bytes(snapshot)),Arg.Any<CancellationToken>()).Returns(true);
        var response=new Dictionary<string,string>{{"token","isolated-private-value"}};
        client.GetSecretAsync("exact-store","isolated/provider/token",Arg.Any<IReadOnlyDictionary<string,string>>(),Arg.Any<CancellationToken>()).Returns(call=>
        { call.ArgAt<IReadOnlyDictionary<string,string>>(2)["version_id"].ShouldBe("7");advanced=true;return response; });
        var workflow=new QualificationProviderCredentialWorkflow(client,actor,authority,new(key,"exact-store","isolated/provider/token","token","7",QualificationProtocolSchema.Digest(request["enrolledScope"]!)));
        (await workflow.ResolveAsync(request,TestContext.Current.CancellationToken)).ShouldBeNull();response.ShouldBeEmpty();
        await actor.Received(2).ReadAsync(key);await client.Received(1).GetSecretAsync("exact-store","isolated/provider/token",Arg.Any<IReadOnlyDictionary<string,string>>(),Arg.Any<CancellationToken>());
    }
    [Fact]
    public async Task MissingReferenceUsesActualAdmissionAndDoesNotCallTransport()
    {
        var invoker=Substitute.For<ISecretsDaprInvoker>();var request=QualificationHostTests.Request();request["caseId"]="S1.reject.missing-reference";
        var raw=await new QualificationMissingReferenceWorkflow(Fixtures.Ready(),invoker).VerifyAsync(request,TestContext.Current.CancellationToken);
        raw.ShouldNotBeNull();raw["missingReference"]!.GetValue<string>().ShouldBe("runtime.hmacCredentialReference");invoker.ReceivedCalls().ShouldBeEmpty();
    }
    [Fact]
    public async Task RevokedScenarioRequiresActualRevokedResultAndUnavailableCannotBecomeDenial()
    {
        var keys=Substitute.For<IPlatformHmacKeyProvider>();var profiles=Substitute.For<IPlatformSigningProfileProvider>();var request=QualificationHostTests.Request();request["caseId"]="S1.reject.revoked";
        var input=new QualificationHmacResolutionCaseInput(new("tenant",PlatformHmacPurpose.ContentDigest),"v1","other",PlatformHmacPurpose.TrustedEnvelope);
        var workflow=new QualificationHmacResolutionWorkflow(keys,profiles,new Dictionary<string,QualificationHmacResolutionCaseInput>{{"S1.reject.revoked",input}});
        keys.ResolveAsync(input.Scope,"v1",Arg.Any<CancellationToken>()).Returns(new PlatformHmacKeyResolution(CustodyStatus.Denied));
        (await workflow.ResolveAsync(request,TestContext.Current.CancellationToken)).ShouldBeNull();
        keys.ResolveAsync(input.Scope,"v1",Arg.Any<CancellationToken>()).Returns(new PlatformHmacKeyResolution(CustodyStatus.Unavailable));
        (await workflow.ResolveAsync(request,TestContext.Current.CancellationToken)).ShouldBeNull();
        keys.ResolveAsync(input.Scope,"v1",Arg.Any<CancellationToken>()).Returns(new PlatformHmacKeyResolution(CustodyStatus.RevokedKey));
        (await workflow.ResolveAsync(request,TestContext.Current.CancellationToken))!["resolutionStatus"]!.GetValue<string>().ShouldBe("RevokedKey");
    }
}
