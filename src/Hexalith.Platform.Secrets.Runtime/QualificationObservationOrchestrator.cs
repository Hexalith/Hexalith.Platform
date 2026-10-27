using System.Text.Json;
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
        if((await recovery.PreflightAsync(request,token).ConfigureAwait(false)).Count!=0 || (await reader.PreflightStagesAsync(request,token).ConfigureAwait(false)).Count!=0) return null;
        string? requestId=SecretsProof.Text(request,"requestId");
        if(requestId is null || !QualificationProtocolSchema.ValidRequest("executor","execute",request) && !QualificationProtocolSchema.ValidOwnerRequest("executor",request)) return null;
        await originals.BeginAsync("executor",requestId+":observations-v2",request,token).ConfigureAwait(false);
        if(await originals.LookupResultAsync("executor",requestId+":observations-v2",token).ConfigureAwait(false) is { } recorded) return recorded;
        bool first=await originals.BeginAsync("executor",requestId+":case-effect",request,token).ConfigureAwait(false);
        await executeOrLookup(!first,token).ConfigureAwait(false);
        var snapshots=new JsonArray();var controls=new JsonArray(); QualificationObservedOriginal? retained=null;
        QualificationObservedOriginal? previous=null;QualificationRecoveryControlOriginal? precedingControl=null;
        async Task<bool> Read(string actualStage,string? nextControl)
        {
            string stageId=requestId+":observation-stage-v2:"+actualStage;
            string predecessor=previous is null?QualificationProtocolSchema.Digest(request):QualificationProtocolSchema.Digest(System.Text.Json.JsonSerializer.SerializeToNode(previous,CustodyBindingContract.Json)!);
            var context=new QualificationObservationStageRequest("EXT-SECRETS-1.native-observation.v2",QualificationProtocolSchema.Digest(request),actualStage,predecessor,
                actualStage=="AfterLookup"?requestId+":lookup-stage":precedingControl?.OperationId,
                actualStage=="AfterLookup"?null:precedingControl is null?null:QualificationProtocolSchema.Digest(System.Text.Json.JsonSerializer.SerializeToNode(precedingControl,CustodyBindingContract.Json)!));
            JsonObject? snapshot=await originals.LookupResultAsync("executor",stageId,token).ConfigureAwait(false);
            if(snapshot is null)
            {
                var measured=await reader.LookupStageAsync(request,context,token).ConfigureAwait(false);
                if(measured is null)
                {
                    if(nextControl is not null && await originals.ReadAdmissionAsync("executor",requestId+":"+nextControl,
                        nextControl=="lookup-stage"?request:ControlRequest(),token).ConfigureAwait(false)!=QualificationOriginalAdmissionState.Absent)
                        throw new InvalidOperationException("External.ExactHistoricalStageSnapshotBeforeAlreadyAdmittedControlProtocol:"+actualStage);
                    await originals.BeginAsync("executor",stageId,request,token).ConfigureAwait(false);
                    // Only the independent receiving service may conditionally admit this first delivery.
                    // An existing remote uncertainty denies there before any second native measurement.
                    measured=await reader.ReadStageAsync(request,context,token).ConfigureAwait(false);
                }
                else await originals.BeginAsync("executor",stageId,request,token).ConfigureAwait(false);
                if(measured is null) return false;
                snapshot=new JsonObject { ["stage"]=actualStage,["originalRequestId"]=requestId,["originalResultSha256"]=measured.OriginalReceiptSha256,
                    ["stateSha256"]=QualificationProtocolSchema.Digest(measured.Source),["authorityRevision"]=measured.AuthorityRevision,["source"]=measured.Source.DeepClone(),
                    ["sourceProof"]=System.Text.Json.JsonSerializer.SerializeToNode(measured,CustodyBindingContract.Json) };
                await originals.RetainAsync("executor",stageId,snapshot,token).ConfigureAwait(false);
            }
            var observed=snapshot["sourceProof"]?.Deserialize<QualificationObservedOriginal>(CustodyBindingContract.Json);
            if(observed is null || observed.RequestId!=requestId || observed.OriginalReceiptSha256.Length!=64 || string.IsNullOrWhiteSpace(observed.AuthorityRevision)
                || snapshot["stage"]?.GetValue<string>()!=actualStage || snapshot["stateSha256"]?.GetValue<string>()!=QualificationProtocolSchema.Digest(observed.Source)
                || snapshot["originalRequestId"]?.GetValue<string>()!=requestId || snapshot["originalResultSha256"]?.GetValue<string>()!=observed.OriginalReceiptSha256
                || snapshot["authorityRevision"]?.GetValue<string>()!=observed.AuthorityRevision || observed.StageEvidence?.Boundary!=context || observed.StageEvidence.Native!=QualificationObservationStage.RequireNative(observed,context)
                || !JsonNode.DeepEquals(snapshot["source"],observed.Source)) return false;
            if(retained is not null && (retained.OriginalReceiptSha256!=observed.OriginalReceiptSha256 || !JsonNode.DeepEquals(retained.Source["state"]??retained.Source,observed.Source["state"]??observed.Source))) return false;
            retained??=observed;previous=observed;snapshots.Add(snapshot.DeepClone());return true;
        }
        JsonObject ControlRequest()
        {
            var value=(JsonObject)request.DeepClone();if(retained is not null) value["originalResultSha256"]=retained.OriginalReceiptSha256;return value;
        }
        if(!await Read("AfterEffect","restart").ConfigureAwait(false)) return null;
        foreach(string operation in new[]{"restart","restore"})
        {
            string controlId=requestId+":"+operation;
            var controlRequest=ControlRequest();
            var control=(await originals.LookupResultAsync("executor",controlId,token).ConfigureAwait(false))?.Deserialize<QualificationRecoveryControlOriginal>(CustodyBindingContract.Json);
            if(control is null)
            {
                bool start=await originals.BeginAsync("executor",controlId,controlRequest,token).ConfigureAwait(false);
                control=start ? await recovery.ExecuteAsync(operation,controlRequest,token).ConfigureAwait(false)
                    : await recovery.LookupAsync(operation,controlRequest,token).ConfigureAwait(false);
            }
            if(control is null || !recovery.Verify(control,controlRequest) || control.Operation!=operation || control.RequestId!=requestId
                || control.TargetCommit!=SecretsProof.Text(request,"targetCommit") || control.IsolatedTargetId!=SecretsProof.Text(request,"isolatedTargetId")
                || control.OriginalReceiptSha256!=retained!.OriginalReceiptSha256 || control.BeforeIncarnation==control.AfterIncarnation
                || string.IsNullOrWhiteSpace(control.BeforeIncarnation) || string.IsNullOrWhiteSpace(control.AfterIncarnation)) return null;
            var controlBody=(JsonObject)System.Text.Json.JsonSerializer.SerializeToNode(control,CustodyBindingContract.Json)!;
            await originals.RetainAsync("executor",controlId,controlBody,token).ConfigureAwait(false);
            controls.Add(controlBody);precedingControl=control;
            if(!await Read(operation=="restart"?"AfterRestart":"AfterRestore",operation=="restart"?"restore":"lookup-stage").ConfigureAwait(false)) return null;
        }
        await originals.BeginAsync("executor",requestId+":lookup-stage",request,token).ConfigureAwait(false);
        await executeOrLookup(true,token).ConfigureAwait(false);
        if(!await Read("AfterLookup",null).ConfigureAwait(false)) return null;
        var result=new JsonObject { ["requestId"]=requestId,["originalResultSha256"]=retained!.OriginalReceiptSha256,["snapshots"]=snapshots,["controls"]=controls };
        await originals.RetainAsync("executor",requestId+":observations-v2",result,token).ConfigureAwait(false);
        return result;
    }
}
