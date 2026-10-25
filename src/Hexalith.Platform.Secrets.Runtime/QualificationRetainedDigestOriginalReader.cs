using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Actual retained-version HMAC recomputation reader. It starts no signature, replay admission or lifecycle effect.</summary>
public sealed class QualificationRetainedDigestOriginalReader(IQualificationOriginalStore originals,IQualificationNativeCaseAuditReader audit,QualificationRuntimeSourceDriver digests):IQualificationObservedOriginalReader
{
    /// <inheritdoc />
    public async Task<QualificationObservedOriginal?> ReadAsync(JsonObject request,CancellationToken token)
    {
        if(request["caseId"]?.GetValue<string>()!="S2.retained-digest" || request["requestId"]?.GetValue<string>() is not { } id) return null;
        var retained=await originals.LookupResultAsync("executor",id+":case-result",token).ConfigureAwait(false);var current=await digests.ExecuteAsync(request,token).ConfigureAwait(false);var native=await audit.ReadAsync(request,token).ConfigureAwait(false);
        if(retained is null || current is null || !JsonNode.DeepEquals(retained,current) || current["first"]?["Status"]?.GetValue<string>()!="Succeeded"
            || !JsonNode.DeepEquals(current["first"],current["lookup"]) || native is null) return null;
        string receipt=QualificationProtocolSchema.Digest(retained);var state=QualificationDeniedCaseState.Create(request,native.OriginalRevision,receipt);state["outcome"]="Verified";state["status"]="Verified";
        return new(id,receipt,"digest:"+native.OriginalRevision,new JsonObject { ["state"]=state,["receipts"]=native.Receipts.DeepClone(),["targetMutations"]=native.TargetMutations.DeepClone(),
            ["surfaceScans"]=native.SurfaceScans.DeepClone(),["copyCensus"]=native.CopyCensus.DeepClone(),["linearization"]=new JsonObject(),["retainedDigestOriginal"]=retained["first"]!.DeepClone(),["retainedDigestCurrent"]=current["lookup"]!.DeepClone() });
    }
}
