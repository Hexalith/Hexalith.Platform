namespace Hexalith.Platform.Custody;

/// <summary>Private independently authenticated replica/failure-model/restore and exact recorder/drain credential authority; unavailable defaults deny.</summary>
public interface IReplicatedSecuritySpoolAuthority
{
    /// <summary>Resolves the actual qualified independent component/epoch; no default name, target or replica proof.</summary>
    Task<ReplicatedSecuritySpoolTarget?> GetCurrentAsync(CancellationToken cancellationToken = default);
    /// <summary>Authenticates the dedicated current private caller and exact Observe/Lookup/Drain/Readiness method/intent; no human/Workflow/general dispatcher access.</summary>
    Task<bool> AuthorizeAsync(ReplicatedSecuritySpoolTarget target, string method, SecurityObservationIntent? intent, CancellationToken cancellationToken = default);
    /// <summary>Independently validates exact durable epoch/monotonic revision and captured state digest; missing/stale restored state cannot certify readiness.</summary>
    Task<bool> ValidateStateAsync(ReplicatedSecuritySpoolTarget target, long revision, string exactStateDigest, CancellationToken cancellationToken = default);
    /// <summary>Conditionally advances a durable independent antirollback anchor before component CAS; unknown/failed save closes availability until reconciliation.</summary>
    Task<bool> RecordRevisionAsync(ReplicatedSecuritySpoolTarget target, long expectedRevision, long nextRevision, string safeStateDigest, CancellationToken cancellationToken = default);
}
