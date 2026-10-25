using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Concrete exact custody wrap, unwrap, pin/unpin and full-manifest destroy recipes. Uncertainty never repeats delivery or native work.</summary>
public sealed class QualificationLifecycleCaseWorkflow(CustodyLifecycleClient client,IQualificationOriginalStore originals,
    IReadOnlyDictionary<string,QualificationLifecycleCaseInput> inputs,IQualificationLifecycleRootSource? roots=null)
{
    /// <summary>Registers all supported case originals in the receiving path.</summary>
    public IReadOnlyList<QualificationSourceWorkflow> Registrations()=>inputs.Keys.Where(Supported).Select(id=>new QualificationSourceWorkflow(id,ExecuteAsync,LookupAsync,PreflightAsync)).ToArray();
    /// <summary>Checks exact scope, operation, holder and manifest before any source effect.</summary>
    public Task<IReadOnlyList<string>> PreflightAsync(JsonObject request,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();var input=Input(request);if(input is null) return Task.FromResult<IReadOnlyList<string>>(["ExactEnrolledCustodyCaseIdentityAndReservation"]);
        string id=request["caseId"]!.GetValue<string>();
        return Task.FromResult<IReadOnlyList<string>>(id=="S3.lifecycle.wrap" && roots is null?["External.OriginalCanaryRootAndCompleteNativeHolderAdmissionProtocol"]:[]);
    }
    /// <summary>Retains admission before the real client call; an admitted unknown result is lookup-only.</summary>
    public async Task<JsonObject?> ExecuteAsync(JsonObject request,CancellationToken token)
    {
        if((await PreflightAsync(request,token).ConfigureAwait(false)).Count!=0) return null;
        string id=request["caseId"]!.GetValue<string>();string requestId=request["requestId"]!.GetValue<string>();var input=Input(request)!;
        bool first=await originals.BeginAsync("executor",requestId+":case-result",request,token).ConfigureAwait(false);
        if(!first) return await LookupAsync(request,token).ConfigureAwait(false);
        object? actual;
        if(id=="S3.lifecycle.wrap")
        {
            byte[]? root=await roots!.ReadAsync(input.Registration,input.KeyUse!,token).ConfigureAwait(false);if(root is null) return null;
            try { actual=root.Length==32?await client.WrapAsync(input.Registration.Identity,input.Registration.OperationId,input.Registration.ExpectedRevision,
                input.Registration.KekVersion,input.Registration.OpaqueWrappedKeyReference,input.KeyUse!,root,token).ConfigureAwait(false):null; }
            finally { CryptographicOperations.ZeroMemory(root); }
        }
        else if(id=="S3.lifecycle.unwrap")
        {
            var released=await client.UnwrapAsync(input.Registration,input.Lifecycle?.OperationId??requestId,input.KeyUse!,token).ConfigureAwait(false);
            if(released.Key is { } key)
            {
                var borrowed=key.Bytes;key.Dispose();
                actual=new { released.State,KeyLength=borrowed.Length,OwnedPlaintextCleared=borrowed.Span.IndexOfAnyExcept((byte)0)<0 };
            }
            else actual=new { released.State,KeyLength=0,OwnedPlaintextCleared=(bool?)null };
        }
        else if(input.Manifest is { } manifest) actual=await client.ExecuteManifestOriginalAsync(manifest,input.ReservationReceiptId!,false,token).ConfigureAwait(false);
        else actual=await client.ExecuteLifecycleAsync(input.Registration,input.Lifecycle!,token).ConfigureAwait(false);
        if(actual is null) return null;
        var result=new JsonObject { ["caseId"]=id,["requestId"]=requestId,["actual"]=JsonSerializer.SerializeToNode(actual,CustodyBindingContract.Json) };
        await originals.RetainAsync("executor",requestId+":case-result",result,token).ConfigureAwait(false);return result;
    }
    /// <summary>Retrieves exact native originals without reissuing wrap/unwrap or repeating a manifest. Lost delivery remains unknown until independent holder observation.</summary>
    public async Task<JsonObject?> LookupAsync(JsonObject request,CancellationToken token)
    {
        var input=Input(request);if(input is null) return null;string requestId=request["requestId"]!.GetValue<string>();
        if(await originals.LookupResultAsync("executor",requestId+":case-result",token).ConfigureAwait(false) is { } retained) return retained;
        if(request["caseId"]!.GetValue<string>()=="S3.lifecycle.unwrap") return null;
        object actual=input.Manifest is { } manifest?await client.ExecuteManifestOriginalAsync(manifest,input.ReservationReceiptId!,true,token).ConfigureAwait(false):
            await client.LookupOriginalAsync(input.Registration,input.Lifecycle,token).ConfigureAwait(false);
        if(actual is CustodyKeyLifecycleOutcome { Status:CustodyKeyLifecycleStatus.Unknown or CustodyKeyLifecycleStatus.Unavailable }
            || actual is CustodyManifestOriginal { Result.State:DeletionManifestProviderState.Unknown }) return null;
        var result=new JsonObject { ["caseId"]=request["caseId"]!.DeepClone(),["requestId"]=requestId,["actual"]=JsonSerializer.SerializeToNode(actual,CustodyBindingContract.Json) };
        await originals.RetainAsync("executor",requestId+":case-result",result,token).ConfigureAwait(false);return result;
    }
    /// <summary>Reads current independently authenticated native original only, with no effect admission.</summary>
    public async Task<JsonNode?> ObserveNativeAsync(JsonObject request,CancellationToken token)
    {
        var input=Input(request);if(input is null) return null;
        object actual=input.Manifest is { } manifest?await client.ExecuteManifestOriginalAsync(manifest,input.ReservationReceiptId!,true,token).ConfigureAwait(false):
            await client.LookupOriginalAsync(input.Registration,input.Lifecycle,token).ConfigureAwait(false);
        return JsonSerializer.SerializeToNode(actual,CustodyBindingContract.Json);
    }
    private QualificationLifecycleCaseInput? Input(JsonObject request)
    {
        string? id=SecretsProof.Text(request,"caseId");if(id is null || !Supported(id) || !inputs.TryGetValue(id,out var input) || request["enrolledScope"] is not JsonObject scope
            || QualificationProtocolSchema.Digest(scope)!=input.EnrolledScopeSha256 || scope["tenantId"]?.GetValue<string>()!=input.Registration.Identity.TenantId
            || input.Lifecycle is { } lifecycle && lifecycle.Identity!=input.Registration.Identity) return null;
        if(id is "S3.lifecycle.wrap" or "S3.lifecycle.unwrap") return input.KeyUse is null || input.KeyUse.OriginalRootReference!=input.Registration.OpaqueWrappedKeyReference?null:input;
        if(id is "S3.lifecycle.pin" or "S3.lifecycle.unpin") return input.Lifecycle?.Action!=(id.EndsWith(".pin",StringComparison.Ordinal)?CustodyKeyLifecycleAction.Pin:CustodyKeyLifecycleAction.Unpin)?null:input;
        return input.Manifest is null || string.IsNullOrWhiteSpace(input.ReservationReceiptId) || input.Manifest.Capability.TenantId!=input.Registration.Identity.TenantId
            || !input.Manifest.Targets.Contains(new(input.Registration.Identity.TenantId,input.Registration.Identity.ObjectId,input.Registration.Identity.KeyAlias))?null:input;
    }
    /// <summary>Gets the closed supported lifecycle group.</summary>
    public static bool Supported(string id)=>id is "S3.lifecycle.wrap" or "S3.lifecycle.unwrap" or "S3.lifecycle.pin" or "S3.lifecycle.unpin" or "S3.lifecycle.destroy" or "S3.hold-pin";
}
