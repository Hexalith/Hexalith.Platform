using System.Runtime.Serialization;

namespace Hexalith.Platform.Custody;

/// <summary>Current independently authenticated provider provenance and allowed operations; no signature or signing intent is created.</summary>
[DataContract]
public sealed record DeletionCapabilitySigningAvailability([property: DataMember] DeletionCapabilitySigningReadinessRequest Request,
    [property: DataMember] string ProviderIdentity, [property: DataMember] string AuthorityRevision, [property: DataMember] string[] Operations, [property: DataMember] DateTimeOffset ObservedAt, [property: DataMember] DateTimeOffset ValidUntil);
