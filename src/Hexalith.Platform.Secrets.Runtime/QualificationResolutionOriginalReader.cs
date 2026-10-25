using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Independent repetition of a read-only resolution, exact durable-original comparison, and actual native audit/census reads.</summary>
public sealed class QualificationResolutionOriginalReader(IQualificationOriginalStore originals,IQualificationNativeCaseAuditReader audit,
    Func<JsonObject,CancellationToken,Task<JsonObject?>> independentlyResolve):IQualificationObservedOriginalReader
{
    /// <inheritdoc />
    public async Task<QualificationObservedOriginal?> ReadAsync(JsonObject original,CancellationToken token)
    {
        string? requestId=SecretsProof.Text(original,"requestId");if(requestId is null) return null;
        var retained=await originals.LookupResultAsync("executor",requestId+":case-result",token).ConfigureAwait(false);
        var actual=await independentlyResolve(original,token).ConfigureAwait(false);var native=await audit.ReadAsync(original,token).ConfigureAwait(false);
        if(retained is null || actual is null || !JsonNode.DeepEquals(retained,actual) || native is null || native.OriginalRevision<1 || actual["outcome"]?.GetValue<string>() is not ("Denied" or "Resolved")) return null;
        string receipt=QualificationProtocolSchema.Digest(retained);var state=QualificationDeniedCaseState.Create(original,native.OriginalRevision,receipt);
        state["outcome"]=actual["outcome"]!.DeepClone();state["status"]=state["outcome"]!.DeepClone();
        return new(requestId,receipt,"resolution:"+native.OriginalRevision,new JsonObject { ["state"]=state,["receipts"]=native.Receipts.DeepClone(),["targetMutations"]=native.TargetMutations.DeepClone(),
            ["surfaceScans"]=native.SurfaceScans.DeepClone(),["copyCensus"]=native.CopyCensus.DeepClone(),["linearization"]=new JsonObject(),["independentResolution"]=actual.DeepClone() });
    }
}
