using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Protection;

/// <summary>Exact encrypted persisted canary and occurrence; no plaintext or key bytes.</summary>
public sealed record Fr34StoredCanary(Fr34CanaryReference Reference, AggregateIdentity Owner, ProtectionTarget ProtectionTarget,
    PayloadProtectionOccurrenceContext Occurrence, PayloadProtectionResult Protected);

