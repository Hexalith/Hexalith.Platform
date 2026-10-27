using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Exact content-free delivery actor lookup and actual independent transport/audit proof correlation. Unknown original results are never renewed.</summary>
public sealed class QualificationExportDeliveryOriginalReader(IQualificationOriginalStore originals,IQualificationNativeCaseAuditReader audit,QualificationExportDeliveryWorkflow workflow):IQualificationObservedOriginalReader
{
    /// <inheritdoc />
    public Task<IReadOnlyList<string>> PreflightStagesAsync(JsonObject original,CancellationToken token)=>audit.PreflightStagesAsync(original,token);
    /// <inheritdoc />
    public async Task<QualificationObservedOriginal?> ReadAsync(JsonObject request,CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(originals);
        string? id=SecretsProof.Text(request,"caseId"),requestId=SecretsProof.Text(request,"requestId");if(id is null || requestId is null) return null;
        bool substitution=id.StartsWith("S3.delivery-substitution.",StringComparison.Ordinal);
        if(substitution)
        {
            var retainedReceiving=await workflow.ReadTerminalAsync(request,token).ConfigureAwait(false);var observed=await audit.ReadAsync(request,token).ConfigureAwait(false);
            if(retainedReceiving is null || observed?.Facts?["principalDeliveryDenialOriginal"] is not JsonObject denialProof) return null;
            JsonNode? executed;DateTimeOffset expiry;
            if(retainedReceiving["deliveryOrigin"]?.GetValue<string>()=="ExactBoundReceivingDenial.v4")
            {
                var receiving=await workflow.ObserveReceivingDenialOriginalAsync(request,token).ConfigureAwait(false);if(receiving is null) return null;
                executed=JsonSerializer.SerializeToNode(receiving,CustodyBindingContract.Json);expiry=receiving.Requested.ExclusiveExpiry;
            }
            else
            {
                var legacy=await workflow.ObserveDenialOriginalAsync(request,token).ConfigureAwait(false);if(legacy is null) return null;
                executed=JsonSerializer.SerializeToNode(legacy,CustodyBindingContract.Json);expiry=legacy.RequestedIdentity.ExclusiveExpiry;
            }
            if(!JsonNode.DeepEquals(denialProof,executed) || !JsonNode.DeepEquals(retainedReceiving["receivingDenialOriginal"],denialProof)) return null;
            string originalReceipt=QualificationProtocolSchema.Digest(retainedReceiving);var deniedState=QualificationDeniedCaseState.Create(request,observed.OriginalRevision,originalReceipt);
            deniedState["outcome"]="Denied";deniedState["status"]="Denied";deniedState["deliveredAt"]=null;deniedState["exclusiveExpiry"]=expiry.ToUnixTimeMilliseconds();
            return new(requestId,originalReceipt,"delivery:"+observed.OriginalRevision,new JsonObject { ["state"]=deniedState,["nativeStageBoundary"]=observed.Facts["nativeStageBoundary"]?.DeepClone(),
                ["receipts"]=observed.Receipts.DeepClone(),["nativeMutationSources"]=observed.Facts["nativeMutationSources"]?.DeepClone(),["targetMutations"]=observed.TargetMutations.DeepClone(),["surfaceScans"]=observed.SurfaceScans.DeepClone(),["copyCensus"]=observed.CopyCensus.DeepClone(),
                ["linearization"]=observed.Facts["linearization"]?.DeepClone()??new JsonObject(),["independentDeliveryOriginal"]=retainedReceiving["delivery"]!.DeepClone(),["independentReceivingDenialOriginal"]=denialProof.DeepClone() });
        }
        var retained=await workflow.ReadTerminalAsync(request,token).ConfigureAwait(false);var actual=await workflow.ObserveAsync(request,token).ConfigureAwait(false);
        var native=await audit.ReadAsync(request,token).ConfigureAwait(false);
        var result=retained?["delivery"]?.Deserialize<ExportKeyDeliveryOutcome>(CustodyBindingContract.Json);
        if(result is null || actual is null || native is null || native.Facts?["principalDeliveryOriginal"] is not JsonObject proof
            || !JsonNode.DeepEquals(proof,JsonSerializer.SerializeToNode(actual,CustodyBindingContract.Json))) return null;
        if(result!=actual) return null;
        string receipt=QualificationProtocolSchema.Digest(retained!);var state=QualificationDeniedCaseState.Create(request,native.OriginalRevision,receipt);
        state["outcome"]=actual.State.ToString();state["status"]=state["outcome"]!.DeepClone();
        state["deliveredAt"]=actual.ObservedAt?.ToUnixTimeMilliseconds();state["exclusiveExpiry"]=actual.Identity.ExclusiveExpiry.ToUnixTimeMilliseconds();
        {
            if(native.Facts["principalDeliveryTiming"] is not JsonObject timing) return null;
            state["timing"]=timing.DeepClone();
        }
        return new(requestId,receipt,"delivery:"+native.OriginalRevision,new JsonObject { ["state"]=state,["nativeStageBoundary"]=native.Facts?["nativeStageBoundary"]?.DeepClone(),["receipts"]=native.Receipts.DeepClone(),["nativeMutationSources"]=native.Facts?["nativeMutationSources"]?.DeepClone(),["targetMutations"]=native.TargetMutations.DeepClone(),
            ["surfaceScans"]=native.SurfaceScans.DeepClone(),["copyCensus"]=native.CopyCensus.DeepClone(),["linearization"]=native.Facts!["linearization"]?.DeepClone()??new JsonObject(),
            ["independentDeliveryOriginal"]=proof.DeepClone() });
    }
}
