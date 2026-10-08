using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Custody;

/// <summary>Bytes obtained from an independent exact persistence read, never the engine's seal return buffer.</summary>
/// <param name="Reference">Exact source reference.</param><param name="Bytes">Persisted bytes.</param><param name="Metadata">Persisted protection metadata.</param>
public sealed record Fr34PersistedCanary(Fr34CanaryReference Reference, byte[] Bytes, EventStorePayloadProtectionMetadata Metadata);
