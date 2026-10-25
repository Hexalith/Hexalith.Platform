using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Protection;

/// <summary>Actual append-only directory metadata and exact native immutable outcome lookups. Metadata alone does not establish all-writer enforcement.</summary>
public sealed record QualificationDirectoryBoundaryOriginals(DirectoryEpochLedger Ledger,IReadOnlyList<DirectoryBoundaryOutcome> Originals);
