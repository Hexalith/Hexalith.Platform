using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Custody;

/// <summary>Independent exact guard terminal no-issue proof, never inferred from absence, timeout or stale local reads.</summary>
/// <param name="Payload">Original complete signed payload.</param><param name="SigningRequestId">Original deterministic signing request.</param>
/// <param name="DetachedJwsDigest">SHA-256 of the original retained public artifact.</param><param name="ProofId">Opaque durable guard terminal result.</param>
/// <param name="CurrentGuardRevision">Guard-returned revision for the successor's intended compare.</param><param name="CurrentHealthyKeyVersion">Current qualified per-tenant replacement or routine key.</param>
/// <param name="AuthorityRevision">Independent guard proof authority.</param><param name="ObservedAt">Fresh observation.</param><param name="ValidUntil">Exclusive current observation limit; capability itself has no expiry.</param>
public sealed record DeletionCapabilityNoIssueProof(DeletionBatchCapabilityV1 Payload, string SigningRequestId, string DetachedJwsDigest,
    string ProofId, long CurrentGuardRevision, string CurrentHealthyKeyVersion, string AuthorityRevision, DateTimeOffset ObservedAt, DateTimeOffset ValidUntil);
