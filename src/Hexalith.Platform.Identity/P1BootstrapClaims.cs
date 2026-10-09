namespace Hexalith.Platform.Identity;

/// <summary>Exact issuer enrollment claims signed by an independently pinned P1 root.</summary>
/// <param name="Revision">Immutable bootstrap revision.</param>
/// <param name="Issuer">Exact receipt and status issuer.</param>
/// <param name="Audience">Exact consumer audience.</param>
/// <param name="RetrievalOrigin">Exact HTTPS receipt origin.</param>
/// <param name="StatusOrigin">Exact HTTPS status origin.</param>
/// <param name="PrincipalMappingRevision">Immutable credential-to-principal mapping revision.</param>
/// <param name="ReceiptPublicKeySpki">DER SPKI of the receipt-only P-256 key.</param>
/// <param name="ReceiptKeyFingerprint">SHA-256 of the receipt SPKI.</param>
/// <param name="StatusPublicKeySpki">DER SPKI of the separate status-only P-256 key.</param>
/// <param name="StatusKeyFingerprint">SHA-256 of the status SPKI.</param>
/// <param name="EffectiveAtUtc">Inclusive UTC start.</param>
/// <param name="ExpiresAtUtc">Exclusive UTC expiry.</param>
public sealed record P1BootstrapClaims(
    string Revision, string Issuer, string Audience, string RetrievalOrigin, string StatusOrigin,
    string PrincipalMappingRevision, byte[] ReceiptPublicKeySpki, string ReceiptKeyFingerprint,
    byte[] StatusPublicKeySpki, string StatusKeyFingerprint, DateTimeOffset EffectiveAtUtc,
    DateTimeOffset ExpiresAtUtc);
