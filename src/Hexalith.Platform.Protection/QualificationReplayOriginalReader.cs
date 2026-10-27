using System.Text.Json.Nodes;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Actual exact nonce/source lookup reader with complete native ACL/census auditing. It starts no replay write, even when an original is absent.</summary>
public sealed class QualificationReplayOriginalReader(IQualificationOriginalStore originals,IQualificationNativeCaseAuditReader audit,QualificationReplayCaseWorkflow replay):IQualificationObservedOriginalReader
{
    /// <inheritdoc />
    public Task<IReadOnlyList<string>> PreflightStagesAsync(JsonObject original,CancellationToken token)=>audit.PreflightStagesAsync(original,token);
    /// <inheritdoc />
    public async Task<QualificationObservedOriginal?> ReadAsync(JsonObject request,CancellationToken token)
    {
        string? requestId=SecretsProof.Text(request,"requestId");if(requestId is null) return null;
        var retained=await originals.LookupResultAsync("executor",requestId+":case-result",token).ConfigureAwait(false);var actual=await replay.ReadOriginalAsync(request,token).ConfigureAwait(false);
        var native=await audit.ReadAsync(request,token).ConfigureAwait(false);
        if(retained is null || actual is null || !JsonNode.DeepEquals(retained,actual) || native?.Facts?["replayStreamCensus"] is not JsonArray census) return null;
        string receipt=QualificationProtocolSchema.Digest(retained);var state=QualificationDeniedCaseState.Create(request,native.OriginalRevision,receipt);
        state["outcome"]="Recorded";state["status"]="Recorded";
        var original=actual["replayOriginal"]!;
        if(!QualificationReplayStateProjection.Apply(state,original,(JsonObject)request["enrolledScope"]!)) return null;
        return new(requestId,receipt,"replay:"+native.OriginalRevision,new JsonObject { ["state"]=state,["nativeStageBoundary"]=native.Facts?["nativeStageBoundary"]?.DeepClone(),["receipts"]=native.Receipts.DeepClone(),["nativeMutationSources"]=native.Facts?["nativeMutationSources"]?.DeepClone(),["targetMutations"]=native.TargetMutations.DeepClone(),
            ["surfaceScans"]=native.SurfaceScans.DeepClone(),["copyCensus"]=native.CopyCensus.DeepClone(),["linearization"]=new JsonObject(),["replayStreamCensus"]=census.DeepClone(),["independentReplayOriginal"]=original.DeepClone() });
    }
}
