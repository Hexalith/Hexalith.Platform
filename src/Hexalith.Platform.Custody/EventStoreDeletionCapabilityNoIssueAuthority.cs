using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Custody;

/// <summary>Actual adapter from the original backend-CAS terminal guard result to the signer's exact no-issue contract; no absence/timeout inference.</summary>
/// <param name="guard">Private typed qualified EventStore owner.</param>
public sealed class EventStoreDeletionCapabilityNoIssueAuthority(IGovernanceScopeGuard guard) : IDeletionCapabilityNoIssueAuthority
{
    /// <inheritdoc/>
    public async Task<DeletionCapabilityNoIssueProof?> ReadAsync(DeletionBatchCapabilityV1 payload, string signingRequestId, string detachedJwsDigest, CancellationToken cancellationToken = default)
    {
        var proof = await guard.ReadNoIssueAsync(payload, signingRequestId, detachedJwsDigest, cancellationToken).ConfigureAwait(false);
        return proof is null ? null : new(proof.Payload, proof.SigningRequestId, proof.DetachedJwsDigest, proof.NoIssueReceiptId,
            proof.CurrentGuardRevision, proof.CurrentHealthyKeyVersion, proof.AuthorityRevision, proof.ObservedAt, proof.ValidUntil);
    }
}
