using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Actors;
using Dapr.Actors.Client;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Closed actor-level direct-delivery, eleven substitution and recovery recipes. The direct transport's atomic principal/expiry/revocation protocol remains separately required.</summary>
public sealed class QualificationExportDeliveryWorkflow(IQualificationOriginalStore originals,IReadOnlyDictionary<string,QualificationExportDeliveryInput> inputs,
    Func<ExportKeyDeliveryIdentity,IExportKeyDeliveryActor>? actorFactory=null)
{
    private IExportKeyDeliveryActor Actor(ExportKeyDeliveryIdentity original)=>actorFactory?.Invoke(original)??ActorProxy.Create<IExportKeyDeliveryActor>(new ActorId(original.ActorId),ExportKeyDeliveryActor.ActorTypeName);
    /// <summary>Registers actual closed actor calls and exact lookup-only recovery.</summary>
    public IReadOnlyList<QualificationSourceWorkflow> Registrations()=>inputs.Keys.Where(Supported).Select(id=>new QualificationSourceWorkflow(id,ExecuteAsync,LookupAsync,PreflightAsync)).ToArray();
    /// <summary>Refuses substitutions without an actual original actor identity and exact scope.</summary>
    public Task<IReadOnlyList<string>> PreflightAsync(JsonObject request,CancellationToken token) { token.ThrowIfCancellationRequested();return Task.FromResult<IReadOnlyList<string>>(Input(request) is null?["ExactEnrolledPrincipalDeliveryOriginal"]:[]); }
    /// <summary>Admits one actor call before dispatch. A lost result never calls Deliver again.</summary>
    public async Task<JsonObject?> ExecuteAsync(JsonObject request,CancellationToken token)
    {
        var input=Input(request);if(input is null) return null;string id=request["caseId"]!.GetValue<string>(),requestId=request["requestId"]!.GetValue<string>();
        bool first=await originals.BeginAsync("executor",requestId+":case-result",request,token).ConfigureAwait(false);if(!first) return await LookupAsync(request,token).ConfigureAwait(false);
        var actor=Actor(input.Original);ExportKeyDeliveryIdentity supplied=Substitute(id,input.Original);ExportKeyDeliveryOutcome result;
        if(id.StartsWith("S3.delivery-substitution.",StringComparison.Ordinal))
        {
            var original=await actor.LookupAsync(input.Original).WaitAsync(token).ConfigureAwait(false);
            if(original.Identity!=input.Original || original.State is not (ExportKeyDeliveryState.Delivered or ExportKeyDeliveryState.NotDelivered)) return null;
            try
            {
                supplied.Validate();
                result=supplied.ActorId!=input.Original.ActorId?new(supplied,ExportKeyDeliveryState.Conflict,Reason:"exact-actor-binding"):
                    await actor.DeliverAsync(supplied).WaitAsync(token).ConfigureAwait(false);
            }
            catch(ArgumentException) { result=new(supplied,ExportKeyDeliveryState.Conflict,Reason:"closed-delivery-identity"); }
        }
        else result=await actor.DeliverAsync(input.Original).WaitAsync(token).ConfigureAwait(false);
        if(result.Identity!=supplied) return null;
        var value=Encode(request,result);await originals.RetainAsync("executor",requestId+":case-result",value,token).ConfigureAwait(false);return value;
    }
    /// <summary>Exact actor-original lookup only after loss; a missing/unknown original remains unresolved.</summary>
    public async Task<JsonObject?> LookupAsync(JsonObject request,CancellationToken token)
    {
        var input=Input(request);if(input is null) return null;string requestId=request["requestId"]!.GetValue<string>();
        if(await originals.LookupResultAsync("executor",requestId+":case-result",token).ConfigureAwait(false) is { } retained) return retained;
        if(request["caseId"]!.GetValue<string>().StartsWith("S3.delivery-substitution.",StringComparison.Ordinal)) return null;
        var result=await Actor(input.Original).LookupAsync(input.Original).WaitAsync(token).ConfigureAwait(false);
        if(result.Identity!=input.Original || result.State is ExportKeyDeliveryState.Unknown or ExportKeyDeliveryState.Unavailable) return null;
        var value=Encode(request,result);await originals.RetainAsync("executor",requestId+":case-result",value,token).ConfigureAwait(false);return value;
    }
    /// <summary>Independently reads only the unchanged original actor outcome, never a missing delivery.</summary>
    public async Task<ExportKeyDeliveryOutcome?> ObserveAsync(JsonObject request,CancellationToken token)
    { var input=Input(request);if(input is null) return null;var read=await Actor(input.Original).LookupAsync(input.Original).WaitAsync(token).ConfigureAwait(false);return read.Identity==input.Original?read:null; }
    private QualificationExportDeliveryInput? Input(JsonObject request)
    {
        string? id=SecretsProof.Text(request,"caseId");return id is not null && Supported(id) && inputs.TryGetValue(id,out var input) && request["enrolledScope"] is JsonObject scope
            && QualificationProtocolSchema.Digest(scope)==input.EnrolledScopeSha256 && scope["tenantId"]?.GetValue<string>()==input.Original.TenantId?input:null;
    }
    private static JsonObject Encode(JsonObject request,ExportKeyDeliveryOutcome outcome)=>new() { ["caseId"]=request["caseId"]!.DeepClone(),["requestId"]=request["requestId"]!.DeepClone(),["delivery"]=JsonSerializer.SerializeToNode(new { Identity=new { outcome.Identity.TenantId,outcome.Identity.DeliveryId,outcome.Identity.ExportId,outcome.Identity.CommitAcknowledgementRevision,outcome.Identity.RequesterActorId,outcome.Identity.Purpose,outcome.Identity.Audience,outcome.Identity.ExclusiveExpiry,outcome.Identity.LifecycleVersion,outcome.Identity.StoreTarget,outcome.Identity.ContractVersion },outcome.State,outcome.ObservedAt,outcome.Reason },CustodyBindingContract.Json) };
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
