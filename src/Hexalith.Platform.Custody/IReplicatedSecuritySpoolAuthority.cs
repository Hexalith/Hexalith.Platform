using Hexalith.EventStore.Contracts.Security;
namespace Hexalith.Platform.Custody;

/// <summary>Private independently authenticated replica/failure-model/restore and exact recorder/drain credential authority; unavailable defaults deny.</summary>
public interface IReplicatedSecuritySpoolAuthority : IAnchoredStateTransitionAuthority
{
    /// <summary>Resolves the actual qualified independent component/epoch; no default name, target or replica proof.</summary>
    Task<ReplicatedSecuritySpoolTarget?> GetCurrentAsync(CancellationToken cancellationToken = default);
    /// <summary>Authenticates the dedicated current private caller and exact Observe/Lookup/Drain/Readiness/RecoverOriginal method/intent; no human/Workflow/general dispatcher access.</summary>
    Task<bool> AuthorizeAsync(ReplicatedSecuritySpoolTarget target, string method, SecurityObservationIntent? intent, CancellationToken cancellationToken = default);
    /// <summary>Authenticates exact private retained-server-receipt lookup without accepting caller-authored tenant routing; absent credentials deny.</summary>
    Task<bool> AuthorizeOriginalLookupAsync(ReplicatedSecuritySpoolTarget target, SecurityObservationOriginalLookup lookup, CancellationToken cancellationToken = default) => Task.FromResult(false);
    /// <summary>Independently admits only this exact retained aged original and authoritative NotRecorded/Recorded proof under a separately enrolled fresh restricted recovery credential. Bind original routing/first-seen/day/sequence/intent and exact installation; no generic human/Workflow permission or renewal. Missing permission defaults deny.</summary>
    Task<SecurityObservationRecoveryGrant?> AuthorizeAgedOriginalRecoveryAsync(ReplicatedSecuritySpoolTarget target, SecurityObservationRecord original,
        SecurityEventRecorderLookup proof, CancellationToken cancellationToken = default) => Task.FromResult<SecurityObservationRecoveryGrant?>(null);
    /// <summary>Reconfirms independent original grant authenticity, unchanged current exact installation/recovery caller permission and authoritative current original recorder proof after every final await. Recorded reconciliation may follow an originally admitted NotRecorded proof; Unknown never authorizes an effect or acknowledgement. Omission denies.</summary>
    Task<bool> VerifyAgedOriginalRecoveryAsync(SecurityObservationRecoveryGrant grant, SecurityEventRecorderLookup currentProof,
        CancellationToken cancellationToken = default) => Task.FromResult(false);
    /// <summary>Independently validates exact durable epoch/monotonic revision and captured state digest; missing/stale restored state cannot certify readiness.</summary>
    Task<bool> ValidateStateAsync(ReplicatedSecuritySpoolTarget target, long revision, string exactStateDigest, CancellationToken cancellationToken = default);
    /// <summary>Deprecated compatibility-only legacy anchor hook; current recoverable actors do not invoke it.
    /// Qualified implementations must implement the mandatory inherited IAnchoredStateTransitionAuthority admitted-original admission/recovery
    /// and conditional exact transition journal, including independent staging ownership, current permission and final durable-state/anchor confirmation.
    /// Implementing this legacy hook alone never enables an actor; omitted inherited proof defaults deny.</summary>
    Task<bool> RecordRevisionAsync(ReplicatedSecuritySpoolTarget target, long expectedRevision, long nextRevision, string safeStateDigest, CancellationToken cancellationToken = default);
}
