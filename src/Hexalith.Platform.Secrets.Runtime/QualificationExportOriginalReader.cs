using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Independent actual unwrap/offline verification of retained export artifacts, with actual native audit/census reads.</summary>
public sealed class QualificationExportOriginalReader(IQualificationOriginalStore originals,IQualificationNativeCaseAuditReader audit,QualificationExportCaseWorkflow independentlyInstalledWorkflow):IQualificationObservedOriginalReader
{
    /// <inheritdoc />
    public Task<IReadOnlyList<string>> PreflightStagesAsync(JsonObject original,CancellationToken token)=>audit.PreflightStagesAsync(original,token);
    /// <inheritdoc />
    public async Task<QualificationObservedOriginal?> ReadAsync(JsonObject request,CancellationToken token)
    {
        string? requestId=SecretsProof.Text(request,"requestId");if(requestId is null) return null;
        var retained=await originals.LookupResultAsync("executor",requestId+":case-result",token).ConfigureAwait(false);if(retained is null) return null;
        var actual=await independentlyInstalledWorkflow.ObserveAsync(request,retained,token).ConfigureAwait(false);var native=await audit.ReadAsync(request,token).ConfigureAwait(false);
        if(actual is null || native is null || native.OriginalRevision<1) return null;
        string receipt=QualificationProtocolSchema.Digest(retained);var state=QualificationDeniedCaseState.Create(request,native.OriginalRevision,receipt);state["outcome"]=actual.Outcome;state["status"]=actual.Outcome;
        return new(requestId,receipt,"export-original:"+native.OriginalRevision,new JsonObject { ["state"]=state,["nativeStageBoundary"]=native.Facts?["nativeStageBoundary"]?.DeepClone(),["receipts"]=native.Receipts.DeepClone(),["nativeMutationSources"]=native.Facts?["nativeMutationSources"]?.DeepClone(),["targetMutations"]=native.TargetMutations.DeepClone(),
            ["surfaceScans"]=native.SurfaceScans.DeepClone(),["copyCensus"]=native.CopyCensus.DeepClone(),["linearization"]=new JsonObject(),["independentExportVerification"]=JsonSerializer.SerializeToNode(actual,CustodyBindingContract.Json) });
    }
}
