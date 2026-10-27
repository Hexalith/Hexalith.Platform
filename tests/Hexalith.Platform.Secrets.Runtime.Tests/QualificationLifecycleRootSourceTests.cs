using Dapr.Client;
using Hexalith.Platform.Custody;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class QualificationLifecycleRootSourceTests
{
    [Theory]
    [InlineData("")][InlineData("0")][InlineData("current")][InlineData("latest")][InlineData("alias")][InlineData("+1")][InlineData("-1")][InlineData("01")][InlineData(" 1")][InlineData("18446744073709551616")]
    public async Task UnpinnedNativeVersionNeverCallsDapr(string version)
    {
        using var dapr=Substitute.For<DaprClient>();var authority=Substitute.For<ICustodyOwnerStateAuthority>();
        var registration=new CustodyKeyRegistration(new("tenant","interaction",PlatformKeyPurpose.InteractionRootDek,"alias","root-v1","lifecycle-v1","native","v1"),"wrap",1,"kek-v1","opaque","wrap-receipt");
        var source=new DaprQualificationLifecycleRootSource(dapr,authority,new Dictionary<string,QualificationLifecycleRootReference>{{"wrap",new("component","roots/exact",version,"value")}});
        (await source.ReadAsync(registration,new("lease","attempt","process","transport","opaque"),TestContext.Current.CancellationToken)).ShouldBeNull();
        dapr.ReceivedCalls().ShouldBeEmpty();authority.ReceivedCalls().ShouldBeEmpty();
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task ExactVersionFieldAndCurrentAuthoritySurroundPrivateRootAndClearTransportDictionary(bool revokeAfterRead)
    {
        using var dapr=Substitute.For<DaprClient>();var authority=Substitute.For<ICustodyOwnerStateAuthority>();var response=new Dictionary<string,string>{{"value",Convert.ToBase64String(Enumerable.Repeat((byte)42,32).ToArray())}};bool read=false;
        authority.AuthorizeOperationAsync(Arg.Any<string>(),"ReadQualificationLifecycleRoot",Arg.Any<CancellationToken>()).Returns(_=>!revokeAfterRead || !read);
        dapr.GetSecretAsync("component","roots/exact",Arg.Is<IReadOnlyDictionary<string,string>>(value=>value.Count==1 && value["version_id"]=="7"),Arg.Any<CancellationToken>()).Returns(_=> { read=true;return response; });
        var registration=new CustodyKeyRegistration(new("tenant","interaction",PlatformKeyPurpose.InteractionRootDek,"alias","root-v1","lifecycle-v1","native","v1"),"wrap",1,"kek-v1","opaque","wrap-receipt");
        var source=new DaprQualificationLifecycleRootSource(dapr,authority,new Dictionary<string,QualificationLifecycleRootReference>{{"wrap",new("component","roots/exact","7","value")}});
        var actual=await source.ReadAsync(registration,new("lease","attempt","process","transport","opaque"),TestContext.Current.CancellationToken);
        response.ShouldBeEmpty();if(revokeAfterRead) actual.ShouldBeNull();else { actual.ShouldNotBeNull();actual.ShouldAllBe(value=>value==42);System.Security.Cryptography.CryptographicOperations.ZeroMemory(actual); }
        (await source.ReadAsync(registration,new("lease","attempt","process","transport","substituted"),TestContext.Current.CancellationToken)).ShouldBeNull();
        await dapr.Received(1).GetSecretAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<IReadOnlyDictionary<string,string>>(),Arg.Any<CancellationToken>());
    }
}
