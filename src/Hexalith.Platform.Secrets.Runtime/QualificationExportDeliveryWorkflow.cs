using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Actors;
using Dapr.Actors.Client;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Actual original-target delivery and all eleven bounded receiving substitutions. Uncertain results recover only retained receiving originals.</summary>
public sealed class QualificationExportDeliveryWorkflow(IQualificationOriginalStore originals,IReadOnlyDictionary<string,QualificationExportDeliveryInput> inputs,
    Func<ExportKeyDeliveryIdentity,IExportKeyDeliveryActor>? actorFactory=null)
{
    /// <summary>Gets exact missing current receiving-original inputs after unresolved recovery.</summary>
    public IReadOnlyList<string> MissingReadInputs { get; private set; }=[];
    private IExportKeyDeliveryActor Actor(ExportKeyDeliveryIdentity original)=>actorFactory?.Invoke(original)??ActorProxy.Create<IExportKeyDeliveryActor>(new ActorId(original.ActorId),ExportKeyDeliveryActor.ActorTypeName);
    private static bool Substitution(JsonObject request)=>request["caseId"]!.GetValue<string>().StartsWith("S3.delivery-substitution.",StringComparison.Ordinal);
    private static ExportKeyDeliveryReceivingRequest Receiving(JsonObject request,QualificationExportDeliveryInput input)=>new(input.Original,ExportKeyDeliveryRawIdentity.Capture(Substitute(request["caseId"]!.GetValue<string>(),input.Original)));
    /// <summary>Registers the unchanged full delivery case group.</summary>
    public IReadOnlyList<QualificationSourceWorkflow> Registrations()=>inputs.Keys.Where(Supported).Select(id=>new QualificationSourceWorkflow(id,ExecuteAsync,LookupAsync,PreflightAsync)).ToArray();
    /// <summary>Checks bounded receiving input on the original target; no invalid candidate address getter or future rejection is needed.</summary>
    public Task<IReadOnlyList<string>> PreflightAsync(JsonObject request,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();var input=Input(request);if(input is null) return Task.FromResult<IReadOnlyList<string>>(["ExactEnrolledPrincipalDeliveryOriginal"]);
        try { Receiving(request,input).ValidateBounds();return Task.FromResult<IReadOnlyList<string>>([]); }
        catch(ArgumentException) { return Task.FromResult<IReadOnlyList<string>>(["BoundedOriginalTargetReceivingIdentity"]); }
    }
    /// <summary>Dispatches one actual delivery receiving call. Neither unknown delivery nor missing outer retention can authorize another call.</summary>
    public async Task<JsonObject?> ExecuteAsync(JsonObject request,CancellationToken token)
    {
        var input=Input(request);if(input is null || (await PreflightAsync(request,token).ConfigureAwait(false)).Count!=0) return null;
        string requestId=request["requestId"]!.GetValue<string>();if(!await originals.BeginAsync("executor",requestId+":case-result",request,token).ConfigureAwait(false)) return await LookupAsync(request,token).ConfigureAwait(false);
        var actor=Actor(input.Original);
        if(Substitution(request))
        {
            var retained=await actor.LookupAsync(input.Original).WaitAsync(token).ConfigureAwait(false);
            if(retained.Identity!=input.Original || retained.State is not (ExportKeyDeliveryState.Delivered or ExportKeyDeliveryState.NotDelivered)) return null;
            var received=Receiving(request,input);var result=await actor.DeliverReceivedAsync(received).WaitAsync(token).ConfigureAwait(false);
            if(result is not { State:ExportKeyDeliveryState.Conflict,Denial:{ } denial } || result.Requested!=received.Supplied || !denial.IsExact(received)) return null;
            var value=EncodeReceiving(request,denial);await RetainReceivingAsync(request,value,token).ConfigureAwait(false);return value;
        }
        var outcome=await actor.DeliverAsync(input.Original).WaitAsync(token).ConfigureAwait(false);
        if(outcome.Identity!=input.Original || outcome.State is not (ExportKeyDeliveryState.Delivered or ExportKeyDeliveryState.NotDelivered)) return null;
        var terminal=Encode(request,outcome);await QualificationTerminalResult.RetainAsync(originals,request,terminal,token).ConfigureAwait(false);return terminal;
    }
    /// <summary>Reads only the exact versioned native receiving rejection or immutable ordinary delivery, never current comparison as execution evidence.</summary>
    public async Task<JsonObject?> LookupAsync(JsonObject request,CancellationToken token)
    {
        var input=Input(request);if(input is null) return null;if(await ReadTerminalAsync(request,token).ConfigureAwait(false) is { } old) return old;
        if((await PreflightAsync(request,token).ConfigureAwait(false)).Count!=0) return null;
        if(Substitution(request))
        {
            if(await ObserveReceivingDenialOriginalAsync(request,token).ConfigureAwait(false) is { } denial)
            { var value=EncodeReceiving(request,denial);await RetainReceivingAsync(request,value,token).ConfigureAwait(false);return value; }
            // Preserve exact existing v1 denial recovery where that original was actually produced by the old receiver.
            if(await ObserveDenialOriginalAsync(request,token).ConfigureAwait(false) is { } legacy)
            {
                var value=Encode(request,new(legacy.RequestedIdentity,ExportKeyDeliveryState.Conflict));value["deliveryOrigin"]="ExactReceivingDeliverResponse.v3";value["receivingDenialOriginal"]=JsonSerializer.SerializeToNode(legacy,CustodyBindingContract.Json);
                string id=request["requestId"]!.GetValue<string>()+":case-delivery-denial-v3";await originals.BeginAsync("executor",id,request,token).ConfigureAwait(false);await originals.RetainAsync("executor",id,value,token).ConfigureAwait(false);return value;
            }
            MissingReadInputs=["InstalledExportKeyDeliveryReceivingDenialOriginalRoleCredentialAndIndependentStore"];return null;
        }
        var result=await Actor(input.Original).LookupAsync(input.Original).WaitAsync(token).ConfigureAwait(false);
        if(result.Identity!=input.Original || result.State is not (ExportKeyDeliveryState.Delivered or ExportKeyDeliveryState.NotDelivered)) return null;
        var terminal=Encode(request,result);await QualificationTerminalResult.RetainAsync(originals,request,terminal,token).ConfigureAwait(false);return terminal;
    }
    /// <summary>Reads the ordinary unchanged delivery original; current comparison is never substitution evidence.</summary>
    public async Task<ExportKeyDeliveryOutcome?> ObserveAsync(JsonObject request,CancellationToken token)
    { var input=Input(request);if(input is null || Substitution(request)) return null;var read=await Actor(input.Original).LookupAsync(input.Original).WaitAsync(token).ConfigureAwait(false);return read.Identity==input.Original?read:null; }
    /// <summary>Reads an exact executed raw receiving rejection from its independent v2 namespace.</summary>
    public async Task<ExportKeyDeliveryReceivingDenialOriginal?> ObserveReceivingDenialOriginalAsync(JsonObject request,CancellationToken token)
    {
        var input=Input(request);if(input is null || !Substitution(request)) return null;var received=Receiving(request,input);
        var denial=await Actor(input.Original).LookupReceivingRejectionAsync(received).WaitAsync(token).ConfigureAwait(false);return denial is not null && denial.IsExact(received)?denial:null;
    }
    /// <summary>Reads only existing valid v1 originals without changing their namespace, body or signature.</summary>
    public async Task<ExportKeyDeliveryDenialOriginal?> ObserveDenialOriginalAsync(JsonObject request,CancellationToken token)
    {
        var input=Input(request);if(input is null || !Substitution(request)) return null;var supplied=Substitute(request["caseId"]!.GetValue<string>(),input.Original);
        try { supplied.Validate();if(supplied.ActorId!=input.Original.ActorId) return null; } catch(ArgumentException) { return null; }
        var denial=await Actor(input.Original).LookupReceivingDenialAsync(supplied).WaitAsync(token).ConfigureAwait(false);return denial is not null && denial.IsExact(supplied) && denial.RetainedOriginal.Identity==input.Original?denial:null;
    }
    /// <summary>Preserves valid existing initial results and all old uncertainty bodies; new raw receiving originals use a separate v4 locator.</summary>
    public async Task<JsonObject?> ReadTerminalAsync(JsonObject request,CancellationToken token)
    {
        string id=request["requestId"]!.GetValue<string>();
        var current=await originals.LookupResultAsync("executor",id+":case-delivery-rejection-v4",token).ConfigureAwait(false);
        if(current is not null && TerminalDenialBody(current) && Input(request) is { } input
            && current["receivingDenialOriginal"]!.Deserialize<ExportKeyDeliveryReceivingDenialOriginal>(CustodyBindingContract.Json)!.IsExact(Receiving(request,input))) return current;
        var legacy=await originals.LookupResultAsync("executor",id+":case-delivery-denial-v3",token).ConfigureAwait(false);
        if(legacy?["deliveryOrigin"]?.GetValue<string>()=="ExactReceivingDeliverResponse.v3" && legacy["delivery"]?["State"]?.GetValue<string>()=="Conflict"
            && JsonNode.DeepEquals(legacy["caseId"],request["caseId"]) && JsonNode.DeepEquals(legacy["requestId"],request["requestId"])) return legacy;
        return await QualificationTerminalResult.ReadAsync(originals,id,TerminalBody,token).ConfigureAwait(false);
    }
    internal static bool TerminalBody(JsonObject value)=>value["delivery"]?["State"]?.GetValue<string>() is "Delivered" or "NotDelivered";
    internal static bool TerminalDenialBody(JsonObject value)
    {
        if(value["delivery"]?["State"]?.GetValue<string>()!="Conflict") return false;
        return value["deliveryOrigin"]?.GetValue<string>() switch
        {
            "ExactBoundReceivingDenial.v4"=>value["receivingDenialOriginal"]?.Deserialize<ExportKeyDeliveryReceivingDenialOriginal>(CustodyBindingContract.Json) is { } denial && denial.IsExact(new(denial.Target,denial.Requested)),
            "ExactReceivingDeliverResponse.v3"=>value["receivingDenialOriginal"]?.Deserialize<ExportKeyDeliveryDenialOriginal>(CustodyBindingContract.Json) is { } old && old.IsExact(old.RequestedIdentity),
            _=>false,
        };
    }
    private async Task RetainReceivingAsync(JsonObject request,JsonObject value,CancellationToken token)
    { string id=request["requestId"]!.GetValue<string>()+":case-delivery-rejection-v4";await originals.BeginAsync("executor",id,request,token).ConfigureAwait(false);await originals.RetainAsync("executor",id,value,token).ConfigureAwait(false); }
    private static JsonObject EncodeReceiving(JsonObject request,ExportKeyDeliveryReceivingDenialOriginal denial)=>new() { ["caseId"]=request["caseId"]!.DeepClone(),["requestId"]=request["requestId"]!.DeepClone(),
        ["delivery"]=new JsonObject { ["Identity"]=JsonSerializer.SerializeToNode(denial.Requested,CustodyBindingContract.Json),["State"]="Conflict",["ObservedAt"]=null,["Reason"]="receiving-rejected" },
        ["deliveryOrigin"]="ExactBoundReceivingDenial.v4",["receivingDenialOriginal"]=JsonSerializer.SerializeToNode(denial,CustodyBindingContract.Json) };
    private static JsonObject Encode(JsonObject request,ExportKeyDeliveryOutcome outcome)=>new() { ["caseId"]=request["caseId"]!.DeepClone(),["requestId"]=request["requestId"]!.DeepClone(),["delivery"]=JsonSerializer.SerializeToNode(new { Identity=new { outcome.Identity.TenantId,outcome.Identity.DeliveryId,outcome.Identity.ExportId,outcome.Identity.CommitAcknowledgementRevision,outcome.Identity.RequesterActorId,outcome.Identity.Purpose,outcome.Identity.Audience,outcome.Identity.ExclusiveExpiry,outcome.Identity.LifecycleVersion,outcome.Identity.StoreTarget,outcome.Identity.ContractVersion },outcome.State,outcome.ObservedAt,outcome.Reason },CustodyBindingContract.Json) };
    private QualificationExportDeliveryInput? Input(JsonObject request)
    {
        string? id=SecretsProof.Text(request,"caseId");return id is not null && Supported(id) && inputs.TryGetValue(id,out var input) && request["enrolledScope"] is JsonObject scope
            && QualificationProtocolSchema.Digest(scope)==input.EnrolledScopeSha256 && scope["tenantId"]?.GetValue<string>()==input.Original.TenantId?input:null;
    }

    private static ExportKeyDeliveryIdentity Substitute(string id,ExportKeyDeliveryIdentity value)=>id switch {
        "S3.delivery-substitution.tenant"=>value with { TenantId=value.TenantId+"-substituted" },"S3.delivery-substitution.delivery-id"=>value with { DeliveryId=value.DeliveryId+"-substituted" },
        "S3.delivery-substitution.export-id"=>value with { ExportId=value.ExportId+"-substituted" },"S3.delivery-substitution.ack-revision"=>value with { CommitAcknowledgementRevision=value.CommitAcknowledgementRevision+1 },
        "S3.delivery-substitution.requester"=>value with { RequesterActorId=value.RequesterActorId+"-substituted" },"S3.delivery-substitution.purpose"=>value with { Purpose=value.Purpose+"-substituted" },
        "S3.delivery-substitution.audience"=>value with { Audience=value.Audience+"-substituted" },"S3.delivery-substitution.expiry"=>value with { ExclusiveExpiry=value.ExclusiveExpiry.AddSeconds(1) },
        "S3.delivery-substitution.lifecycle-version"=>value with { LifecycleVersion=value.LifecycleVersion+"-substituted" },"S3.delivery-substitution.store-target"=>value with { StoreTarget=value.StoreTarget+"-substituted" },
        "S3.delivery-substitution.contract-version"=>value with { ContractVersion=value.ContractVersion+1 },_=>value };
    /// <summary>Gets the closed actor-level case group.</summary>
    public static bool Supported(string id)=>id=="S3.principal-direct-delivery" || id.StartsWith("S3.delivery-substitution.",StringComparison.Ordinal) || id.StartsWith("S3.delivery-expiry.",StringComparison.Ordinal)
        || id.StartsWith("S3.delivery-actor-revocation.",StringComparison.Ordinal) || id.StartsWith("Recovery.delivery.",StringComparison.Ordinal);
}
