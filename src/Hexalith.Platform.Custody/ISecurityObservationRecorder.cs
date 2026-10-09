namespace Hexalith.Platform.Custody;

/// <summary>Private dedicated recorder credential permits only exact SecurityEventRecorded on SecurityEventLog(RoutingTenantId,UtcDay).
/// Agents owns that aggregate/event; this port grants no public/human/Workflow/general target command authority.</summary>
public interface ISecurityObservationRecorder
{
    /// <summary>Authenticates caller before exact authoritative source lookup; uncertain/gapped/outage evidence is Unknown.</summary>
    Task<SecurityEventRecorderLookup> LookupAsync(SecurityObservationRecord record, CancellationToken cancellationToken = default);
    /// <summary>Recovers only the independently retained exact original under a separately authenticated current restricted aged-recovery grant. Never renew its first-seen/day/sequence/intent/internal request or borrow generic append authority. Authoritative NotRecorded permits idempotent original source recording; Recorded permits reconciliation only. Missing qualified binding defaults unavailable.</summary>
    Task RecoverOriginalAsync(SecurityObservationRecord original, SecurityObservationRecoveryGrant grant, CancellationToken cancellationToken = default)
        => Task.FromException(new InvalidOperationException("Exact original recovery recorder is unavailable."));
    /// <summary>Appends only the original observation idempotently; command acceptance does not acknowledge the spool.</summary>
    Task AppendAsync(SecurityObservationRecord record, CancellationToken cancellationToken = default);
}
