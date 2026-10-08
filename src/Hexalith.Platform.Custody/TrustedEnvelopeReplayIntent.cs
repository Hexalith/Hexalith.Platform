namespace Hexalith.Platform.Custody;

/// <summary>Immutable content-free authenticated first-seen fields, keyed globally in reserved system by issuer and nonce.</summary>
/// <param name="Issuer">Exact authenticated issuer.</param><param name="DeliveryNonce">Original authenticated delivery nonce.</param>
/// <param name="TargetTenantId">Authenticated target tenant.</param><param name="LogicalCommandId">Authenticated immutable logical command.</param>
/// <param name="SigningKeyVersion">Original exact signing key.</param><param name="CanonicalAndTagDigest">SHA-256 of canonical delivery bytes plus authenticated tag.</param>
public sealed record TrustedEnvelopeReplayIntent(string Issuer, string DeliveryNonce, string TargetTenantId,
    string LogicalCommandId, string SigningKeyVersion, string CanonicalAndTagDigest);
