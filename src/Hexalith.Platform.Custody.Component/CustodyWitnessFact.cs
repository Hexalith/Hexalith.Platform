namespace Hexalith.Platform.Custody.Component;

/// <summary>One outside-witness fact. It is not part of the volume snapshot.</summary>
/// <param name="OperationId">Original operation.</param>
/// <param name="RequestDigest">Exact digest.</param>
/// <param name="Status">Retained observation.</param>
/// <param name="ReceiptId">Receipt, when the observation has one.</param>
/// <param name="RestoreBarrierReceiptId">Barrier, only for destruction.</param>
/// <param name="Identity">Key the fact names.</param>
/// <param name="HoldId">Hold the fact names.</param>
internal sealed record CustodyWitnessFact(string OperationId, string RequestDigest, CustodyComponentStatus Status, string? ReceiptId,
    string? RestoreBarrierReceiptId, CustodyKeyObjectIdentity? Identity, string? HoldId);
