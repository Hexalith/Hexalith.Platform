using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Custody.Component;

/// <summary>Terminal or unknown manifest observation retained by the witness.</summary>
/// <param name="RequestDigest">Digest of the admitted request.</param>
/// <param name="Status">Retained observation.</param>
/// <param name="RestoreBarrierReceiptId">Barrier after destruction.</param>
/// <param name="Receipts">Ordered vector. Empty unless destroyed.</param>
internal sealed record CustodyManifestRecord(string RequestDigest, CustodyComponentStatus Status, string? RestoreBarrierReceiptId, IReadOnlyList<DeletionTargetReceipt> Receipts);
