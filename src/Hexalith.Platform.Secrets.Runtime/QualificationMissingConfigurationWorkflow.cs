using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Concrete missing-configuration case. The actual runtime admission result and exact request are retained before independent native observation.</summary>
public sealed class QualificationMissingConfigurationWorkflow(IQualificationOriginalStore originals)
{
    /// <summary>Gets the executable case registration.</summary>
    public QualificationSourceWorkflow Registration=>new("S1.reject.missing-config",ExecuteAsync,LookupAsync,(_,_)=>Task.FromResult<IReadOnlyList<string>>([]));
    /// <summary>Evaluates an absent runtime configuration. It cannot invoke a material owner port.</summary>
    public async Task<JsonObject?> ExecuteAsync(JsonObject original,CancellationToken token)
    {
        if(SecretsProof.Text(original,"caseId")!="S1.reject.missing-config" || SecretsProof.Text(original,"requestId") is not { } requestId) return null;
        string id=requestId+":missing-config";
        bool first=await originals.BeginAsync("executor",id,original,token).ConfigureAwait(false);
        if(!first) return await LookupAsync(original,token).ConfigureAwait(false);
        var admission=QualificationDenial.Evaluate(null);
        var actual=new JsonObject { ["requestId"]=requestId,["caseId"]="S1.reject.missing-config",["admission"]=JsonSerializer.SerializeToNode(admission,CustodyBindingContract.Json),
            ["outcome"]=admission.StatusCode==503?"Denied":"UnexpectedAdmission" };
        await originals.RetainAsync("executor",id,actual,token).ConfigureAwait(false);
        return actual;
    }
    /// <summary>Reads only the exact previously retained runtime result after uncertainty.</summary>
    public Task<JsonObject?> LookupAsync(JsonObject original,CancellationToken token)=>original["requestId"] is { } id
        ?originals.LookupResultAsync("executor",id.GetValue<string>()+":missing-config",token):Task.FromResult<JsonObject?>(null);
}
