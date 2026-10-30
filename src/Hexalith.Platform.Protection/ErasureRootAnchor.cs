using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Protection;

/// <summary>Independently enrolled current anchor and immutable exact transition admissions/outcomes.</summary>
internal sealed record ErasureRootAnchor(long Revision, string Digest,
    SortedDictionary<string, AnchoredStateTransition> Admissions, SortedDictionary<string, AnchoredStateTransition> Completed)
{
    /// <summary>Exclusive admitted originals stored outside the bounded head; admission is not completion.</summary>
    public SortedDictionary<string, string> AdmissionArchives { get; init; } = new(StringComparer.Ordinal);
    /// <summary>Current committed immutable ancestry pointer, changed only with the tenant head CAS.</summary>
    public string? ArchiveHead { get; init; }
    /// <summary>Exact current shared original payload; bounded reconstruction never follows prior snapshots.</summary>
    public string? OriginalArchive { get; init; }
    /// <summary>Revision authenticated by the committed archive pointer.</summary>
    public long ArchiveRevision { get; init; }
}
