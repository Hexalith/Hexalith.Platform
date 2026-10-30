using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Protection;

/// <summary>Original manifest identity retained before rotate/rewrap/trim. Prepared survivor bytes precede the irreversible native operation.</summary>
internal sealed record ErasureRootManifest(DeletionBatchConsumptionRequest Request, string ReservationReceiptId, string RequestDigest,
    int PredecessorEpoch, int SuccessorEpoch, SortedDictionary<string, ErasureRootTarget>? Survivors,
    DeletionTargetReceipt[] Receipts)
{
    /// <summary>Exact admitted qualification provider call that actually committed this terminal manifest, if present. Legacy/native ordinary originals omit it.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? CompletingQualificationCallDigest { get; init; }
    /// <summary>Prepared survivor payload under the exact pending tenant CAS; it is never copied into a completed manifest.</summary>
    public string? SurvivorArchive { get; init; }
}
