using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Custody;

/// <summary>Exact durable guard mirror after the protection-owner block, with no signing or batch-dispatch authority.</summary>
/// <remarks>The owning guard must independently authenticate the complete receipt and persist it under its current revision.
/// The EventStore adapter provides source implementation; independent authority and installation still require qualification.</remarks>
public interface IDeletionCapabilityGuardRevocationMirror
{
    /// <summary>Conditionally mirrors the exact original complete block result; duplicate retry does not mint another fact.</summary>
    Task<bool> RecordAsync(DeletionCapabilityRevocationReceipt receipt, CancellationToken cancellationToken = default);

    /// <summary>Looks up the complete original mirrored receipt, resolving lost mirror acknowledgement.</summary>
    Task<DeletionCapabilityRevocationReceipt?> LookupAsync(DeletionCapabilityRevocationEnvelope envelope,
        CancellationToken cancellationToken = default);
}
