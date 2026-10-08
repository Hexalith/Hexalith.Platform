namespace Hexalith.Platform.Custody;

/// <summary>Independently published current tenant deletion-only verifier; never derived from a signer result.</summary>
/// <param name="TenantId">Exact target tenant.</param>
/// <param name="KeyFamily">Exact deletion signing purpose.</param>
/// <param name="CapabilityKeyVersion">Approved version.</param>
/// <param name="Issuer">Approved issuer.</param>
/// <param name="Audience">Approved protection owner.</param>
/// <param name="PublicAnchorId">Published anchor identity.</param>
/// <param name="PublicAnchorVersion">Published anchor version.</param>
/// <param name="SubjectPublicKeyInfo">Published P-256 public key bytes.</param>
/// <param name="TrustProfileRevision">Independently recorded current profile revision.</param>
/// <param name="IsCurrentNonRevoked">Whether issuance under this version remains authorized.</param>
public sealed record DeletionCapabilityPublishedTrust(string TenantId, string KeyFamily, string CapabilityKeyVersion,
    string Issuer, string Audience, string PublicAnchorId, string PublicAnchorVersion, byte[] SubjectPublicKeyInfo,
    long TrustProfileRevision, bool IsCurrentNonRevoked);
