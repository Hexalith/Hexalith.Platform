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
    /// <summary>Committed immutable completed-manifest originals. Prewritten archives without these pointers have no authority.</summary>
    public SortedDictionary<string, string> ManifestArchives { get; init; } = new(StringComparer.Ordinal);
    /// <summary>Independent qualification call/effect bookkeeping, excluded from the prohibited custody-effect fingerprint.</summary>
    public string? QualificationEvidenceIndexRoot { get; init; }
    /// <summary>Authenticated persistent occurrence index; no orphan archive authorizes an occurrence.</summary>
    public string? OccurrenceIndexRoot { get; init; }
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
    /// <summary>Tenant-CAS pointers to exact immutable guard attempt admissions.</summary>
    public SortedDictionary<string, string> GuardAdmissionArchives { get; init; } = new(StringComparer.Ordinal);
    /// <summary>Exact signing request to guard-intent pointer for bounded no-issue lookup; no archive prewrite creates this index.</summary>
    public SortedDictionary<string, string> GuardSigningIntents { get; init; } = new(StringComparer.Ordinal);
    /// <summary>Tenant-CAS pointers to exact immutable guard outcomes, including genuine refusals.</summary>
    public SortedDictionary<string, string> GuardAttemptArchives { get; init; } = new(StringComparer.Ordinal);
    /// <summary>Exact receiving-owner guard admissions; neither absence nor a pending admission establishes a refusal or completion.</summary>
    public SortedDictionary<string, ErasureRootGuardAdmission> GuardAdmissions { get; init; } = new(StringComparer.Ordinal);
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
    /// <summary>Exact authenticated source/engine producer originals; observer processes never label source artifacts with their own assembly.</summary>
    public SortedDictionary<string, ProtectionQualificationProducerOriginal> QualificationProducerOriginals { get; init; } = new(StringComparer.Ordinal);
    /// <summary>Single independent attempt per accepted target/profile/plan/anchors binding.</summary>
    public SortedDictionary<string, string> QualificationAttempts { get; init; } = new(StringComparer.Ordinal);
    /// <summary>Tenant-CAS pointers to exact conditional hold-refusal originals.</summary>
    public SortedDictionary<string, string> ConditionalHoldOriginalArchives { get; init; } = new(StringComparer.Ordinal);
    /// <summary>Exact conditional hold-refusal originals bound to admitted role calls; no deletion-owner registration or reservation is created.</summary>
    public SortedDictionary<string, System.Text.Json.Nodes.JsonObject> ConditionalHoldOriginals { get; init; } = new(StringComparer.Ordinal);
    /// <summary>Exact source-owned canonical seed producers; generic encrypted content or an arbitrary source cell cannot become a canary seed.</summary>
    public SortedDictionary<string, ProtectionQualificationSeedOriginal> QualificationSeedOriginals { get; init; } = new(StringComparer.Ordinal);
    /// <summary>Content-free private role operation identities and actual native results; observer credentials have no write path.</summary>
    public SortedDictionary<string, ProtectionQualificationOperation> QualificationOperations { get; init; } = new(StringComparer.Ordinal);
}
