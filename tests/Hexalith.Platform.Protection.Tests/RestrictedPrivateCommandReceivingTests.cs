using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

public sealed class RestrictedPrivateCommandReceivingTests
{
    [Fact]
    public async Task ActualReceiverAndProofSourceRequireExactRetainedPayloadAndCurrentWorkload()
    {
        using var authority = new ReceivingAuthorityFixture("private-replay");
        using var key=RSA.Create(2048); string file=Path.GetTempFileName();
        await File.WriteAllTextAsync(file,Convert.ToBase64String(key.ExportPkcs8PrivateKey()),TestContext.Current.CancellationToken);
        try
        {
            var proofSource=new RestrictedPrivateCommandProofSource("recording-owner","private-worker",file,authority.Authority,TimeProvider.System);
            DateTimeOffset now=DateTimeOffset.UtcNow;
            var payload=JsonSerializer.SerializeToElement(new { issuer="issuer",deliveryNonce="nonce",targetTenantId="tenant-a",logicalCommandId="logical",signingKeyVersion="v1",canonicalAndTagDigest=new string('a',64),firstSeenAt=now,retainUntil=now.AddSeconds(604800) });
            string aggregate=Convert.ToHexStringLower(SHA256.HashData(PlatformCanonicalBytes.Identity(["trusted-envelope-replay","issuer","nonce"])));
            var command=new SubmitCommandRequest("01ARZ3NDEKTSV4RRFFQ69G5FAV","system","trusted-envelope-replay",aggregate,
                "Hexalith.Agents.Contracts.TrustedEnvelopeReplay.RegisterTrustedEnvelopeFirstSeen",payload,IdempotencyKey:"01ARZ3NDEKTSV4RRFFQ69G5FAV");
            var request=new BoundWorkloadCommandRequest(command,"private-worker");
            (await proofSource.IssueAsync(request,TestContext.Current.CancellationToken)).ShouldBeNull();
            (await authority.Authority.InvokeAsync<Admission>("qualification-begin-original",new { role="private-replay",requestId=command.MessageId,original=payload },TestContext.Current.CancellationToken)).ShouldBe(new(true,true));
            string? proof=await proofSource.IssueAsync(request,TestContext.Current.CancellationToken);
            proof.ShouldNotBeNull();
            BoundWorkloadCommandProof.Verify(request,proof,"recording-owner",Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),DateTimeOffset.UtcNow).ShouldBeTrue();
            var changed=JsonSerializer.SerializeToElement(new { issuer="issuer",deliveryNonce="nonce",targetTenantId="tenant-b",logicalCommandId="logical",signingKeyVersion="v1",canonicalAndTagDigest=new string('a',64),firstSeenAt=now,retainUntil=now.AddSeconds(604800) });
            (await proofSource.IssueAsync(request with { Command=command with { Payload=changed } },TestContext.Current.CancellationToken)).ShouldBeNull();
            authority.State=authority.State with { Callers=authority.State.Callers.Select(value=>value with { Revoked=true }).ToArray() };
            (await proofSource.IssueAsync(request,TestContext.Current.CancellationToken)).ShouldBeNull();
        }
        finally { File.Delete(file); }
    }
    private sealed record Admission(bool Exact,bool First);
}
