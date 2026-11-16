using System.Text.Json.Serialization;

namespace Hexalith.Platform.Custody;

/// <summary>One complete conditional private system spool namespace; observations/acknowledgements share one atomic CAS owner.</summary>
/// <param name="InstallationEpoch">Independently qualified restore epoch.</param>
/// <param name="Revision">Current monotonic conditional state.</param>
/// <param name="Records">Gap-free complete immutable observation inventory including acknowledged originals.</param>
[JsonConverter(typeof(SecuritySpoolSnapshotJsonConverter))]
public sealed record SecuritySpoolSnapshot(string InstallationEpoch, long Revision, IReadOnlyList<SecurityObservationRecord> Records)
{
    private string? _reservedArchiveDigest;
    private long _archiveReservationCount;

    /// <summary>Retains the authenticated serialized layout across snapshots and record copies.</summary>
    internal bool UsesReservationLayout { get; init; } = true;

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
    /// <summary>Digest of the exact archive page this head has reserved, or null when no rollover is in progress.</summary>
    public string? ReservedArchiveDigest
    {
        get => _reservedArchiveDigest;
        init
        {
            _reservedArchiveDigest = value;
            if (value is not null) { UsesReservationLayout = true; }
        }
    }
    /// <summary>Completed archive reservations. Legacy snapshots leave this at zero; each finalized rollover increments it once.</summary>
    public long ArchiveReservationCount
    {
        get => _archiveReservationCount;
        init
        {
            _archiveReservationCount = value;
            if (value != 0) { UsesReservationLayout = true; }
        }
    }
}

