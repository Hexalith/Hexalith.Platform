using Hexalith.EventStore.PayloadProtection;
using Hexalith.Platform.Custody;
using Hexalith.EventStore.Contracts.Security;
using System.Text.Json.Serialization;

namespace Hexalith.Platform.Protection;

/// <summary>Independent source-writer enrollment. Complete target, root, digest, occurrence and writer identities are never inferred from a caller alias.</summary>
/// <param name="Binding">Original occurrence and source operation.</param><param name="RootIdentity">Complete custody identity.</param>
/// <param name="RootOperationId">Original shared-target root issue operation.</param><param name="ExpectedLifecycleRevision">Independent lifecycle revision.</param>
/// <param name="Digest">Existing exact tenant DigestKey reference.</param><param name="RegistryEpoch">Independently enrolled registry epoch.</param>
public sealed record ErasureRootOccurrenceEnrollment(InteractionProtectionBinding Binding, CustodyKeyObjectIdentity RootIdentity,
    string RootOperationId, long ExpectedLifecycleRevision, CustodyInteractionDigestReference Digest, string RegistryEpoch)
{
    /// <summary>Gets the independently enrolled native source receipt location. Null denies writer completion.</summary>
    public ErasureRootWriterReference? Writer { get; init; }
    /// <summary>Exact independent mixed-snapshot parent, entry path and technical skeleton. Omitted for existing single-target originals to preserve their serialized digests.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SnapshotEntrySourceBinding? SnapshotEntry { get; init; }
}
