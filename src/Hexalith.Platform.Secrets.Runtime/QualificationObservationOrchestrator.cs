using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Exact retained owner source fact, measured by a typed independently authenticated reader.</summary>
public sealed record QualificationObservedOriginal(string RequestId,string OriginalReceiptSha256,string AuthorityRevision,JsonObject Source);
/// <summary>Verified control original. A process or restore operation cannot be replaced by an owner success flag.</summary>
public sealed record QualificationRecoveryControlOriginal(string RequestId,string OperationId,string Operation,string TargetCommit,
    string IsolatedTargetId,string BeforeIncarnation,string AfterIncarnation,string OriginalReceiptSha256,string ControlReceiptSha256,byte[]? Signature=null);
/// <summary>Separately installed exact process/restore controller. No default or self-reported implementation exists.</summary>
public interface IQualificationRecoveryController
{
    /// <summary>Reports exact missing protocol/resource inputs before any case effect or recovery operation.</summary>
    Task<IReadOnlyList<string>> PreflightAsync(JsonObject original,CancellationToken cancellationToken);
    /// <summary>Performs one independently authenticated isolated restart or restore, retaining exact identity across uncertainty.</summary>
    Task<QualificationRecoveryControlOriginal?> ExecuteAsync(string operation,JsonObject original,CancellationToken cancellationToken);
    /// <summary>Reads only the exact control original after loss; never repeats an unknown control.</summary>
    Task<QualificationRecoveryControlOriginal?> LookupAsync(string operation,JsonObject original,CancellationToken cancellationToken);
    /// <summary>Verifies externally enrolled retained public trust and actual process/storage incarnation evidence.</summary>
    bool Verify(QualificationRecoveryControlOriginal original,JsonObject request);
}
/// <summary>Typed actual persisted owner reader. No scenario expected values or stage labels are inputs.</summary>
public interface IQualificationObservedOriginalReader
{
    /// <summary>Reads the exact retained source original under fresh current authority.</summary>
    Task<QualificationObservedOriginal?> ReadAsync(JsonObject original,CancellationToken cancellationToken);
}
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
            if(retained is not null && (retained.OriginalReceiptSha256!=observed.OriginalReceiptSha256 || !JsonNode.DeepEquals(retained.Source,observed.Source))) return false;
            retained??=observed;
            snapshots.Add(new JsonObject { ["stage"]=actualStage,["originalRequestId"]=requestId,["originalResultSha256"]=observed.OriginalReceiptSha256,
                ["stateSha256"]=QualificationProtocolSchema.Digest(observed.Source),["authorityRevision"]=observed.AuthorityRevision,["source"]=observed.Source.DeepClone() });
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
