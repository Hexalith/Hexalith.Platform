using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Four actual independent read stages with durable exact control identity; missing controller protocols refuse before case effects.</summary>
public sealed class QualificationObservationOrchestrator(IQualificationOriginalStore originals,IQualificationRecoveryController recovery,
    IQualificationObservedOriginalReader reader)
{
    /// <summary>Runs one original case operation, actual restart, actual restore, then exact lookup. Unknown controls and effects are lookup-only.</summary>
    public async Task<JsonObject?> ObserveAsync(JsonObject request,Func<bool,CancellationToken,Task> executeOrLookup,CancellationToken token)
    {
        if(SecretsProof.Parse(request.ToJsonString()) is not { } normalized) return null;
        request=normalized;
        if((await recovery.PreflightAsync(request,token).ConfigureAwait(false)).Count!=0) return null;
        string? requestId=SecretsProof.Text(request,"requestId");
        if(requestId is null || !QualificationProtocolSchema.ValidRequest("executor","execute",request) && !QualificationProtocolSchema.ValidOwnerRequest("executor",request)) return null;
        await originals.BeginAsync("executor",requestId+":observations",request,token).ConfigureAwait(false);
        if(await originals.LookupResultAsync("executor",requestId+":observations",token).ConfigureAwait(false) is { } recorded) return recorded;
        bool first=await originals.BeginAsync("executor",requestId+":case-effect",request,token).ConfigureAwait(false);
        await executeOrLookup(!first,token).ConfigureAwait(false);
        var snapshots=new JsonArray();var controls=new JsonArray(); QualificationObservedOriginal? retained=null;
        async Task<bool> Read(string actualStage)
        {
            var observed=await reader.ReadAsync(request,token).ConfigureAwait(false);
            if(observed is null || observed.RequestId!=requestId || observed.OriginalReceiptSha256.Length!=64 || string.IsNullOrWhiteSpace(observed.AuthorityRevision)) return false;
            if(retained is not null && (retained.OriginalReceiptSha256!=observed.OriginalReceiptSha256 || !JsonNode.DeepEquals(retained.Source["state"]??retained.Source,observed.Source["state"]??observed.Source))) return false;
            retained??=observed;
            snapshots.Add(new JsonObject { ["stage"]=actualStage,["originalRequestId"]=requestId,["originalResultSha256"]=observed.OriginalReceiptSha256,
                ["stateSha256"]=QualificationProtocolSchema.Digest(observed.Source),["authorityRevision"]=observed.AuthorityRevision,["source"]=observed.Source.DeepClone(),["sourceProof"]=System.Text.Json.JsonSerializer.SerializeToNode(observed,CustodyBindingContract.Json) });
            return true;
        }
        if(!await Read("AfterEffect").ConfigureAwait(false)) return null;
        foreach(string operation in new[]{"restart","restore"})
        {
            string controlId=requestId+":"+operation;
            var controlRequest=(JsonObject)request.DeepClone(); controlRequest["originalResultSha256"]=retained!.OriginalReceiptSha256;
            bool start=await originals.BeginAsync("executor",controlId,controlRequest,token).ConfigureAwait(false);
            var control=start ? await recovery.ExecuteAsync(operation,controlRequest,token).ConfigureAwait(false)
                : await recovery.LookupAsync(operation,controlRequest,token).ConfigureAwait(false);
            if(control is null || !recovery.Verify(control,controlRequest) || control.Operation!=operation || control.RequestId!=requestId
                || control.TargetCommit!=SecretsProof.Text(request,"targetCommit") || control.IsolatedTargetId!=SecretsProof.Text(request,"isolatedTargetId")
                || control.OriginalReceiptSha256!=retained.OriginalReceiptSha256 || control.BeforeIncarnation==control.AfterIncarnation
                || string.IsNullOrWhiteSpace(control.BeforeIncarnation) || string.IsNullOrWhiteSpace(control.AfterIncarnation)) return null;
            controls.Add(System.Text.Json.JsonSerializer.SerializeToNode(control,CustodyBindingContract.Json));
            if(!await Read(operation=="restart"?"AfterRestart":"AfterRestore").ConfigureAwait(false)) return null;
        }
        await executeOrLookup(true,token).ConfigureAwait(false);
        if(!await Read("AfterLookup").ConfigureAwait(false)) return null;
        var result=new JsonObject { ["requestId"]=requestId,["originalResultSha256"]=retained!.OriginalReceiptSha256,["snapshots"]=snapshots,["controls"]=controls };
        await originals.RetainAsync("executor",requestId+":observations",result,token).ConfigureAwait(false);
        return result;
    }
}
