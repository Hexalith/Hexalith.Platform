namespace Hexalith.Platform.Custody;

/// <summary>Private expected deletion key-family/public-anchor context; configuration alone cannot authorize issuance or consumption.</summary>
internal sealed record DeletionCapabilityTrustProfile(string Issuer, string Audience, string TenantId, string CapabilityKeyVersion,
    string PublicAnchorId, string PublicAnchorVersion);
