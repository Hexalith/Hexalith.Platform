using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Actual metadata rotation, exact append-only signal lookup, retained-version digest behavior and selected real invalidation consumer/read sequencing.</summary>
public sealed class QualificationInventoryRotationWorkflow(IPlatformKeyInventoryActor inventory,IPlatformKeyInventoryAuthority authority,PlatformHmacService digests,
    IQualificationOriginalStore originals,QualificationInventoryRotationInput input,IQualificationSafetyInvalidationPort? invalidation)
{
    /// <summary>Gets the closed executable registration; the absent safety consumer/protocol cannot be replaced by an install flag.</summary>
    public QualificationSourceWorkflow Registration=>new("S1.rotation-invalidation",ExecuteAsync,LookupAsync,PreflightAsync);
    /// <summary>Names missing selected controls before changing even isolated actor metadata.</summary>
    public async Task<IReadOnlyList<string>> PreflightAsync(JsonObject original,CancellationToken token)
    {
        var old=input.InstallOriginal;var next=input.InstallSuccessor;
        if(original["caseId"]?.GetValue<string>()!="S1.rotation-invalidation" || original["enrolledScope"] is not JsonObject scope || QualificationProtocolSchema.Digest(scope)!=input.EnrolledScopeSha256
            || old.Action!=PlatformKeyInventoryAction.InstallCurrent || next.Action!=PlatformKeyInventoryAction.InstallCurrent || old.Key.TenantId!=next.Key.TenantId
            || old.Key.Purpose!=next.Key.Purpose || old.Key.KeyAlias!=next.Key.KeyAlias || old.Key.Version==next.Key.Version || next.ExpectedInventoryRevision!=old.ExpectedInventoryRevision+1
            || input.DigestScope.TenantId!=old.Key.TenantId || scope["tenantId"]?.GetValue<string>()!=old.Key.TenantId) return ["ExactIndependentlyProvisionedRotationArguments"];
        return invalidation is null?["SelectedSafetyVerdictInvalidationConsumerAndNativeReader","SelectedNativeKeyVersionRotationControl"]:
            await invalidation.PreflightAsync(input,token).ConfigureAwait(false);
    }
    /// <summary>Executes only exact admitted source steps. A previously admitted unknown step uses actor/native original lookup and cannot resend.</summary>
    public async Task<JsonObject?> ExecuteAsync(JsonObject original,CancellationToken token)
    {
        if((await PreflightAsync(original,token).ConfigureAwait(false)).Count!=0) return null;
        string requestId=original["requestId"]!.GetValue<string>();
        await originals.BeginAsync("executor",requestId+":case-result",original,token).ConfigureAwait(false);
        if(await LookupAsync(original,token).ConfigureAwait(false) is { } existing) return existing;
        async Task<PlatformKeyInventorySignal?> Change(PlatformKeyInventoryChange change,string suffix)
        {
            string operation=requestId+suffix;var raw=(JsonObject)JsonSerializer.SerializeToNode(change,CustodyBindingContract.Json)!;
            bool first=await originals.BeginAsync("executor",operation,raw,token).ConfigureAwait(false);
            var signal=first?await inventory.ApplyAsync(change).WaitAsync(token).ConfigureAwait(false):
                (await inventory.ReadAsync(change.Key).WaitAsync(token).ConfigureAwait(false)).Signals.SingleOrDefault(value=>value.OperationId==change.OperationId);
            string digest=CustodyBindingContract.Hash(JsonSerializer.SerializeToUtf8Bytes(change));
            if(signal is null || signal.Key!=change.Key || signal.Action!=change.Action || signal.RequestDigest!=digest || signal.InventoryRevision!=change.ExpectedInventoryRevision+1) return null;
            await originals.RetainAsync("executor",operation,(JsonObject)JsonSerializer.SerializeToNode(signal,CustodyBindingContract.Json)!,token).ConfigureAwait(false);return signal;
        }
        if(await Change(input.InstallOriginal,":install-original").ConfigureAwait(false) is null) return null;
        string digestId=requestId+":original-digest";
        bool firstDigest=await originals.BeginAsync("executor",digestId,original,token).ConfigureAwait(false);
        if(firstDigest)
        {
            var before=await digests.DigestAsync(input.DigestScope,input.DigestComponents,input.InstallOriginal.Key.Version,token).ConfigureAwait(false);
            if(before.Status!=CustodyStatus.Succeeded) return null;
            await originals.RetainAsync("executor",digestId,(JsonObject)JsonSerializer.SerializeToNode(before,CustodyBindingContract.Json)!,token).ConfigureAwait(false);
        }
        var retained=await originals.LookupResultAsync("executor",digestId,token).ConfigureAwait(false);if(retained is null) return null;
        var signal=await Change(input.InstallSuccessor,":install-successor").ConfigureAwait(false);if(signal is null) return null;
        string consumeId=requestId+":invalidate";
        bool first=await originals.BeginAsync("executor",consumeId,(JsonObject)JsonSerializer.SerializeToNode(signal,CustodyBindingContract.Json)!,token).ConfigureAwait(false);
        string? receipt=first?await invalidation!.PublishAsync(signal,token).ConfigureAwait(false):await invalidation!.LookupAsync(signal,token).ConfigureAwait(false);
        if(receipt is not { Length:64 }) return null;
        var read=await ObserveAsync(original,token).ConfigureAwait(false);if(read is null) return null;
        read["invalidationReceiptSha256"]=receipt;read["originalDigest"]=retained.DeepClone();
        await originals.RetainAsync("executor",requestId+":case-result",read,token).ConfigureAwait(false);return read;
    }
    /// <summary>Reads actual current versions/signals, retained old digest and real native consumer verdicts. It performs no rotation or invalidation.</summary>
    public async Task<JsonObject?> ObserveAsync(JsonObject original,CancellationToken token)
    {
        if(invalidation is null || !await authority.AuthorizeOperationAsync(input.InstallSuccessor.Key,"ReadKeyInventory",token).ConfigureAwait(false)) return null;
        var state=await inventory.ReadAsync(input.InstallSuccessor.Key).WaitAsync(token).ConfigureAwait(false);
        if(!await authority.ValidateStateAsync(input.InstallSuccessor.Key,state.Revision,CustodyBindingContract.Hash(JsonSerializer.SerializeToUtf8Bytes(state)),token).ConfigureAwait(false)) return null;
        var retained=await digests.DigestAsync(input.DigestScope,input.DigestComponents,input.InstallOriginal.Key.Version,token).ConfigureAwait(false);
        var verdicts=await invalidation.ReadAsync(input.InstallSuccessor.Key,token).ConfigureAwait(false);if(verdicts is null) return null;
        return new JsonObject { ["caseId"]="S1.rotation-invalidation",["requestId"]=original["requestId"]!.DeepClone(),["inventory"]=JsonSerializer.SerializeToNode(state,CustodyBindingContract.Json),
            ["retainedDigest"]=JsonSerializer.SerializeToNode(retained,CustodyBindingContract.Json),["safetyVerdicts"]=JsonSerializer.SerializeToNode(verdicts,CustodyBindingContract.Json) };
    }
    /// <summary>Reads only the exact original full case result after an unknown source effect.</summary>
    public Task<JsonObject?> LookupAsync(JsonObject original,CancellationToken token)=>originals.LookupResultAsync("executor",original["requestId"]!.GetValue<string>()+":case-result",token);
}
