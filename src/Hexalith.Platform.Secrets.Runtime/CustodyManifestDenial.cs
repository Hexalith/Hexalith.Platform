using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>An immutable held-manifest decision, including its original attempt and exact observed pins.</summary>
public sealed record CustodyManifestDenial(string OperationId,string RequestDigest,string RequestAttemptId,long CustodyRevision,
    string CheckpointDigest,string ManifestDigest,string ReceiptId,IReadOnlyList<CustodyManifestHeldPin> Pins)
{
    internal bool Matches(DeletionBatchConsumptionRequest original,string reservation)
        =>OperationId==CustodyLifecycleClient.ManifestOperationId(original,reservation)
            && RequestDigest==CustodyBindingContract.Hash(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(original))
            && CustodyBindingContract.Text(RequestAttemptId) && CustodyRevision>=0 && CustodyBindingContract.Digest(CheckpointDigest)
            && ManifestDigest==original.Capability.ManifestDigest && CustodyBindingContract.Text(ReceiptId)
            && Pins is { Count:>0 } && Pins.All(pin=>original.Targets.Contains(pin.Target) && CustodyBindingContract.Text(pin.HoldId)
                && pin.GuardRevision>0 && CustodyBindingContract.Text(pin.GuardReceiptId))
            && Pins.Select(pin=>(pin.Target,pin.HoldId)).Distinct().Count()==Pins.Count;
}

/// <summary>A target pin observed at the original denial's conditional custody head.</summary>
public sealed record CustodyManifestHeldPin(ProtectionTarget Target,string HoldId,long GuardRevision,string GuardReceiptId);
