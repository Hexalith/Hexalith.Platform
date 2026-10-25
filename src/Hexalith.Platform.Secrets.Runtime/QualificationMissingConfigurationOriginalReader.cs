using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Projects the concrete missing-configuration original using actual independently read retention revisions, audit receipts and leakage scans.</summary>
public sealed class QualificationMissingConfigurationOriginalReader(IQualificationOriginalStore readOnlyOriginals,IQualificationNativeCaseAuditReader audit):IQualificationObservedOriginalReader
{
    /// <inheritdoc />
    public async Task<QualificationObservedOriginal?> ReadAsync(JsonObject original,CancellationToken token)
    {
        if(SecretsProof.Text(original,"caseId")!="S1.reject.missing-config" || SecretsProof.Text(original,"requestId") is not { } requestId || original["enrolledScope"] is not JsonObject scope) return null;
        var actual=await readOnlyOriginals.LookupResultAsync("executor",requestId+":missing-config",token).ConfigureAwait(false);
        var native=await audit.ReadAsync(original,token).ConfigureAwait(false);
        var independentAdmission=QualificationDenial.Evaluate(null);
        if(actual is null || actual["requestId"]?.GetValue<string>()!=requestId || actual["outcome"]?.GetValue<string>()!="Denied" || independentAdmission.StatusCode!=503
            || !JsonNode.DeepEquals(actual["admission"],JsonSerializer.SerializeToNode(independentAdmission,CustodyBindingContract.Json)) || native is null || native.OriginalRevision<1) return null;
        string receipt=QualificationProtocolSchema.Digest(actual);
        var state=QualificationDeniedCaseState.Create(original,native.OriginalRevision,receipt);
        return new(requestId,receipt,native.OriginalRevision.ToString(System.Globalization.CultureInfo.InvariantCulture),new JsonObject { ["state"]=state,["receipts"]=native.Receipts.DeepClone(),
            ["targetMutations"]=native.TargetMutations.DeepClone(),["surfaceScans"]=native.SurfaceScans.DeepClone(),["copyCensus"]=native.CopyCensus.DeepClone(),["linearization"]=new JsonObject() });
    }
}
