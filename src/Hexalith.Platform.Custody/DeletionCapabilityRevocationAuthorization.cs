using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Custody;

/// <summary>Independent current authenticated source-envelope basis; no signer or Agents principal is carried.</summary>
/// <param name="Envelope">Exact cryptographically authenticated revocation.</param>
/// <param name="AuthorityRevision">Current independent source/profile/revocation revision.</param>
/// <param name="ObservedAt">Authentication observation.</param>
/// <param name="ValidUntil">Exclusive current authority boundary.</param>
public sealed record DeletionCapabilityRevocationAuthorization(DeletionCapabilityRevocationEnvelope Envelope,
    string AuthorityRevision, DateTimeOffset ObservedAt, DateTimeOffset ValidUntil);
