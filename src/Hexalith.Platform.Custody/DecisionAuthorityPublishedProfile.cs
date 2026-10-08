namespace Hexalith.Platform.Custody;

/// <summary>Independently published verification-only candidate profile; export/custodian keys or self-supplied manifest anchors never qualify.</summary>
/// <param name="Issuer">Approved independent issuer.</param><param name="Audience">Exact verification audience.</param><param name="ProfileRevision">Independent current policy/trust revision.</param>
/// <param name="SigningKeyVersion">Exact retained issuer key.</param><param name="PublicAnchorId">Independently published anchor.</param><param name="PublicAnchorVersion">Original anchor version.</param>
/// <param name="SubjectPublicKeyInfo">Owned public P-256 verifier only.</param><param name="IsRevoked">Current immediate revocation.</param><param name="IsIndependentlyGoverned">Authenticated separate governance/custody qualification.</param>
/// <param name="KeyPurpose">Exactly DecisionApprovalVerificationOnly; no export/capability signer reuse.</param><param name="MaximumManifestLifetime">Independent policy's actual bound; no default approval policy.</param>
/// <param name="ValidFrom">Published UTC validity.</param><param name="ValidUntil">Exclusive current profile validity.</param><param name="ForbiddenApprovalActors">Recorder and platform-custodian stable actors, never approvers.</param>
public sealed record DecisionAuthorityPublishedProfile(string Issuer, string Audience, long ProfileRevision, string SigningKeyVersion, string PublicAnchorId,
    string PublicAnchorVersion, byte[] SubjectPublicKeyInfo, bool IsRevoked, bool IsIndependentlyGoverned, string KeyPurpose, TimeSpan MaximumManifestLifetime,
    DateTimeOffset ValidFrom, DateTimeOffset ValidUntil, IReadOnlyList<string> ForbiddenApprovalActors);
