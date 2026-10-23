using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Security;

/// <summary>Private durable tenant state; actor turns atomically order blocks, activation and irreversible reservations.</summary>
/// <param name="TenantId">Exact tenant.</param>
/// <param name="Revision">Monotonic durable revision.</param>
/// <param name="KeyBlockSetRevision">Monotonic global block-set revision; blocks are never cleared by activation.</param>
/// <param name="Batches">Complete registered owner batches.</param>
/// <param name="Revocations">Append-only original authenticated key revocation receipts.</param>
/// <param name="Operations">Append-only exact operation receipts.</param>
internal sealed record DeletionConsumptionLedger(string TenantId, long Revision, long KeyBlockSetRevision,
    IReadOnlyList<DeletionConsumptionBatch> Batches, IReadOnlyList<DeletionCapabilityRevocationReceipt> Revocations,
    IReadOnlyList<DeletionConsumptionOperation> Operations);
