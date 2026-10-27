using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Direct local composition of a concrete source workflow, exact retained-original lookup, actual recovery orchestration and independent projection.</summary>
public sealed class QualificationBoundSourceCaseDriver(string caseId,Func<JsonObject,CancellationToken,Task<IReadOnlyList<string>>> installedPreflight,
    Func<JsonObject,CancellationToken,Task<JsonObject?>> execute,Func<JsonObject,CancellationToken,Task<JsonObject?>> lookup,
    QualificationObservationOrchestrator observation,IQualificationMeasuredCaseProjector projector,IQualificationOriginalStore independentlyReadOnlyExecutorOriginals,Func<JsonObject,CancellationToken,Task<JsonObject?>>? resume=null) : IQualificationOwnerCaseDriver
{
    /// <inheritdoc />
    public string CaseId=>caseId;
    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> PreflightAsync(JsonObject original,CancellationToken cancellationToken)
        => (await installedPreflight(original,cancellationToken).ConfigureAwait(false)).Concat(projector.MissingInputs(caseId)).Distinct(StringComparer.Ordinal).ToArray();
    /// <inheritdoc />
    public Task<JsonObject?> ExecuteAsync(JsonObject original,CancellationToken cancellationToken)=>RunAsync("executor",original,false,cancellationToken);
    /// <inheritdoc />
    public Task<JsonObject?> LookupAsync(JsonObject original,CancellationToken cancellationToken)=>RunAsync("executor",original,true,cancellationToken);
    /// <inheritdoc />
    public async Task<JsonObject?> ObserveAsync(JsonObject original,CancellationToken cancellationToken)
    {
        if(!QualificationProtocolSchema.ValidOwnerRequest("executor",original) || SecretsProof.Text(original,"caseId")!=caseId || (await PreflightAsync(original,cancellationToken).ConfigureAwait(false)).Count!=0) return null;
        string? id=SecretsProof.Text(original,"requestId");
        var retained=id is null?null:await independentlyReadOnlyExecutorOriginals.LookupResultAsync("executor",id+":observations-v2",cancellationToken).ConfigureAwait(false);
        return retained is null?null:projector.Project("observer",original,retained);
    }
    private async Task<JsonObject?> RunAsync(string role,JsonObject original,bool onlyLookup,CancellationToken token)
    {
        if(!QualificationProtocolSchema.ValidOwnerRequest("executor",original) || SecretsProof.Text(original,"caseId")!=caseId || (await PreflightAsync(original,token).ConfigureAwait(false)).Count!=0) return null;
        var measured=await observation.ObserveAsync(original,async (recover,cancellation)=>
        {
            var actual=onlyLookup||recover?await lookup(original,cancellation).ConfigureAwait(false):await execute(original,cancellation).ConfigureAwait(false);
            // Only a workflow's explicitly supplied continuation can reconcile its per-step barriers.
            // An arbitrary Execute callback is never a recovery permission.
            if(actual is null && (onlyLookup||recover) && resume is not null)
                actual=await resume(original,cancellation).ConfigureAwait(false);
            if(actual is null) throw new InvalidOperationException("Exact original source outcome remains pending.");
        },token).ConfigureAwait(false);
        return measured is null?null:projector.Project(role,original,measured);
    }
}
