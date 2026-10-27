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
        var runtimeOriginals=new QualificationRetainedRuntimeRead(originals,runtime);
        foreach(string id in enrolledCaseIds)
        {
            Func<JsonObject,CancellationToken,Task<JsonObject?>>? execute=id.StartsWith("S1.inventory.",StringComparison.Ordinal) || id=="S2.retained-digest"?runtimeOriginals.ExecuteAsync:
                envelope is not null && (id is "S2.canonical-hmac" or "S2.byte-identical-replay" or "S2.replay-reject.changed-fields" or "S2.replay-reject.cross-tenant" or "S2.replay-reject.foreign-issuer" || id.StartsWith("S2.substitution.",StringComparison.Ordinal))?envelope.ExecuteAsync:
                capability is not null && (id=="S4.capability-canonical-14-fields" || id.StartsWith("S4.capability-substitution.",StringComparison.Ordinal))?capability.ExecuteAsync:null;
            if(execute is null) continue;
            Func<JsonObject,CancellationToken,Task<JsonObject?>> recover=id.StartsWith("S1.inventory.",StringComparison.Ordinal) || id=="S2.retained-digest"?runtimeOriginals.LookupAsync:
                id.StartsWith("S4.capability",StringComparison.Ordinal)?capability!.LookupAsync:envelope!.LookupAsync;
            var registration=Retained(id,originals,execute,recover);
            if(id.StartsWith("S2.replay-reject.",StringComparison.Ordinal)) registration=registration with { Preflight=envelope!.PreflightAsync };
            result.Add(registration);
        }
        return result;
    }
    /// <summary>Publishes one raw actual result under the standard case identity. A lost source acknowledgement is lookup-only and cannot call the source again.</summary>
    public static QualificationSourceWorkflow Retained(string caseId,IQualificationOriginalStore originals,Func<JsonObject,CancellationToken,Task<JsonObject?>> source,Func<JsonObject,CancellationToken,Task<JsonObject?>>? sourceLookup=null)
    {
        async Task<JsonObject?> Lookup(JsonObject original,CancellationToken token)
        {
            string id=original["requestId"]!.GetValue<string>()+":case-result";
            if(await originals.LookupResultAsync("executor",id,token).ConfigureAwait(false) is { } retained) return retained;
            if(sourceLookup is null || await originals.ReadAdmissionAsync("executor",id,original,token).ConfigureAwait(false)!=QualificationOriginalAdmissionState.Admitted) return null;
            var recovered=await sourceLookup(original,token).ConfigureAwait(false);
            if(recovered is not null) await originals.RetainAsync("executor",id,recovered,token).ConfigureAwait(false);
            return recovered;
        }
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
