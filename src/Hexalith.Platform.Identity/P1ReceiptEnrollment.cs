namespace Hexalith.Platform.Identity;

/// <summary>Offline copy of an independently authenticated issuer enrollment; construction does not enroll it.</summary>
/// <param name="Issuer">Exact receipt and status issuer.</param>
/// <param name="RetrievalOrigin">Exact HTTPS retrieval origin, without trailing slash.</param>
/// <param name="StatusOrigin">Exact HTTPS status origin, without trailing slash.</param>
/// <param name="Audience">Exact consumer audience.</param>
/// <param name="TrustRevision">Immutable authenticated trust revision.</param>
/// <param name="ReceiptPublicKeySpki">DER SPKI of the receipt-only P-256 public key.</param>
/// <param name="ReceiptKeyFingerprint">SHA-256 of receipt SPKI.</param>
/// <param name="StatusPublicKeySpki">DER SPKI of the separate status-only P-256 public key.</param>
/// <param name="StatusKeyFingerprint">SHA-256 of status SPKI.</param>
/// <param name="EffectiveAtUtc">Enrollment start.</param>
/// <param name="ExpiresAtUtc">Exclusive enrollment expiry.</param>
public sealed record P1ReceiptEnrollment(
    string Issuer, string RetrievalOrigin, string StatusOrigin, string Audience, string TrustRevision,
    byte[] ReceiptPublicKeySpki, string ReceiptKeyFingerprint, byte[] StatusPublicKeySpki,
    string StatusKeyFingerprint, DateTimeOffset EffectiveAtUtc, DateTimeOffset ExpiresAtUtc);
