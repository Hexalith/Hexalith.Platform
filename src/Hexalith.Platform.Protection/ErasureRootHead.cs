using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Protection;

/// <summary>One CAS-controlled tenant head serializes holds, root issue, manifests, erasure identity index and nonrollback anchors.</summary>
internal sealed record ErasureRootHead(string TenantId, string StoreId, string EnrollmentId, int Epoch,
    SortedDictionary<string, ErasureRootTarget> Targets,
    SortedDictionary<string, ErasureRootErasure> Erased,
    SortedDictionary<string, ErasureRootManifest> Manifests,
    SortedDictionary<string, CustodyKeyLifecycleOutcome> LifecycleOutcomes,
    SortedDictionary<string, ErasureRootAnchor> Anchors,
    ErasureRootManifest? Pending = null);
