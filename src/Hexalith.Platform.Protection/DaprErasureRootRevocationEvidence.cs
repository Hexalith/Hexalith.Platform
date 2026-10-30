using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Fresh private registrar evidence. Synchronous compatibility methods remain restrictive; production consumption uses independently authenticated asynchronous reads.</summary>
public sealed class DaprErasureRootRevocationEvidence(ErasureRootTenantAuthority registrar) : IAsyncPublishedTrustRevocationEvidence
{
    /// <inheritdoc />
    public bool Matches(DeletionCapabilityRevocationEnvelope envelope) => false;
    /// <inheritdoc />
    public bool Pending(DeletionCapabilityRevocationEnvelope envelope) => true;
    /// <inheritdoc />
    public bool MatchesActivation(DeletionReattestationActivation activation) => false;
    /// <inheritdoc />
    public bool PendingActivation(DeletionReattestationActivation activation) => true;
    /// <inheritdoc />
    public Task<bool> VerifyRevocationEvidenceAsync(DeletionCapabilityRevocationEnvelope envelope, CancellationToken cancellationToken)
        => VerifyAsync(envelope.TenantId, envelope.EventIdentity, "VerifyRevocationEvidence", envelope, cancellationToken);
    /// <inheritdoc />
    public Task<bool> VerifyActivationEvidenceAsync(DeletionReattestationActivation activation, CancellationToken cancellationToken)
        => VerifyAsync(activation.Replacement.Capability.TenantId, activation.OperationId, "VerifyActivationEvidence", activation, cancellationToken);
    /// <inheritdoc />
    public Task<bool> VerifyBlockedReplacementEvidenceAsync(DeletionBlockedReplacementReconciliation request, CancellationToken cancellationToken)
        => VerifyAsync(request.Capability.TenantId, request.OperationId, "VerifyBlockedReplacementEvidence", request, cancellationToken);
    /// <inheritdoc />
    public Task<bool> VerifyCurrentBlockedReplacementAsync(DeletionBlockedReplacementReconciliation request, long guardRevision, string guardStateDigest, CancellationToken cancellationToken = default)
        => VerifyAsync(request.Capability.TenantId, request.OperationId, "VerifyCurrentBlockedReplacement", new { request, guardRevision, guardStateDigest }, cancellationToken);
    private async Task<bool> VerifyAsync(string tenant, string identity, string operation, object original, CancellationToken token)
    {
        string digest = ErasureRootCustody.Hash(original);
        var proof = await registrar.InvokeAsync<ErasureRootPermissionEvidence>("erc-registrar-proof", new { tenantId = tenant, identity, operation, requestDigest = digest, original }, token).ConfigureAwait(false);
        return proof is { Permitted: true } && proof.TenantId == tenant && proof.Identity == identity && proof.Operation == operation
            && proof.RequestDigest == digest && !string.IsNullOrWhiteSpace(proof.ReceiptId);
    }
}
