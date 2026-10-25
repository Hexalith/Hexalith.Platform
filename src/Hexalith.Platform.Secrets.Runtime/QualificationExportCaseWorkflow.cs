using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Owning executable adapter to the custody-owned export crypto workflow. It accepts only exact independently enrolled canary identities and dedicated key resolution.</summary>
public sealed class QualificationExportCaseWorkflow(ExportQualificationEngine engine,IQualificationOriginalStore originals,IReadOnlyDictionary<string,ExportQualificationInput> enrolledInputs)
{
    /// <summary>Registers exact retained original execution and lookup, with resolver preflight before any key access.</summary>
    public IReadOnlyList<QualificationSourceWorkflow> Registrations()=>enrolledInputs.Select(pair=>QualificationRuntimeWorkflowCatalog.Retained(pair.Key,originals,ExecuteAsync)
        with { Preflight=async (original,token)=>original["enrolledScope"]?["tenantId"]?.GetValue<string>()!=pair.Value.TenantId || original["enrolledScope"]?["logicalId"]?.GetValue<string>()!=pair.Value.ExportId?["ExactEnrolledExportTenantAndIdentity"]:await engine.PreflightAsync(pair.Key,pair.Value,token).ConfigureAwait(false) }).ToArray();
    private async Task<JsonObject?> ExecuteAsync(JsonObject original,CancellationToken token)
    {
        string? id=SecretsProof.Text(original,"caseId");if(id is null || !enrolledInputs.TryGetValue(id,out var input) || original["enrolledScope"]?["tenantId"]?.GetValue<string>()!=input.TenantId
            || original["enrolledScope"]?["logicalId"]?.GetValue<string>()!=input.ExportId) return null;
        var artifact=await engine.ExecuteAsync(id,input,token).ConfigureAwait(false);if(artifact is null) return null;
        return new JsonObject { ["caseId"]=id,["requestId"]=original["requestId"]!.DeepClone(),["artifact"]=JsonSerializer.SerializeToNode(artifact,CustodyBindingContract.Json) };
    }
    /// <summary>Independently re-verifies one actual original export artifact without generating a new nonce or signing a new artifact.</summary>
    public async Task<ExportQualificationObservation?> ObserveAsync(JsonObject original,JsonObject retained,CancellationToken token)
    {
        string? id=SecretsProof.Text(original,"caseId");if(id is null || !enrolledInputs.TryGetValue(id,out var input) || retained["artifact"] is not JsonObject value
            || value.Deserialize<ExportQualificationOriginal>(CustodyBindingContract.Json) is not { } artifact || artifact.CaseId!=id || retained["requestId"]?.GetValue<string>()!=original["requestId"]?.GetValue<string>()) return null;
        return await engine.ObserveAsync(artifact,input,token).ConfigureAwait(false);
    }
}
