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
    /// <summary>Authenticated compromise source evidence, retained before any private registrar effect and independently reverified when consumed.</summary>
    public SortedDictionary<string, ErasureRootRevocationOriginal> RevocationOriginals { get; init; } = new(StringComparer.Ordinal);
    /// <summary>Exact receiving-owner guard attempts, including restrictive stale/blocked outcomes absent from the committed guard receipt list.</summary>
    public SortedDictionary<string, ErasureRootGuardAttempt> GuardAttempts { get; init; } = new(StringComparer.Ordinal);
    /// <summary>Exact registrar-verified successor evidence, retained before the independent protocol proof is consumed.</summary>
    public SortedDictionary<string, DeletionReattestationActivation> ActivationEvidence { get; init; } = new(StringComparer.Ordinal);
    /// <summary>Exact independently verified issued-but-blocked replacement evidence.</summary>
    public SortedDictionary<string, DeletionBlockedReplacementReconciliation> BlockedReplacementEvidence { get; init; } = new(StringComparer.Ordinal);
    /// <summary>Actual independently observed signing successor/no-issue bases, retained before returning them to the signer.</summary>
    public SortedDictionary<string, GovernanceSigningRecovery> SigningRecoveries { get; init; } = new(StringComparer.Ordinal);
    /// <summary>Private authenticated native pin-refusal originals, without destructive authority.</summary>
    public SortedDictionary<string, ErasureRootPinRefusalOriginal> PinRefusals { get; init; } = new(StringComparer.Ordinal);
    /// <summary>Single independent attempt per accepted target/profile/plan/anchors binding.</summary>
    public SortedDictionary<string, string> QualificationAttempts { get; init; } = new(StringComparer.Ordinal);
    /// <summary>Content-free private role operation identities and actual native results; observer credentials have no write path.</summary>
    public SortedDictionary<string, ProtectionQualificationOperation> QualificationOperations { get; init; } = new(StringComparer.Ordinal);
}
