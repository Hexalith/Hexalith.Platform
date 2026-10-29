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
    ErasureRootManifest? Pending = null)
{
    /// <summary>Exact occurrence originals and once-only source leases, independently surviving EventStore restore.</summary>
    public SortedDictionary<string, ErasureRootOccurrence> Occurrences { get; init; } = new(StringComparer.Ordinal);
    /// <summary>Independent source-owner enrollment, retained before a caller can request a binding.</summary>
    public SortedDictionary<string, ErasureRootOccurrenceEnrollment> SourceOriginals { get; init; } = new(StringComparer.Ordinal);
    /// <summary>Original content-free source reservation fingerprints, once bound and never overwritten.</summary>
    public SortedDictionary<string, string> SourceReservations { get; init; } = new(StringComparer.Ordinal);
    /// <summary>Content-free exact successful native AEAD and HMAC verification receipts; retries still require current native authorization.</summary>
    public SortedDictionary<string, string> SealedOriginals { get; init; } = new(StringComparer.Ordinal);
    /// <summary>Single independent attempt per accepted target/profile/plan/anchors binding.</summary>
    public SortedDictionary<string, string> QualificationAttempts { get; init; } = new(StringComparer.Ordinal);
    /// <summary>Content-free private role operation identities and actual native results; observer credentials have no write path.</summary>
    public SortedDictionary<string, ProtectionQualificationOperation> QualificationOperations { get; init; } = new(StringComparer.Ordinal);
}
