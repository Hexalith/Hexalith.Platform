namespace Hexalith.Platform.Custody.Component;

/// <summary>Exact admitted manifest. Callers supply the order; this record does not sort it.</summary>
/// <param name="ReservationId">Original protection reservation.</param>
/// <param name="BatchId">Immutable batch.</param>
/// <param name="ManifestDigest">Digest of the supplied order.</param>
/// <param name="Targets">Supplied same-tenant order.</param>
internal sealed record CustodyOriginalManifest(string ReservationId, string BatchId, string ManifestDigest, IReadOnlyList<CustodyKeyObjectIdentity> Targets);
