using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Independent actual decision re-verification plus native original/audit reads, never a copied sender validity flag.</summary>
public sealed class QualificationDecisionOriginalReader(IQualificationOriginalStore originals,IQualificationNativeCaseAuditReader audit,QualificationDecisionCaseWorkflow independentlyInstalledVerifier):IQualificationObservedOriginalReader
{
    /// <inheritdoc />
    public Task<IReadOnlyList<string>> PreflightStagesAsync(JsonObject original,CancellationToken token)=>audit.PreflightStagesAsync(original,token);
    /// <inheritdoc />
    public async Task<QualificationObservedOriginal?> ReadAsync(JsonObject original,CancellationToken cancellationToken)
    {
        string? requestId=SecretsProof.Text(original,"requestId");if(requestId is null) return null;
        var retained=await originals.LookupResultAsync("executor",requestId+":case-result",cancellationToken).ConfigureAwait(false);
        var actual=await independentlyInstalledVerifier.VerifyAsync(original,cancellationToken).ConfigureAwait(false);
        var native=await audit.ReadAsync(original,cancellationToken).ConfigureAwait(false);
        if(retained is null || actual is null || !JsonNode.DeepEquals(retained,actual) || native is null || native.OriginalRevision<1 || actual["verified"] is not JsonValue flag || !flag.TryGetValue<bool>(out bool verified)) return null;
        string receipt=QualificationProtocolSchema.Digest(retained);var state=QualificationDeniedCaseState.Create(original,native.OriginalRevision,receipt);
        state["outcome"]=verified?"Verified":"Denied";state["status"]=state["outcome"]!.DeepClone();
        return new(requestId,receipt,"decision-original:"+native.OriginalRevision,new JsonObject { ["state"]=state,["nativeStageBoundary"]=native.Facts?["nativeStageBoundary"]?.DeepClone(),["receipts"]=native.Receipts.DeepClone(),["nativeMutationSources"]=native.Facts?["nativeMutationSources"]?.DeepClone(),["targetMutations"]=native.TargetMutations.DeepClone(),
            ["surfaceScans"]=native.SurfaceScans.DeepClone(),["copyCensus"]=native.CopyCensus.DeepClone(),["linearization"]=new JsonObject(),["independentDecisionVerification"]=actual.DeepClone() });
    }
}
