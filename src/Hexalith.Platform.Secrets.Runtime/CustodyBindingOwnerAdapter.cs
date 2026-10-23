using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Secrets.Runtime;

// Concrete handback for the existing EventStore transition port. The independently installed
// authorizer supplies current exact envelopes; it may not derive genesis from local actor state.
// The public adapter never equates a protection-owner revision with a governance-guard revision.
internal sealed class CustodyBindingOwnerAdapter(CustodyBindingDaprInvoker invoker, AuthorityEnrollmentV1 enrollment,
    string enrolledPublicKeyPem, Func<string, AnchoredStateTransition?, CancellationToken, Task<CustodyBindingRequestV1?>> requests)
    : IAnchoredStateTransitionAuthority
{
    public Task<bool> AdmitTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => ExecuteAsync("admit-transition", transition, cancellationToken);
    public Task<bool> RecordTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => ExecuteAsync("record-transition", transition, cancellationToken);
    public Task<bool> RecoverTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => ExecuteAsync("recover-transition", transition, cancellationToken);
    public Task<bool> VerifyTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => ExecuteAsync("verify-transition", transition, cancellationToken);

    internal async Task<bool> ValidateOwnerAsync(string tenant, string ownerPurposeScope, long revision, string exactStateDigest, CancellationToken token)
    {
        if (revision < 0 || !CustodyBindingContract.Digest(exactStateDigest)) return false;
        try
        {
            var request = await requests("read-current", null, token);
            if (request is null || request.Envelope.TenantId != tenant || request.Envelope.OwnerPurposeScope != ownerPurposeScope) return false;
            var result = await invoker.InvokeAsync(request, TimeSpan.FromSeconds(30), token);
            var proof = result.Response?.Current;
            return result.State == CustodyBindingTransportState.ReceivedUnverified && proof is not null
                && proof.Authority == enrollment && proof.TenantId == tenant && proof.OwnerPurposeScope == ownerPurposeScope
                && proof.OwnerRevision == revision && proof.OwnerDigest == exactStateDigest
                && Verify(JsonSerializer.SerializeToUtf8Bytes(proof with { Signature = [] }, CustodyBindingContract.Json), proof.Signature);
        }
        catch (Exception) { return false; }
    }
    private async Task<bool> ExecuteAsync(string operation, AnchoredStateTransition transition, CancellationToken token)
    {
        try
        {
            var request = await requests(operation, transition, token);
            if (request is null || request.Envelope.Operation != operation || request.Envelope.Enrollment != enrollment
                || request.Transition is not { } exact || !JsonSerializer.SerializeToUtf8Bytes(exact, CustodyBindingContract.Json).AsSpan()
                    .SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(transition, CustodyBindingContract.Json))) return false;
            var result = await invoker.InvokeAsync(request, TimeSpan.FromSeconds(30), token);
            var proof = result.Response?.TransitionProof;
            bool admitted = operation == "admit-transition" && proof?.State == "Admitted";
            long expectedRevision = admitted ? transition.ExpectedRevision : transition.TargetRevision;
            string expectedDigest = admitted ? transition.PredecessorDigest : transition.TargetDigest;
            return result.State == CustodyBindingTransportState.ReceivedUnverified && result.Response?.TransitionVerified == true
                && proof is not null && (admitted || proof.State == "Recorded") && proof.Authority == enrollment && proof.RequestAttemptId == request.Envelope.RequestAttemptId
                && proof.Challenge == request.Envelope.Challenge && proof.Operation == operation && proof.ScopeId == transition.ScopeId
                && proof.OwnerRevision == expectedRevision && proof.OwnerDigest == expectedDigest
                && proof.TransitionDigest == CustodyBindingContract.Hash(JsonSerializer.SerializeToUtf8Bytes(transition, CustodyBindingContract.Json))
                && Verify(JsonSerializer.SerializeToUtf8Bytes(proof with { Signature = [] }, CustodyBindingContract.Json), proof.Signature);
        }
        catch (Exception) { return false; }
    }
    private bool Verify(byte[] bytes, byte[] signature)
    {
        using var key = ECDsa.Create(); key.ImportFromPem(enrolledPublicKeyPem);
        return key.KeySize == 256 && key.VerifyData(bytes, signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
    }
}
