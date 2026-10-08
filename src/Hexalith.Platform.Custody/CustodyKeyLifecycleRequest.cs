namespace Hexalith.Platform.Custody;

/// <summary>Immutable complete conditional lifecycle identity; control proof is authenticated independently and consumed linearly by the physical owner.</summary>
/// <param name="Identity">Complete phase-pinned key.</param><param name="OperationId">Original physical operation.</param><param name="Action">Pin, unpin or destroy.</param>
/// <param name="ExpectedRevision">Conditional lifecycle-owner revision.</param><param name="HoldId">Exact hold for pin/unpin, null only for destroy.</param>
/// <param name="FenceTokenId">Exact current fence/store-control reservation.</param><param name="FenceRevision">Original control revision.</param><param name="DecisionVersion">Approved applicable lifecycle/disposition.</param>
/// <param name="ProtectionReservationReceiptId">Mandatory exact protection-owner ConsumptionReserved proof for interaction-root destruction; never a signature-only substitute.</param>
public sealed record CustodyKeyLifecycleRequest(CustodyKeyObjectIdentity Identity, string OperationId, CustodyKeyLifecycleAction Action, long ExpectedRevision,
    string? HoldId, string FenceTokenId, long FenceRevision, string DecisionVersion, string? ProtectionReservationReceiptId = null);
