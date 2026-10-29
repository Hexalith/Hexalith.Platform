using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Protection;

/// <summary>Original manifest identity retained before rotate/rewrap/trim. Prepared survivor bytes precede the irreversible native operation.</summary>
internal sealed record ErasureRootManifest(DeletionBatchConsumptionRequest Request, string ReservationReceiptId, string RequestDigest,
    int PredecessorEpoch, int SuccessorEpoch, SortedDictionary<string, ErasureRootTarget>? Survivors,
    DeletionTargetReceipt[] Receipts);
