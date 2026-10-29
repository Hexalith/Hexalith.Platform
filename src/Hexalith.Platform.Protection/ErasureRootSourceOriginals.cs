namespace Hexalith.Platform.Protection;

/// <summary>Independent source-owner originals, not an ERC backup: source-created bindings and locations contain no wrapped roots, anchors, epoch keys or erasure index.</summary>
/// <param name="TenantId">Exact source-owner tenant.</param><param name="StoreId">Original enrolled ERC identity.</param><param name="Originals">Immutable source-created occurrence enrollments.</param>
public sealed record ErasureRootSourceOriginals(string TenantId, string StoreId, Dictionary<string, ErasureRootOccurrenceEnrollment> Originals);
