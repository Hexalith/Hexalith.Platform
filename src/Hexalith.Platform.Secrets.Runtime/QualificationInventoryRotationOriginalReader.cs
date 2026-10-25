using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Independent current inventory/retained digest and real invalidation consumer reader for the exact rotation original.</summary>
public sealed class QualificationInventoryRotationOriginalReader(IQualificationOriginalStore originals,IQualificationNativeCaseAuditReader audit,QualificationInventoryRotationWorkflow rotation):IQualificationObservedOriginalReader
{
    /// <inheritdoc />
    public async Task<QualificationObservedOriginal?> ReadAsync(JsonObject request,CancellationToken token)
    {
        string? requestId=SecretsProof.Text(request,"requestId");if(requestId is null) return null;
        var retained=await originals.LookupResultAsync("executor",requestId+":case-result",token).ConfigureAwait(false);var current=await rotation.ObserveAsync(request,token).ConfigureAwait(false);
        var native=await audit.ReadAsync(request,token).ConfigureAwait(false);
        if(retained is null || current is null || native is null || current["inventory"] is not JsonObject inventory || current["safetyVerdicts"] is not JsonArray verdicts
            || current["retainedDigest"] is not JsonObject digest || retained["originalDigest"] is not JsonObject old) return null;
        string receipt=QualificationProtocolSchema.Digest(retained);var state=QualificationDeniedCaseState.Create(request,native.OriginalRevision,receipt);state["outcome"]="Invalidated";state["status"]="Invalidated";
        return new(requestId,receipt,"rotation:"+inventory["Revision"]!.GetValue<long>(),new JsonObject { ["state"]=state,["receipts"]=native.Receipts.DeepClone(),["targetMutations"]=native.TargetMutations.DeepClone(),
            ["surfaceScans"]=native.SurfaceScans.DeepClone(),["copyCensus"]=native.CopyCensus.DeepClone(),["linearization"]=new JsonObject(),["rotationInventory"]=inventory.DeepClone(),
            ["rotationVerdicts"]=verdicts.DeepClone(),["retainedDigestOriginal"]=old.DeepClone(),["retainedDigestCurrent"]=digest.DeepClone() });
    }
}
