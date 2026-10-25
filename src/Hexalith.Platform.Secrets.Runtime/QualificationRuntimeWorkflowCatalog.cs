using Dapr.Actors;
using Dapr.Actors.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Closed executable registrations for actual inventory/HMAC/envelope/capability workflows. It never infers installed read or recovery readiness.</summary>
public static class QualificationRuntimeWorkflowCatalog
{
    /// <summary>Registers only case IDs implemented by the supplied concrete source workflows.</summary>
    public static IReadOnlyList<QualificationSourceWorkflow> Create(IQualificationOriginalStore originals,IReadOnlyCollection<string> enrolledCaseIds,
        QualificationRuntimeSourceDriver runtime,QualificationEnvelopeCaseWorkflow? envelope=null,QualificationCapabilityCaseWorkflow? capability=null)
    {
        var result=new List<QualificationSourceWorkflow>();
        foreach(string id in enrolledCaseIds)
        {
            Func<JsonObject,CancellationToken,Task<JsonObject?>>? execute=id.StartsWith("S1.inventory.",StringComparison.Ordinal) || id=="S2.retained-digest"?runtime.ExecuteAsync:
                envelope is not null && (id is "S2.canonical-hmac" or "S2.byte-identical-replay" or "S2.replay-reject.changed-fields" or "S2.replay-reject.cross-tenant" or "S2.replay-reject.foreign-issuer" || id.StartsWith("S2.substitution.",StringComparison.Ordinal))?envelope.ExecuteAsync:
                capability is not null && (id=="S4.capability-canonical-14-fields" || id.StartsWith("S4.capability-substitution.",StringComparison.Ordinal))?capability.ExecuteAsync:null;
            if(execute is null) continue;
            result.Add(Retained(id,originals,execute));
        }
        return result;
    }
    /// <summary>Publishes one raw actual result under the standard case identity. A lost source acknowledgement is lookup-only and cannot call the source again.</summary>
    public static QualificationSourceWorkflow Retained(string caseId,IQualificationOriginalStore originals,Func<JsonObject,CancellationToken,Task<JsonObject?>> source)
    {
        Task<JsonObject?> Lookup(JsonObject original,CancellationToken token)=>originals.LookupResultAsync("executor",original["requestId"]!.GetValue<string>()+":case-result",token);
        async Task<JsonObject?> Execute(JsonObject original,CancellationToken token)
        {
            if(original["caseId"]?.GetValue<string>()!=caseId) return null;
            string requestId=original["requestId"]!.GetValue<string>()+":case-result";
            bool first=await originals.BeginAsync("executor",requestId,original,token).ConfigureAwait(false);
            if(!first) return await Lookup(original,token).ConfigureAwait(false);
            var actual=await source(original,token).ConfigureAwait(false);
            if(actual is not null) await originals.RetainAsync("executor",requestId,actual,token).ConfigureAwait(false);
            return actual;
        }
        return new(caseId,Execute,Lookup,(_,_)=>Task.FromResult<IReadOnlyList<string>>([]));
    }
}
