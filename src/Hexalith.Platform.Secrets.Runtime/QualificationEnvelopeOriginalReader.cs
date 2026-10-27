using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Positive exact envelope/replay reader. It never signs a missing envelope, registers a missing nonce or dispatches a target command.</summary>
public sealed class QualificationEnvelopeOriginalReader(IQualificationOriginalStore originals,IQualificationNativeCaseAuditReader audit,QualificationEnvelopeCaseWorkflow verifier):IQualificationObservedOriginalReader
{
    /// <inheritdoc />
    public Task<IReadOnlyList<string>> PreflightStagesAsync(JsonObject original,CancellationToken token)=>audit.PreflightStagesAsync(original,token);
    /// <inheritdoc />
    public async Task<QualificationObservedOriginal?> ReadAsync(JsonObject request,CancellationToken token)
    {
        string? id=SecretsProof.Text(request,"caseId"),requestId=SecretsProof.Text(request,"requestId");if(id is not ("S2.canonical-hmac" or "S2.byte-identical-replay") || requestId is null) return null;
        var retained=await originals.LookupResultAsync("executor",requestId+":case-result",token).ConfigureAwait(false);var verified=await verifier.ReadVerifyAsync(request,token).ConfigureAwait(false);
        var native=await audit.ReadAsync(request,token).ConfigureAwait(false);
        if(retained is null || verified is null || !JsonNode.DeepEquals(retained,verified) || verified["firstStatus"]?.GetValue<string>()!="Succeeded"
            || verified["exactRetryStatus"]?.GetValue<string>()!="Succeeded" || native is null) return null;
        if(id=="S2.byte-identical-replay" && native.Facts?["domainIdempotencyOriginal"] is not JsonObject) return null;
        string receipt=QualificationProtocolSchema.Digest(retained);var state=QualificationDeniedCaseState.Create(request,native.OriginalRevision,receipt);
        state["outcome"]=id=="S2.canonical-hmac"?"Verified":native.Facts!["domainIdempotencyOriginal"]!["outcome"]!.DeepClone();state["status"]=state["outcome"]!.DeepClone();
        if(id=="S2.byte-identical-replay" && !QualificationReplayStateProjection.Apply(state,verified["replayOriginal"]!,(JsonObject)request["enrolledScope"]!)) return null;
        return new(requestId,receipt,"envelope:"+native.OriginalRevision,new JsonObject { ["state"]=state,["nativeStageBoundary"]=native.Facts?["nativeStageBoundary"]?.DeepClone(),["receipts"]=native.Receipts.DeepClone(),["nativeMutationSources"]=native.Facts?["nativeMutationSources"]?.DeepClone(),["targetMutations"]=native.TargetMutations.DeepClone(),
            ["surfaceScans"]=native.SurfaceScans.DeepClone(),["copyCensus"]=native.CopyCensus.DeepClone(),["linearization"]=new JsonObject(),["independentEnvelopeVerification"]=verified.DeepClone() });
    }
}
