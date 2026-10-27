using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Client;
using Hexalith.Platform.Custody;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class DaprExportQualificationReferenceReaderTests
{
    [Theory]
    [InlineData("")][InlineData(" ")][InlineData("0")][InlineData("current")][InlineData("latest")][InlineData("alias")]
    [InlineData("+1")][InlineData("-1")][InlineData("01")][InlineData("1.0")][InlineData("1e0")][InlineData(" 1")][InlineData("1 ")]
    [InlineData("18446744073709551616")][InlineData("١")]
    public async Task ActualExportReferenceReaderRejectsUnpinnedNativeSelectorBeforeSecretOrInventoryRead(string nativeVersion)
    {
        using var dapr=Substitute.For<DaprClient>();var inventory=Substitute.For<IPlatformKeyInventoryActor>();var authority=Substitute.For<IPlatformKeyInventoryAuthority>();int decodes=0;
        var key=new PlatformKeyVersion("tenant",PlatformKeyPurpose.ExportEnvelopeKey,"export","logical-export-version","native-target","independent-authority",null,null);
        var reference=new DaprExportQualificationReference("store","exports/exact",nativeVersion,"value","Aes256Base64",key);
        var reader=new DaprExportQualificationReferenceReader(dapr,inventory,authority);
        (await reader.ReadAsync(reference,true,value=> { decodes++;return Convert.FromBase64String(value); },TestContext.Current.CancellationToken)).ShouldBeNull();
        decodes.ShouldBe(0);inventory.ReceivedCalls().ShouldBeEmpty();authority.ReceivedCalls().ShouldBeEmpty();
        await dapr.DidNotReceive().GetSecretAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<IReadOnlyDictionary<string,string>>(),Arg.Any<CancellationToken>());
        key.Version.ShouldBe("logical-export-version");
    }

    [Theory]
    [InlineData("7",false)][InlineData("7",true)][InlineData("18446744073709551615",false)]
    public async Task ActualExportReferenceReaderPinsNativeVersionAndPreservesCurrentChecksLogicalIdentityAndOwnedBuffers(string nativeVersion,bool revokeAfterRead)
    {
        using var dapr=Substitute.For<DaprClient>();var inventory=Substitute.For<IPlatformKeyInventoryActor>();var authority=Substitute.For<IPlatformKeyInventoryAuthority>();bool read=false;
        var key=new PlatformKeyVersion("tenant",PlatformKeyPurpose.ExportEnvelopeKey,"export","logical-export-version","native-target","independent-authority",null,null);
        var reference=new DaprExportQualificationReference("store","exports/exact",nativeVersion,"value","Aes256Base64",key);
        var state=new PlatformKeyInventorySnapshot("tenant",key.Purpose,key.KeyAlias,9,[new(key,PlatformHmacKeyState.Active,9)],[]);
        inventory.ReadAsync(key).Returns(state);
        authority.AuthorizeOperationAsync(key,"ResolveExportQualificationMaterial",Arg.Any<CancellationToken>()).Returns(_=>!revokeAfterRead || !read);
        authority.ValidateStateAsync(key,9,Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(state))),Arg.Any<CancellationToken>()).Returns(true);
        var response=new Dictionary<string,string>{{"value",Convert.ToBase64String(Enumerable.Repeat((byte)84,32).ToArray())}};
        dapr.GetSecretAsync("store","exports/exact",Arg.Is<IReadOnlyDictionary<string,string>>(metadata=>metadata.Count==1 && metadata["version_id"]==nativeVersion),Arg.Any<CancellationToken>()).Returns(_=> { read=true;return response; });
        byte[]? decoded=null;
        var actual=await new DaprExportQualificationReferenceReader(dapr,inventory,authority).ReadAsync(reference,true,value=>decoded=DaprExportQualificationReferenceReader.Base64(value,32),TestContext.Current.CancellationToken);
        response.ShouldBeEmpty();decoded.ShouldNotBeNull();
        if(revokeAfterRead) { actual.ShouldBeNull();decoded.ShouldAllBe(value=>value==0);await inventory.Received(1).ReadAsync(key); }
        else { actual.ShouldNotBeNull();ReferenceEquals(actual,decoded).ShouldBeTrue();actual.ShouldAllBe(value=>value==84);CryptographicOperations.ZeroMemory(actual);await inventory.Received(2).ReadAsync(key); }
        await authority.Received(2).AuthorizeOperationAsync(key,"ResolveExportQualificationMaterial",Arg.Any<CancellationToken>());
        await dapr.Received(1).GetSecretAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<IReadOnlyDictionary<string,string>>(),Arg.Any<CancellationToken>());
        key.Version.ShouldBe("logical-export-version");
    }
}
