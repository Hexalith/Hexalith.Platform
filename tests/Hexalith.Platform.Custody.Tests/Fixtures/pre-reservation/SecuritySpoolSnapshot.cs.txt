namespace Hexalith.Platform.Custody;

/// <summary>One complete conditional private system spool namespace; observations/acknowledgements share one atomic CAS owner.</summary>
/// <param name="InstallationEpoch">Independently qualified restore epoch.</param>
/// <param name="Revision">Current monotonic conditional state.</param>
/// <param name="Records">Gap-free complete immutable observation inventory including acknowledged originals.</param>
public sealed record SecuritySpoolSnapshot(string InstallationEpoch, long Revision, IReadOnlyList<SecurityObservationRecord> Records)
{
    /// <summary>Current bounded carrier number; earlier acknowledged carriers are retained under immutable page keys.</summary>
    public long PageIndex { get; init; }
    /// <summary>Originals retained in prior acknowledged carriers.</summary>
    public long ArchivedObservedCount { get; init; }
    /// <summary>Acknowledgements retained in prior acknowledged carriers.</summary>
    public long ArchivedAcknowledgedCount { get; init; }
    /// <summary>Digest of the latest immutable archive carrier, including its predecessor link.</summary>
    public string? ArchiveHeadDigest { get; init; }
    /// <summary>Authenticated conditional drain scheduling transitions; this grants no acknowledgement or receiver capability.</summary>
    public long DrainRevision { get; init; }
    /// <summary>Exact last selected original sequence, retained durably for fair restart scheduling across routable source streams.</summary>
    public long DrainAfterSequence { get; init; }
}

