using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Positive capability primitive reader: exact retained signer original, separately current public verification and native audit, with no signing in observation.</summary>
public sealed class QualificationCapabilityOriginalReader(IQualificationOriginalStore originals,IQualificationNativeCaseAuditReader audit,QualificationCapabilityCaseWorkflow verification):IQualificationObservedOriginalReader
{
    /// <inheritdoc />
    public Task<IReadOnlyList<string>> PreflightStagesAsync(JsonObject original,CancellationToken token)=>audit.PreflightStagesAsync(original,token);
    /// <inheritdoc />
    public async Task<QualificationObservedOriginal?> ReadAsync(JsonObject request,CancellationToken token)
    {
        if(request["caseId"]?.GetValue<string>()!="S4.capability-canonical-14-fields" || request["requestId"]?.GetValue<string>() is not { } id) return null;
        var retained=await originals.LookupResultAsync("executor",id+":case-result",token).ConfigureAwait(false);var actual=await verification.ReadVerifyAsync(request,token).ConfigureAwait(false);var native=await audit.ReadAsync(request,token).ConfigureAwait(false);
        if(retained is null || actual is null || !JsonNode.DeepEquals(retained,actual) || actual["verified"]?.GetValue<bool>()!=true || actual["payload"] is not JsonObject payload || native is null) return null;
        string receipt=QualificationProtocolSchema.Digest(retained);var state=QualificationDeniedCaseState.Create(request,native.OriginalRevision,receipt);state["outcome"]="Verified";state["status"]="Verified";
        foreach(var field in new[]{("sealId","DestructionSealId"),("batchId","BatchId"),("manifestDigest","ManifestDigest"),("attestationOrdinal","AttestationOrdinal"),("keyVersion","CapabilityKeyVersion")}) state[field.Item1]=payload[field.Item2]!.DeepClone();
        return new(id,receipt,"capability:"+native.OriginalRevision,new JsonObject { ["state"]=state,["nativeStageBoundary"]=native.Facts?["nativeStageBoundary"]?.DeepClone(),["receipts"]=native.Receipts.DeepClone(),["nativeMutationSources"]=native.Facts?["nativeMutationSources"]?.DeepClone(),["targetMutations"]=native.TargetMutations.DeepClone(),
            ["surfaceScans"]=native.SurfaceScans.DeepClone(),["copyCensus"]=native.CopyCensus.DeepClone(),["linearization"]=new JsonObject(),["independentCapabilityVerification"]=actual.DeepClone() });
    }
}
