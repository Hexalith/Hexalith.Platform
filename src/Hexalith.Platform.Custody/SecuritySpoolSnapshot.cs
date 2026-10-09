namespace Hexalith.Platform.Custody;

/// <summary>One complete conditional private system spool namespace; observations/acknowledgements share one atomic CAS owner.</summary>
/// <param name="InstallationEpoch">Independently qualified restore epoch.</param>
/// <param name="Revision">Current monotonic conditional state.</param>
/// <param name="Records">Gap-free complete immutable observation inventory including acknowledged originals.</param>
public sealed record SecuritySpoolSnapshot(string InstallationEpoch, long Revision, IReadOnlyList<SecurityObservationRecord> Records)
{
    /// <summary>Authenticated conditional drain scheduling transitions; this grants no acknowledgement or receiver capability.</summary>
    public long DrainRevision { get; init; }
    /// <summary>Exact last selected original sequence, retained durably for fair restart scheduling across routable source streams.</summary>
    public long DrainAfterSequence { get; init; }
}

