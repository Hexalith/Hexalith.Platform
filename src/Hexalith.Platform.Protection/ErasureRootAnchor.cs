using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Protection;

/// <summary>Independently enrolled current anchor and immutable exact transition admissions/outcomes.</summary>
internal sealed record ErasureRootAnchor(long Revision, string Digest,
    SortedDictionary<string, AnchoredStateTransition> Admissions, SortedDictionary<string, AnchoredStateTransition> Completed)
{
    /// <summary>Current committed immutable ancestry pointer, changed only with the tenant head CAS.</summary>
    public string? ArchiveHead { get; init; }
    /// <summary>Revision authenticated by the committed archive pointer.</summary>
    public long ArchiveRevision { get; init; }
}
