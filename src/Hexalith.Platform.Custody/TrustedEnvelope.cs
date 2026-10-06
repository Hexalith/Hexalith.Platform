namespace Hexalith.Platform.Custody;

/// <summary>A signed delivery; verification alone does not register replay or authorize dispatch.</summary>
/// <param name="Identity">Logical identity.</param>
/// <param name="IssuedAt">Inclusive issuance time.</param>
/// <param name="ExpiresAt">Exclusive expiry.</param>
/// <param name="DeliveryNonce">Per-delivery nonce.</param>
/// <param name="SigningKeyVersion">Exact envelope key version.</param>
/// <param name="Tag">Hexadecimal HMAC-SHA-256 authentication.</param>
public sealed record TrustedEnvelope(TrustedEnvelopeIdentity Identity, DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt, string DeliveryNonce, string SigningKeyVersion, string Tag);
