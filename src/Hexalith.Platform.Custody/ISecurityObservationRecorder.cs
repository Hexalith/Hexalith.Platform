namespace Hexalith.Platform.Custody;

/// <summary>Private dedicated recorder credential permits only exact SecurityEventRecorded on SecurityEventLog(RoutingTenantId,UtcDay).
/// Agents owns that aggregate/event; this port grants no public/human/Workflow/general target command authority.</summary>
public interface ISecurityObservationRecorder
{
    /// <summary>Authenticates caller before exact authoritative source lookup; uncertain/gapped/outage evidence is Unknown.</summary>
    Task<SecurityEventRecorderLookup> LookupAsync(SecurityObservationRecord record, CancellationToken cancellationToken = default);
    /// <summary>Appends only the original observation idempotently; command acceptance does not acknowledge the spool.</summary>
    Task AppendAsync(SecurityObservationRecord record, CancellationToken cancellationToken = default);
}
