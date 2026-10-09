namespace Hexalith.Platform.Identity;

/// <summary>Operator-supplied trust facts obtained independently of the bootstrap and receipt issuer.</summary>
/// <param name="RootPublicKeySpki">Out-of-band DER SPKI of the P-256 bootstrap root.</param>
/// <param name="RootKeyFingerprint">Out-of-band SHA-256 fingerprint of that SPKI.</param>
/// <param name="BootstrapSha256">Out-of-band SHA-256 digest of the exact bootstrap payload.</param>
/// <param name="Revision">Expected immutable bootstrap revision.</param>
/// <param name="Issuer">Expected issuer.</param>
/// <param name="Audience">Expected consumer audience.</param>
/// <param name="RetrievalOrigin">Expected HTTPS receipt origin.</param>
/// <param name="StatusOrigin">Expected HTTPS status origin.</param>
/// <param name="ReceiptKeyFingerprint">Expected receipt key fingerprint.</param>
/// <param name="StatusKeyFingerprint">Expected status key fingerprint.</param>
/// <param name="PrincipalMappingRevision">Expected principal mapping revision.</param>
public sealed record P1BootstrapPin(
    byte[] RootPublicKeySpki, string RootKeyFingerprint, string BootstrapSha256, string Revision,
    string Issuer, string Audience, string RetrievalOrigin, string StatusOrigin,
    string ReceiptKeyFingerprint, string StatusKeyFingerprint, string PrincipalMappingRevision);
