namespace Hexalith.Platform.Custody;

/// <summary>Opaque exact key identity; no secret/private key material and no approval-signing purpose.</summary>
/// <param name="TenantId">Exact tenant; security-observation purpose is reserved system.</param>
/// <param name="Purpose">Closed exact purpose.</param>
/// <param name="KeyAlias">Exact independently provisioned alias.</param>
/// <param name="Version">Exact immutable backend version.</param>
/// <param name="ProviderTarget">Qualified exact backend target.</param>
/// <param name="AuthorityReference">Independently governed authority/profile reference.</param>
/// <param name="PublicAnchorId">Published public identity for verifier purposes.</param>
/// <param name="PublicAnchorVersion">Published public version.</param>
public sealed record PlatformKeyVersion(string TenantId, PlatformKeyPurpose Purpose, string KeyAlias, string Version, string ProviderTarget, string AuthorityReference, string? PublicAnchorId, string? PublicAnchorVersion);
