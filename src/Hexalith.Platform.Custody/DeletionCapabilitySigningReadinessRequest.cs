using System.Runtime.Serialization;

namespace Hexalith.Platform.Custody;

/// <summary>Exact installed tenant/key/trust and qualification binding for a read-only signing availability query.</summary>
[DataContract]
public sealed record DeletionCapabilitySigningReadinessRequest([property: DataMember] string TenantId, [property: DataMember] string KeyFamily, [property: DataMember] string KeyVersion,
    [property: DataMember] string Issuer, [property: DataMember] string Audience, [property: DataMember] string PublicAnchorId, [property: DataMember] string PublicAnchorVersion, [property: DataMember] string SubjectPublicKeyInfoSha256,
    [property: DataMember] long TrustProfileRevision, [property: DataMember] string InstallationBinding, [property: DataMember] string ProviderIdentity);
