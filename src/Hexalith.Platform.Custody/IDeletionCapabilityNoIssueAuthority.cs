using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Custody;

/// <summary>Authenticated online original guard no-issue outcome; required before terminal obsolescence or any successor signing attempt.</summary>
public interface IDeletionCapabilityNoIssueAuthority
{
    /// <summary>Returns only a durable irrevocable original no-issue result that prevents the old attempt from ever issuing.
    /// Unknown sign/issue, not-found or ambiguous state returns no proof. Revision/key selection is guard-owned and currently qualified.</summary>
    Task<DeletionCapabilityNoIssueProof?> ReadAsync(DeletionBatchCapabilityV1 payload, string signingRequestId,
        string detachedJwsDigest, CancellationToken cancellationToken = default);
}
