using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Custody;

/// <summary>Independent historic exact signing-outcome receipt verification, distinct from current issue/consume authorization.
/// A revoked-key signature or provider self-asserted timestamp is insufficient. Missing historical proof keeps the original request Unknown.</summary>
public interface IDeletionCapabilityOriginalSigningReceiptAuthority
{
    /// <summary>Independently authenticates original committed issuance before compromise, exact request/payload/signature digest/public anchor/provider receipt,
    /// against the retained published verifier and current revocation history. It cannot authorize a new signature or consumption.</summary>
    Task<bool> VerifyOriginalIssuanceAsync(DeletionBatchCapabilityV1 payload, string signingRequestId,
        DeletionCapabilitySigningResult original, DeletionCapabilityPublishedTrust retainedTrust, CancellationToken cancellationToken = default);
}
