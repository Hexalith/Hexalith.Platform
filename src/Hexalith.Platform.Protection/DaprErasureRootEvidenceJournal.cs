using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Independent actual admission/registrar/activation evidence verification for the existing published-trust proof protocol; it never advances an owner anchor.</summary>
public sealed class DaprErasureRootEvidenceJournal(IndependentDaprAuthority evidence) : IAnchoredStateTransitionAuthority
{
    /// <inheritdoc />
    public async Task<bool> VerifyTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
    {
        string digest = ErasureRootCustody.Hash(transition);
        var proof = await evidence.InvokeAsync<ErasureRootPermissionEvidence>("erc-current-protocol-evidence", new { transition, requestDigest = digest }, cancellationToken).ConfigureAwait(false);
        return proof is { Permitted: true } && proof.Identity == transition.ScopeId && proof.Operation == "VerifyProtocolEvidence"
            && proof.RequestDigest == digest && !string.IsNullOrWhiteSpace(proof.ReceiptId);
    }
}
