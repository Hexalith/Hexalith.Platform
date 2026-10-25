using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;
using Shouldly;
using NSubstitute;

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
    [Fact]
    public async Task ReplayTerminalRecoveryReceivingBoundaryRequiresFreshNativeProofExactBodyAndDeterministicTransport()
    {
        var source=NSubstitute.Substitute.For<IPrivateRecordingEventStore,IPrivateReplayRecoverySource>();
        using var authority=new ReceivingAuthorityFixture("private-replay",source);using var key=RSA.Create(2048);string file=Path.GetTempFileName();
        await File.WriteAllTextAsync(file,Convert.ToBase64String(key.ExportPkcs8PrivateKey()),TestContext.Current.CancellationToken);
        try
        {
            DateTimeOffset now=DateTimeOffset.UtcNow;
            var intent=new TrustedEnvelopeReplayIntent("issuer","nonce","tenant-a","logical","v1",new string('a',64));
            var payload=JsonSerializer.SerializeToElement(new { issuer=intent.Issuer,deliveryNonce=intent.DeliveryNonce,targetTenantId=intent.TargetTenantId,logicalCommandId=intent.LogicalCommandId,signingKeyVersion=intent.SigningKeyVersion,canonicalAndTagDigest=intent.CanonicalAndTagDigest,firstSeenAt=now,retainUntil=now.AddSeconds(604800) });
            string aggregate=Convert.ToHexStringLower(SHA256.HashData(PlatformCanonicalBytes.Identity(["trusted-envelope-replay","issuer","nonce"])));
            string originalId=PrivateRecordingIdentity.StableUlid("replay",new Hexalith.EventStore.Contracts.Identity.AggregateIdentity("system","trusted-envelope-replay",aggregate).ActorId);
            await authority.Authority.InvokeAsync<Admission>("qualification-begin-original",new { role="private-replay",requestId=originalId,original=payload },TestContext.Current.CancellationToken);
            (await authority.Authority.InvokeAsync<Permission>("recording-dispatch-admit",new { role="private-replay",requestId=originalId,original=payload },TestContext.Current.CancellationToken))!.Allowed.ShouldBeTrue();
            var proof=new PrivateReplayRecoveryProof(intent,originalId,originalId,payload,new(originalId,originalId,new string('b',64),"native-incarnation",0,new string('c',64),new string('d',64),"TerminalNoEvent"));
            ((IPrivateReplayRecoverySource)source).ReadReplayRecoveryProofAsync(intent,Arg.Any<CancellationToken>()).Returns(proof);
            string transport=PrivateRecordingIdentity.ReplayRecoveryMessageId(proof);
            await authority.Authority.InvokeAsync<Admission>("qualification-begin-original",new { role="private-replay",requestId=transport,original=payload },TestContext.Current.CancellationToken);
            var command=new SubmitCommandRequest(transport,"system","trusted-envelope-replay",aggregate,"Hexalith.Agents.Contracts.TrustedEnvelopeReplay.RegisterTrustedEnvelopeFirstSeen",payload,IdempotencyKey:transport);
            var request=new BoundWorkloadCommandRequest(command,"private-worker");var signer=new RestrictedPrivateCommandProofSource("recording-owner","private-worker",file,authority.Authority,TimeProvider.System);
            var signed=await signer.IssueReplayRecoveryAsync(request,proof,TestContext.Current.CancellationToken);signed.ShouldNotBeNull();
            BoundWorkloadCommandProof.Verify(request,signed,"recording-owner",Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),now).ShouldBeTrue();
            var changed=proof with { PreviousTransportMessageId="changed" };
            (await signer.IssueReplayRecoveryAsync(request,changed,TestContext.Current.CancellationToken)).ShouldBeNull();
            ((IPrivateReplayRecoverySource)source).ReadReplayRecoveryProofAsync(intent,Arg.Any<CancellationToken>()).Returns((PrivateReplayRecoveryProof?)null);
            (await signer.IssueReplayRecoveryAsync(request,proof,TestContext.Current.CancellationToken)).ShouldBeNull();
            ((IPrivateReplayRecoverySource)source).ReadReplayRecoveryProofAsync(intent,Arg.Any<CancellationToken>()).Returns(proof);
            (await authority.Authority.InvokeAsync<Permission>("recording-replay-recovery-dispatch-admit",new { command,proof },TestContext.Current.CancellationToken))!.Allowed.ShouldBeTrue();
            (await signer.IssueReplayRecoveryAsync(request,proof,TestContext.Current.CancellationToken)).ShouldBeNull();
        }
        finally { File.Delete(file); }
    }
    [Fact]
    public async Task LegacyRecorderRequiresExactLegacyStreamScopeAndNeverUsesGeneralAgentsGrant()
    {
        using var authority=new ReceivingAuthorityFixture("private-security-observation");using var key=RSA.Create(2048);string file=Path.GetTempFileName();
        await File.WriteAllTextAsync(file,Convert.ToBase64String(key.ExportPkcs8PrivateKey()),TestContext.Current.CancellationToken);
        try
        {
            DateTimeOffset now=DateTimeOffset.UtcNow;string day=now.ToString("yyyy-MM-dd");
            var payload=JsonSerializer.SerializeToElement(new { securityObservationId="observation",routingTenantId="tenant-a",utcDay=day,observedAt=now,reasonCode="denied",untrustedFieldsHmac=new string('a',64),digestKeyVersion="v1" });
            string id=PrivateRecordingIdentity.SecurityOriginalMessageId("observation");
            await authority.Authority.InvokeAsync<Admission>("qualification-begin-original",new { role="private-security-observation",requestId=id,original=payload },TestContext.Current.CancellationToken);
            var command=new SubmitCommandRequest(id,"tenant-a","agents","SecurityEventLog-"+day,"Hexalith.Agents.Contracts.SecurityEventLog.RecordSecurityEvent",payload,IdempotencyKey:id);
            var request=new BoundWorkloadCommandRequest(command,"private-worker");var signer=new RestrictedPrivateCommandProofSource("recording-owner","private-worker",file,authority.Authority,TimeProvider.System);
            (await signer.IssueAsync(request,TestContext.Current.CancellationToken)).ShouldBeNull();
            authority.State=authority.State with { Callers=authority.State.Callers.Select(value=>value with { Methods=value.Methods.Concat(["recording-source:agents"]).ToArray() }).ToArray() };
            (await signer.IssueAsync(request,TestContext.Current.CancellationToken)).ShouldBeNull();
            authority.State=authority.State with { Callers=authority.State.Callers.Select(value=>value with { Methods=value.Methods.Concat(["recording-source:legacy-security-event-log"]).ToArray() }).ToArray() };
            var proof=await signer.IssueAsync(request,TestContext.Current.CancellationToken);proof.ShouldNotBeNull();
            BoundWorkloadCommandProof.Verify(request,proof,"recording-owner",Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),DateTimeOffset.UtcNow).ShouldBeTrue();
            (await signer.IssueAsync(request with { Command=command with { AggregateId="other-agent-stream" } },TestContext.Current.CancellationToken)).ShouldBeNull();
        }
        finally { File.Delete(file); }
    }
    private sealed record Permission(bool Allowed);
    private sealed record Admission(bool Exact,bool First);
}
