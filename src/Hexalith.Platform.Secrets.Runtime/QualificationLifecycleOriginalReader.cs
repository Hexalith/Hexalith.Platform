using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Independent exact native lookup and original-time holder/control observation for lifecycle cases. Missing delivery or reservation evidence cannot become a success.</summary>
public sealed class QualificationLifecycleOriginalReader(IQualificationOriginalStore originals,IQualificationNativeCaseAuditReader audit,
    QualificationLifecycleCaseWorkflow workflow,IReadOnlyDictionary<string,QualificationLifecycleCaseInput> inputs):IQualificationObservedOriginalReader
{
    /// <inheritdoc />
    public async Task<QualificationObservedOriginal?> ReadAsync(JsonObject request,CancellationToken token)
    {
        string? id=SecretsProof.Text(request,"caseId"),requestId=SecretsProof.Text(request,"requestId");if(id is null || requestId is null || !inputs.TryGetValue(id,out var input)) return null;
        var retained=await originals.LookupResultAsync("executor",requestId+":case-result",token).ConfigureAwait(false);var native=await audit.ReadAsync(request,token).ConfigureAwait(false);
        if(retained is null || native?.Facts?["lifecycleOriginal"] is not JsonObject originalFact || !JsonNode.DeepEquals(originalFact,retained)
            || native.Facts["lifecycleReservationOriginal"] is not JsonObject reservation || reservation["operationId"]?.GetValue<string>()!=(input.Lifecycle?.OperationId??input.Registration.OperationId)
            || reservation["requestId"]?.GetValue<string>()!=requestId || reservation["receiptId"]?.GetValue<string>() is not { Length:>0 }) return null;
        var actual=await workflow.ObserveNativeAsync(request,token).ConfigureAwait(false);if(actual is null) return null;
        long exact=0,pins=0;string? outcome=null;
        if(input.Manifest is { } manifest)
        {
            var observed=actual.Deserialize<CustodyManifestOriginal>(CustodyBindingContract.Json);if(observed is null) return null;
            if(observed.Result.TenantId!=manifest.Capability.TenantId || observed.Result.BatchId!=manifest.Capability.BatchId || observed.Result.ReservationReceiptId!=input.ReservationReceiptId) return null;
            if(observed.Result.State==DeletionManifestProviderState.Consumed && observed.RestoreBarrierReceiptId is not null
                && observed.Result.TargetReceipts.Count==manifest.Targets.Count && observed.Result.TargetReceipts.All(value=>manifest.Targets.Contains(value.Target))) { exact=observed.Result.TargetReceipts.Count;outcome="Completed"; }
            if(id=="S3.hold-pin" && native.Facts["retainedPins"] is JsonArray retainedPins && observed.Result.State!=DeletionManifestProviderState.Consumed) { pins=retainedPins.Count;outcome="Denied"; }
        }
        else
        {
            var observed=actual.Deserialize<CustodyKeyLifecycleOutcome>(CustodyBindingContract.Json);if(observed?.Identity!=input.Registration.Identity || observed.OperationId!=(input.Lifecycle?.OperationId??input.Registration.OperationId)) return null;
            if(input.Lifecycle is not null && observed.RequestDigest!=CustodyBindingContract.Hash(JsonSerializer.SerializeToUtf8Bytes(input.Lifecycle))) return null;
            if(observed.Status is CustodyKeyLifecycleStatus.Wrapped or CustodyKeyLifecycleStatus.Pinned or CustodyKeyLifecycleStatus.Unpinned && observed.ReceiptId is not null) { exact=1;outcome="Completed"; }
            if(id=="S3.lifecycle.unwrap")
            {
                if(retained["actual"]?["State"]?.GetValue<string>()!="Released" || native.Facts["keyDeliveryOriginal"] is not JsonObject delivery
                    || delivery["requestId"]?.GetValue<string>()!=requestId || delivery["allHoldersCompleted"]?.GetValue<bool>()!=true || delivery["receiptId"]?.GetValue<string>() is not { Length:>0 }) return null;
                exact=1;outcome="Completed";
            }
        }
        if(outcome is null) return null;
        string receipt=QualificationProtocolSchema.Digest(retained);var state=QualificationDeniedCaseState.Create(request,native.OriginalRevision,receipt);state["outcome"]=outcome;state["status"]=outcome;
        if(id is "S3.lifecycle.destroy" or "S3.hold-pin")
        {
            if(native.Facts["destroyedTargetIds"] is not JsonArray destroyed || native.Facts["pinnedTargetIds"] is not JsonArray pinned) return null;
            state["destroyedTargetIds"]=destroyed.DeepClone();state["pinnedTargetIds"]=pinned.DeepClone();
        }
        return new(requestId,receipt,"custody:"+native.OriginalRevision,new JsonObject { ["state"]=state,["receipts"]=native.Receipts.DeepClone(),["targetMutations"]=native.TargetMutations.DeepClone(),
            ["surfaceScans"]=native.SurfaceScans.DeepClone(),["copyCensus"]=native.CopyCensus.DeepClone(),["linearization"]=new JsonObject(),["independentLifecycleVerification"]=new JsonObject { ["ExactReceiptCount"]=exact,["CurrentReservationCount"]=1,["RetainedPinCount"]=pins },["nativeOriginal"]=actual });
    }
}
