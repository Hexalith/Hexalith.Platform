namespace Hexalith.Platform.Custody;

/// <summary>Immutable first durable observation and exact independently confirmed recorder receipt.</summary>
/// <param name="Intent">Original safe immutable identity/facts.</param>
/// <param name="ObservedAt">Original spool-assigned UTC first-seen time, never recomputed on retry.</param>
/// <param name="UtcDay">Exact SecurityEventLog day partition.</param>
/// <param name="Sequence">Complete spool-owner insertion sequence.</param>
/// <param name="Receipt">Only exact authoritative EventStore proof may acknowledge.</param>
public sealed record SecurityObservationRecord(SecurityObservationIntent Intent, DateTimeOffset ObservedAt, string UtcDay, long Sequence, SecurityEventRecordReceipt? Receipt)
{
    /// <inheritdoc/>
    public override string ToString() => nameof(SecurityObservationRecord);
}
