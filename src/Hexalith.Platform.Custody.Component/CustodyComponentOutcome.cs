using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Custody.Component;

/// <summary>Value-free result of one disposable component operation. Receipts are witness facts, not transport bodies.</summary>
/// <param name="Status">Observation.</param>
/// <param name="OperationId">Original operation.</param>
/// <param name="RequestDigest">Exact request digest.</param>
/// <param name="ReceiptId">Witness receipt for a terminal hold or the requested target.</param>
/// <param name="RestoreBarrierReceiptId">Witness barrier for destruction.</param>
/// <param name="TargetReceipts">Ordered manifest vector. Empty unless destroyed.</param>
/// <param name="Identity">Requested key, when the operation names one.</param>
/// <param name="HoldId">Exact hold, when the operation names one.</param>
internal sealed record CustodyComponentOutcome(
    CustodyComponentStatus Status,
    string OperationId,
    string RequestDigest,
    string? ReceiptId = null,
    string? RestoreBarrierReceiptId = null,
    IReadOnlyList<DeletionTargetReceipt>? TargetReceipts = null,
    CustodyKeyObjectIdentity? Identity = null,
    string? HoldId = null);
