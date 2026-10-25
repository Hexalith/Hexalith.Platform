using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Exact content-free delivery actor lookup and actual independent transport/audit proof correlation. Unknown original results are never renewed.</summary>
public sealed class QualificationExportDeliveryOriginalReader(IQualificationOriginalStore originals,IQualificationNativeCaseAuditReader audit,QualificationExportDeliveryWorkflow workflow):IQualificationObservedOriginalReader
{
    /// <inheritdoc />
    public async Task<QualificationObservedOriginal?> ReadAsync(JsonObject request,CancellationToken token)
    {
        string? id=SecretsProof.Text(request,"caseId"),requestId=SecretsProof.Text(request,"requestId");if(id is null || requestId is null) return null;
        var retained=await originals.LookupResultAsync("executor",requestId+":case-result",token).ConfigureAwait(false);var actual=await workflow.ObserveAsync(request,token).ConfigureAwait(false);
        var native=await audit.ReadAsync(request,token).ConfigureAwait(false);
        var result=retained?["delivery"]?.Deserialize<ExportKeyDeliveryOutcome>(CustodyBindingContract.Json);
        if(result is null || actual is null || native is null || native.Facts?["principalDeliveryOriginal"] is not JsonObject proof
            || !JsonNode.DeepEquals(proof,JsonSerializer.SerializeToNode(actual,CustodyBindingContract.Json))) return null;
        bool substitution=id.StartsWith("S3.delivery-substitution.",StringComparison.Ordinal);
        if(result!=actual || substitution && result.State!=ExportKeyDeliveryState.Conflict) return null;
        string receipt=QualificationProtocolSchema.Digest(retained!);var state=QualificationDeniedCaseState.Create(request,native.OriginalRevision,receipt);
        state["outcome"]=substitution?"Denied":actual.State.ToString();state["status"]=state["outcome"]!.DeepClone();
        state["deliveredAt"]=substitution?null:actual.ObservedAt?.ToUnixTimeMilliseconds();state["exclusiveExpiry"]=actual.Identity.ExclusiveExpiry.ToUnixTimeMilliseconds();
        if(!substitution)
        {
            if(native.Facts["principalDeliveryTiming"] is not JsonObject timing) return null;
            state["timing"]=timing.DeepClone();
        }
        return new(requestId,receipt,"delivery:"+native.OriginalRevision,new JsonObject { ["state"]=state,["receipts"]=native.Receipts.DeepClone(),["targetMutations"]=native.TargetMutations.DeepClone(),
            ["surfaceScans"]=native.SurfaceScans.DeepClone(),["copyCensus"]=native.CopyCensus.DeepClone(),["linearization"]=native.Facts["linearization"]?.DeepClone()??new JsonObject(),
            ["independentDeliveryOriginal"]=proof.DeepClone() });
    }
}
