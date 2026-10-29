using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Protection;

/// <summary>Independently enrolled current anchor and immutable exact transition admissions/outcomes.</summary>
internal sealed record ErasureRootAnchor(long Revision, string Digest,
    SortedDictionary<string, AnchoredStateTransition> Admissions, SortedDictionary<string, AnchoredStateTransition> Completed);
