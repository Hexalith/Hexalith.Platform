using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Protection;

/// <summary>Exact persisted snapshot/cache identity for real trusted and external readers.</summary>
public sealed record ProtectionQualificationNativeSnapshotRead(AggregateIdentity Owner, PayloadProtectionOccurrenceContext Parent, ProtectionQualificationNativeCell Source);
