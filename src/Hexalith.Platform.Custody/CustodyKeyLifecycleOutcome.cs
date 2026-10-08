namespace Hexalith.Platform.Custody;

/// <summary>Immutable opaque physical outcome, read only under separate current exact private lookup authorization.</summary>
/// <param name="Identity">Complete original key.</param><param name="OperationId">Original operation.</param><param name="RequestDigest">Exact content-free request identity.</param>
/// <param name="Status">Original physical result.</param><param name="ReceiptId">Authenticated original physical receipt.</param><param name="RestoreBarrierReceiptId">Irreversible all-copy/backup/restore exclusion proof for Destroyed.</param>
public sealed record CustodyKeyLifecycleOutcome(CustodyKeyObjectIdentity Identity, string OperationId, string RequestDigest, CustodyKeyLifecycleStatus Status,
    string? ReceiptId = null, string? RestoreBarrierReceiptId = null);
