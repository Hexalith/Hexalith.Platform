using System.Text.Json;
using System.Security.Cryptography;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class QualificationExportSourceTests
{
    [Theory]
    [InlineData("S3.aes256gcm", "Verified")]
    [InlineData("S3.es256-offline-anchor", "Verified")]
    [InlineData("S3.crypto-substitution.ciphertext", "Denied")]
    [InlineData("S3.crypto-substitution.tag", "Denied")]
    [InlineData("S3.crypto-substitution.tenant", "Denied")]
    [InlineData("S3.crypto-substitution.export", "Denied")]
    [InlineData("S3.crypto-substitution.purpose", "Denied")]
    [InlineData("S3.crypto-substitution.version", "Denied")]
    [InlineData("S3.crypto-substitution.jws-header", "Denied")]
    [InlineData("S3.crypto-substitution.public-anchor", "Denied")]
    public async Task CustodyOwnedActualExportCoresVerifyOriginalsAndRejectAllClosedSubstitutions(string caseId,string expected)
    {
        using var fixture=new ExportQualificationFixture();var engine=fixture.Engine();var input=fixture.Input("export");
        var original=await engine.ExecuteAsync(caseId,input,TestContext.Current.CancellationToken);original.ShouldNotBeNull();original.OwnedBuffersCleared.ShouldBeTrue();
        var observation=await engine.ObserveAsync(original,input,TestContext.Current.CancellationToken);observation.ShouldNotBeNull();observation.Outcome.ShouldBe(expected);if(original.Ciphertext is not null) observation.OwnedPlaintextCleared.ShouldBe(true);else observation.OwnedPlaintextCleared.ShouldBeNull();
        fixture.ReleasedBuffers.ShouldNotBeEmpty();fixture.ReleasedBuffers.ShouldAllBe(bytes=>bytes.All(value=>value==0));
        var serialized=JsonSerializer.Serialize(original);serialized.ShouldNotContain("ExportKey");serialized.ShouldNotContain("PrivateKey");
        (await engine.ObserveAsync(original,input with { TenantId="foreign" },TestContext.Current.CancellationToken)).ShouldBeNull();
        if(original.Ciphertext is not null) (await engine.ObserveAsync(original with { OwnedBuffersCleared=false },input,TestContext.Current.CancellationToken))!.OwnedPlaintextCleared.ShouldBe(false);
        byte[] manifest=input.Manifest.ToArray();manifest[0]^=1;
        (await engine.ObserveAsync(original,input with { Manifest=manifest },TestContext.Current.CancellationToken)).ShouldBeNull();
    }
}
