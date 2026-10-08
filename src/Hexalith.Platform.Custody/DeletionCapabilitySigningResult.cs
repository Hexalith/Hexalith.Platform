namespace Hexalith.Platform.Custody;

/// <summary>Qualified backend result with its independently authenticated public-only verifier; no private key material exists in this carrier.</summary>
/// <param name="Outcome">Exact original immutable signing result.</param>
/// <param name="PublicAnchorSubjectPublicKeyInfo">Bounded public P256 SPKI; owned before validation.</param>
/// <param name="OriginalIssuanceReceiptId">Opaque original committed provider receipt; only independent historic verification permits unresolved recovery after revocation.</param>
public sealed record DeletionCapabilitySigningResult(DeletionCapabilitySigningOutcome Outcome, byte[]? PublicAnchorSubjectPublicKeyInfo = null, string? OriginalIssuanceReceiptId = null);
