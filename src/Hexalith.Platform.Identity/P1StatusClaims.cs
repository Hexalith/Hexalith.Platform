namespace Hexalith.Platform.Identity;

/// <summary>Nonce-bound current status of one receipt and every authority it depends on.</summary>
/// <param name="Issuer">Exact enrolled status issuer.</param>
/// <param name="TrustRevision">Exact independently enrolled trust revision.</param>
/// <param name="ReceiptId">Receipt queried.</param>
/// <param name="KeyFingerprint">Receipt signing key queried.</param>
/// <param name="GrantId">Grant queried.</param>
/// <param name="SessionId">Session queried.</param>
/// <param name="PolicyRevision">Policy queried.</param>
/// <param name="ReceiptState">Current receipt state.</param>
/// <param name="KeyState">Current signing-key state.</param>
/// <param name="GrantState">Current grant state.</param>
/// <param name="PolicyState">Current policy state.</param>
/// <param name="SessionState">Current session state.</param>
/// <param name="Nonce">Lowercase hexadecimal 32-byte caller challenge.</param>
/// <param name="ObservedAtUtc">Authenticated status observation instant.</param>
/// <param name="ExpiresAtUtc">Exclusive status expiry.</param>
public sealed record P1StatusClaims(
    string Issuer, string TrustRevision, string ReceiptId, string KeyFingerprint, string GrantId,
    string SessionId, string PolicyRevision, string ReceiptState, string KeyState, string GrantState, string PolicyState,
    string SessionState, string Nonce, DateTimeOffset ObservedAtUtc, DateTimeOffset ExpiresAtUtc);
