namespace Hexalith.Platform.Protection;

/// <summary>Independent source-owner originals, not an ERC backup: source-created bindings and locations contain no wrapped roots, anchors, epoch keys or erasure index.</summary>
/// <param name="TenantId">Exact source-owner tenant.</param><param name="StoreId">Original enrolled ERC identity.</param><param name="Originals">Immutable source-created occurrence enrollments.</param>
public sealed record ErasureRootSourceOriginals(string TenantId, string StoreId, Dictionary<string, ErasureRootOccurrenceEnrollment> Originals)
{
    /// <summary>Exact accepted enrollment for the partitioned format; legacy maps remain immutable and readable.</summary>
    public string? EnrollmentId { get; init; }
    /// <summary>Authenticated exact source radix root; no lifetime-sized pointer map.</summary>
    public string? OriginalIndexRoot { get; init; }
    /// <summary>Authenticated exact snapshot radix root, retaining independently verified ciphertext and skeleton originals.</summary>
    public string? SnapshotIndexRoot { get; init; }
    /// <summary>Actual native-AEAD-verified snapshot outputs and technical skeletons; neither roots nor ERC authority is copied.</summary>
    public Dictionary<string, Hexalith.EventStore.Contracts.Security.InteractionOccurrenceSealedResult> SnapshotOriginals { get; init; } = new(StringComparer.Ordinal);
}
