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
    [System.Runtime.InteropServices.DllImport("libc",EntryPoint="mkfifo",SetLastError=true)] private static extern int Mkfifo(string path,int mode);
    [Fact]
    public async Task ActualReceiverAndProofSourceRequireExactRetainedPayloadAndCurrentWorkload()
    {
        using var authority = new ReceivingAuthorityFixture("private-replay");
        using var key=RSA.Create(2048); string file=Path.GetTempFileName();
        await File.WriteAllTextAsync(file,Convert.ToBase64String(key.ExportPkcs8PrivateKey()),TestContext.Current.CancellationToken);
        authority.State=authority.State with { Callers=authority.State.Callers.Select(value=>value with { CommandProofPublicKeySpkiBase64=Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()) }).ToArray() };
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
            File.Delete(file);Mkfifo(file,0x180).ShouldBe(0);
            (await proofSource.IssueAsync(request,TestContext.Current.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(1),TestContext.Current.CancellationToken)).ShouldBeNull();
            File.Delete(file);await File.WriteAllTextAsync(file,Convert.ToBase64String(key.ExportPkcs8PrivateKey()),TestContext.Current.CancellationToken);
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
        authority.State=authority.State with { Callers=authority.State.Callers.Select(value=>value with { CommandProofPublicKeySpkiBase64=Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()) }).ToArray() };
        try
        {
            DateTimeOffset now=DateTimeOffset.UtcNow;
            var intent=new TrustedEnvelopeReplayIntent("issuer","nonce","tenant-a","logical","v1",new string('a',64));
            var payload=JsonSerializer.SerializeToElement(new { issuer=intent.Issuer,deliveryNonce=intent.DeliveryNonce,targetTenantId=intent.TargetTenantId,logicalCommandId=intent.LogicalCommandId,signingKeyVersion=intent.SigningKeyVersion,canonicalAndTagDigest=intent.CanonicalAndTagDigest,firstSeenAt=now,retainUntil=now.AddSeconds(604800) });
            string aggregate=Convert.ToHexStringLower(SHA256.HashData(PlatformCanonicalBytes.Identity(["trusted-envelope-replay","issuer","nonce"])));
            string originalId=PrivateRecordingIdentity.StableUlid("replay",new Hexalith.EventStore.Contracts.Identity.AggregateIdentity("system","trusted-envelope-replay",aggregate).ActorId);
            await authority.Authority.InvokeAsync<Admission>("qualification-begin-original",new { role="private-replay",requestId=originalId,original=payload },TestContext.Current.CancellationToken);
            var firstCommand=new SubmitCommandRequest(originalId,"system","trusted-envelope-replay",aggregate,"Hexalith.Agents.Contracts.TrustedEnvelopeReplay.RegisterTrustedEnvelopeFirstSeen",payload,IdempotencyKey:originalId);
            var firstRequest=new BoundWorkloadCommandRequest(firstCommand,"private-worker");
            var firstProof=await new RestrictedPrivateCommandProofSource("recording-owner","private-worker",file,authority.Authority,TimeProvider.System).IssueAsync(firstRequest,TestContext.Current.CancellationToken);
            (await authority.Authority.InvokeAsync<Permission>("recording-dispatch-admit",new { role="private-replay",requestId=originalId,original=payload,boundRequest=firstRequest,workloadProof=firstProof },TestContext.Current.CancellationToken))!.Allowed.ShouldBeTrue();
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
            foreach(var substituted in new[]{command with { Tenant="foreign" },command with { Domain="foreign" },command with { AggregateId="foreign" },command with { CommandType="foreign" },command with { CorrelationId="foreign" },command with { Extensions=new() { ["changed"]="value" } }})
                (await authority.Authority.InvokeAsync<Permission>("recording-replay-recovery-dispatch-admit",new { command=substituted,proof,boundRequest=request,workloadProof=signed },TestContext.Current.CancellationToken))!.Allowed.ShouldBeFalse();
            (await authority.Authority.InvokeAsync<Permission>("recording-replay-recovery-dispatch-admit",new { command,proof,boundRequest=request,workloadProof=signed },TestContext.Current.CancellationToken))!.Allowed.ShouldBeTrue();
            (await signer.IssueReplayRecoveryAsync(request,proof,TestContext.Current.CancellationToken)).ShouldBeNull();
        }
        finally { File.Delete(file); }
    }
    [Fact]
    public async Task LegacyRecorderRequiresExactLegacyStreamScopeAndNeverUsesGeneralAgentsGrant()
    {
        using var authority=new ReceivingAuthorityFixture("private-security-observation");using var key=RSA.Create(2048);string file=Path.GetTempFileName();
        await File.WriteAllTextAsync(file,Convert.ToBase64String(key.ExportPkcs8PrivateKey()),TestContext.Current.CancellationToken);
        authority.State=authority.State with { Callers=authority.State.Callers.Select(value=>value with { CommandProofPublicKeySpkiBase64=Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()) }).ToArray() };
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
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task WorkloadProofAndDispatchShareRevocationOrderingAndCannotSubstitutePrivateStoreCaller(bool revokeDuringDispatch)
    {
        using var authority=new ReceivingAuthorityFixture("private-replay");using var key=RSA.Create(2048);string file=Path.GetTempFileName();
        await File.WriteAllTextAsync(file,Convert.ToBase64String(key.ExportPkcs8PrivateKey()),TestContext.Current.CancellationToken);
        try
        {
            var storageCaller=authority.State.Callers[0] with { Methods=authority.State.Callers[0].Methods.Where(value=>!value.StartsWith("recording-source:",StringComparison.Ordinal)).ToArray() };
            var workload=new IndependentCallerGrant(new string('e',64),"exact-workload","workload-principal","workload-source",["recording-source:trusted-envelope-replay"],DateTimeOffset.UtcNow.AddHours(1),false,Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()));
            authority.State=authority.State with { Callers=[storageCaller,workload] };
            DateTimeOffset now=DateTimeOffset.UtcNow;var payload=JsonSerializer.SerializeToElement(new { issuer="issuer",deliveryNonce="nonce",targetTenantId="tenant-a",logicalCommandId="logical",signingKeyVersion="v1",canonicalAndTagDigest=new string('a',64),firstSeenAt=now,retainUntil=now.AddSeconds(604800) });
            string aggregate=Convert.ToHexStringLower(SHA256.HashData(PlatformCanonicalBytes.Identity(["trusted-envelope-replay","issuer","nonce"])));
            var command=new SubmitCommandRequest("01ARZ3NDEKTSV4RRFFQ69G5FAV","system","trusted-envelope-replay",aggregate,"Hexalith.Agents.Contracts.TrustedEnvelopeReplay.RegisterTrustedEnvelopeFirstSeen",payload,IdempotencyKey:"01ARZ3NDEKTSV4RRFFQ69G5FAV");
            var request=new BoundWorkloadCommandRequest(command,"workload-principal");
            await authority.Authority.InvokeAsync<Admission>("qualification-begin-original",new { role="private-replay",requestId=command.MessageId,original=payload },TestContext.Current.CancellationToken);
            var source=new RestrictedPrivateCommandProofSource("exact-workload","workload-principal",file,authority.Authority,TimeProvider.System);
            var proof=await source.IssueAsync(request,TestContext.Current.CancellationToken);proof.ShouldNotBeNull();
            object Dispatch(BoundWorkloadCommandRequest bound,string? signed)=>new { role="private-replay",requestId=command.MessageId,original=payload,boundRequest=bound,workloadProof=signed };
            (await authority.Authority.InvokeAsync<Permission>("recording-dispatch-admit",Dispatch(request with { PrincipalId="private-worker" },proof),TestContext.Current.CancellationToken))!.Allowed.ShouldBeFalse();
            string substitute=BoundWorkloadCommandProof.Issue(request,"recording-owner",Convert.ToBase64String(key.ExportPkcs8PrivateKey()),now);
            (await authority.Authority.InvokeAsync<Permission>("recording-dispatch-admit",Dispatch(request,substitute),TestContext.Current.CancellationToken))!.Allowed.ShouldBeFalse();
            if(revokeDuringDispatch)
            {
                authority.BeforeConditionalSave=()=> { authority.BeforeConditionalSave=null;authority.RevokeWorkload("exact-workload"); };
                (await authority.Authority.InvokeAsync<Permission>("recording-dispatch-admit",Dispatch(request,proof),TestContext.Current.CancellationToken)).ShouldBeNull();
            }
            else
            {
                (await authority.Authority.InvokeAsync<Permission>("recording-dispatch-admit",Dispatch(request,proof),TestContext.Current.CancellationToken))!.Allowed.ShouldBeTrue();
                authority.RevokeWorkload("exact-workload");
            }
            var original=await authority.Authority.InvokeAsync<IndependentOriginal>("qualification-lookup-original",new { role="private-replay",requestId=command.MessageId },TestContext.Current.CancellationToken);
            original!.SubmissionState.ShouldBe(revokeDuringDispatch?"Unsent":"Dispatched");
            if(!revokeDuringDispatch) { original.DispatchBinding!.Caller.ShouldBe("exact-workload");original.DispatchBinding.Principal.ShouldBe("workload-principal"); }
            (await source.IssueAsync(request,TestContext.Current.CancellationToken)).ShouldBeNull();
            (await authority.Authority.InvokeAsync<Permission>("recording-dispatch-admit",Dispatch(request,proof),TestContext.Current.CancellationToken))!.Allowed.ShouldBeFalse();
        }
        finally { File.Delete(file); }
    }
    [Theory]
    [InlineData("expiry")][InlineData("revocation")]
    public async Task PublicationCannotAdmitAProofWhichExpiredOrWasRevokedWhileArchiveWasAwaited(string change)
    {
        var clock=new DispatchClock();var archive=new DispatchArchive();using var authority=new ReceivingAuthorityFixture("private-replay",originalArchive:archive,clock:clock);
        var command=new SubmitCommandRequest("01ARZ3NDEKTSV4RRFFQ69G5FAV","system","trusted-envelope-replay","aggregate","command",JsonSerializer.SerializeToElement(new { exact="payload" }),IdempotencyKey:"01ARZ3NDEKTSV4RRFFQ69G5FAV");
        var request=new BoundWorkloadCommandRequest(command,"private-worker");
        await authority.Authority.InvokeAsync<Admission>("qualification-begin-original",new { role="private-replay",requestId=command.MessageId,original=command.Payload },TestContext.Current.CancellationToken);
        string proof=BoundWorkloadCommandProof.Issue(request,"recording-owner",Convert.ToBase64String(authority.ProofKey.ExportPkcs8PrivateKey()),clock.GetUtcNow());
        archive.Published=()=> { archive.Published=null;if(change=="expiry") clock.Now=clock.Now.AddMinutes(2);else authority.RevokeWorkload("recording-owner"); };
        (await authority.Authority.InvokeAsync<Permission>("recording-dispatch-admit",new { role="private-replay",requestId=command.MessageId,original=command.Payload,boundRequest=request,workloadProof=proof },TestContext.Current.CancellationToken)).ShouldBeNull();
        if(change=="revocation") authority.State=authority.State with { Callers=authority.State.Callers.Select(value=>value with { Revoked=false }).ToArray() };
        var retained=await authority.Authority.InvokeAsync<IndependentOriginal>("qualification-lookup-original",new { role="private-replay",requestId=command.MessageId },TestContext.Current.CancellationToken);
        retained!.SubmissionState.ShouldBe("Unsent");
    }
    private sealed class DispatchClock:TimeProvider { internal DateTimeOffset Now=DateTimeOffset.UtcNow;public override DateTimeOffset GetUtcNow()=>Now; }
    private sealed class DispatchArchive:IIndependentContentArchive
    {
        private readonly Dictionary<string,byte[]> _values=[];internal Action? Published;
        public async Task<string> PublishAsync(ReadOnlyMemory<byte> bytes,CancellationToken token)
        { await Task.Yield();string hash=Convert.ToHexString(SHA256.HashData(bytes.Span));_values.TryAdd(hash,bytes.ToArray());Published?.Invoke();return hash; }
        public Task<byte[]> ReadAsync(string hash,CancellationToken token)=>Task.FromResult(_values[hash].ToArray());
    }
    private sealed record Permission(bool Allowed);
    private sealed record Admission(bool Exact,bool First);
}
