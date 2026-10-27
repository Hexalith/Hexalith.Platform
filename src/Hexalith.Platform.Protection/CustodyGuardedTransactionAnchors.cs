using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Exact independently retained transaction proposals and cell/receipt anchors. Restored missing native outcomes remain unknown; this adapter never executes a transaction or enrolls genesis.</summary>
public sealed class CustodyGuardedTransactionAnchors(DaprClient dapr,IQualificationOriginalStore originals,
    IReadOnlyDictionary<string,string> cellScopes,IReadOnlyDictionary<string,string> receiptScopes)
{
    private const string Role="guarded-transaction";
    private readonly ConcurrentDictionary<string,int> _firstSequence=new(StringComparer.Ordinal);
    private sealed record Cell(string CellId,GuardedStateCell? Before,GuardedStateCell After,AnchoredStateTransition Transition);
    private sealed record Proposal(GuardedStateTransactionTarget Target,GuardedStateCommitRequest Request,GuardedStateCommitReceipt Receipt,
        IReadOnlyList<Cell> Cells,AnchoredStateTransition ReceiptTransition);
    private static JsonObject Node<T>(T value)=>(JsonObject)JsonSerializer.SerializeToNode(value,CustodyBindingContract.Json)!;
    private static string Hash<T>(T value)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    private static string CellKey(GuardedStateTransactionTarget target,string id)=>"governed/"+Hash(new[]{target.TenantId,target.InstallationId,"cell",id});
    private static string ReceiptKey(GuardedStateTransactionTarget target,string id)=>"governed/"+Hash(new[]{target.TenantId,target.InstallationId,"outcome",id});
    private static string Reference(string cell,long revision)=>"cell:"+Hash(cell)+":"+revision.ToString(System.Globalization.CultureInfo.InvariantCulture);
    private static IReadOnlyDictionary<string,string> Metadata(GuardedStateTransactionTarget target)=>new Dictionary<string,string>{{"partitionKey",target.PartitionKey}};
    private async Task<Proposal?> Read(string operation,CancellationToken token)
        =>(await originals.LookupRequestAsync(Role,operation,token).ConfigureAwait(false))?.Deserialize<Proposal>(CustodyBindingContract.Json);
    private static bool Exact(Proposal value,GuardedStateTransactionTarget target,GuardedStateCommitRequest request)=>Hash(value.Target)==Hash(target) && Hash(value.Request)==Hash(request);
    /// <summary>Retains the full proposal before any native write and admits its exact independently current transitions.</summary>
    public async Task<bool> AdmitAsync(GuardedStateTransactionTarget target,GuardedStateCommitRequest request,
        IReadOnlyDictionary<string,ICustodyOwnerStateAuthority> cells,IReadOnlyDictionary<string,ICustodyOwnerStateAuthority> receipts,CancellationToken token)
    {
        if(await Read(request.OperationId,token).ConfigureAwait(false) is { } prior)
        {
            if(!Exact(prior,target,request)) return false;
            if(await originals.LookupResultAsync(Role,request.OperationId,token).ConfigureAwait(false) is not null)
                return await ReconcileAsync(target,prior.Receipt,cells,receipts,token).ConfigureAwait(false);
            // The native method calls authorization at entry, just before its single transaction, and after lookup.
            // At most three grants permit that original first sequence. A restart has no first-sequence grant; an unknown dispatch cannot obtain another pair.
            return _firstSequence.TryGetValue(request.OperationId,out int count) && count<3 && _firstSequence.TryUpdate(request.OperationId,count+1,count);
        }
        if(!receiptScopes.TryGetValue(request.OperationId,out var receiptScope) || !receipts.TryGetValue(request.OperationId,out var receiptOwner)) return false;
        var proposed=new List<Cell>();
        foreach(var mutation in new[]{request.Guard}.Concat(request.Targets))
        {
            if(!cellScopes.TryGetValue(mutation.CellId,out string? scope) || !cells.TryGetValue(mutation.CellId,out var owner)) return false;
            var before=await dapr.GetStateAsync<GuardedStateCell>(target.ComponentName,CellKey(target,mutation.CellId),ConsistencyMode.Strong,Metadata(target),token).ConfigureAwait(false);
            if((before?.Revision??0)!=mutation.ExpectedRevision || Convert.ToHexString(SHA256.HashData(before?.Value??[]))!=mutation.ExpectedDigest
                || before is not null && (before.TenantId!=target.TenantId || before.InstallationId!=target.InstallationId || before.CellId!=mutation.CellId)
                || !await owner.ValidateStateAsync(before?.Revision??0,Hash(before),token).ConfigureAwait(false)) return false;
            var after=new GuardedStateCell(target.TenantId,target.InstallationId,mutation.CellId,checked(mutation.ExpectedRevision+1),mutation.NextValue.ToArray());
            proposed.Add(new(mutation.CellId,before,after,RecoverableAnchoredState.Prepare(scope,before?.Revision??0,after.Revision,before,after)));
        }
        var receipt=new GuardedStateCommitReceipt(target.TenantId,target.InstallationId,request.OperationId,Hash(request),checked(request.Guard.ExpectedRevision+1),Hash(new[]{target.InstallationId,request.OperationId,Hash(request)}))
            { LogicalIntentDigest=request.LogicalIntentDigest,Outcome=request.Outcome.ToArray(),LogicalGuardHighWater=request.LogicalGuardHighWater==0?checked(request.Guard.ExpectedRevision+1):request.LogicalGuardHighWater };
        if(!await receiptOwner.ValidateStateAsync(0,Hash<object?>(null),token).ConfigureAwait(false)) return false;
        var plan=new Proposal(target,request,receipt,proposed,RecoverableAnchoredState.Prepare(receiptScope,0,1,(object?)null,receipt));
        if(!await originals.BeginAsync(Role,request.OperationId,Node(plan),token).ConfigureAwait(false)) return false;
        foreach(var cell in proposed)
        {
            await originals.BeginFinalAsync(Role,Reference(cell.CellId,cell.After.Revision),Node(new { operationId=request.OperationId,proposalSha256=Hash(plan) }),token).ConfigureAwait(false);
            if(!await cells[cell.CellId].AdmitTransitionAsync(cell.Transition,token).ConfigureAwait(false)) return false;
        }
        if(!await receiptOwner.AdmitTransitionAsync(plan.ReceiptTransition,token).ConfigureAwait(false)) return false;
        return _firstSequence.TryAdd(request.OperationId,1);
    }
    /// <summary>Correlates the exact immutable proposal, actual native receipt and every actual native cell before completing any anchor.</summary>
    public async Task<bool> ReconcileAsync(GuardedStateTransactionTarget target,GuardedStateCommitReceipt receipt,
        IReadOnlyDictionary<string,ICustodyOwnerStateAuthority> cells,IReadOnlyDictionary<string,ICustodyOwnerStateAuthority> receipts,CancellationToken token)
    {
        var plan=await Read(receipt.OperationId,token).ConfigureAwait(false);
        if(plan is null || Hash(plan.Target)!=Hash(target) || Hash(plan.Receipt)!=Hash(receipt) || !receipts.TryGetValue(receipt.OperationId,out var receiptOwner)) return false;
        if(await originals.LookupResultAsync(Role,receipt.OperationId,token).ConfigureAwait(false) is { } completed)
            return Hash(completed.Deserialize<GuardedStateCommitReceipt>(CustodyBindingContract.Json))==Hash(receipt)
                && await receiptOwner.VerifyTransitionAsync(plan.ReceiptTransition,token).ConfigureAwait(false)
                && await receiptOwner.ValidateStateAsync(plan.ReceiptTransition.TargetRevision,Hash(receipt),token).ConfigureAwait(false);
        var nativeReceipt=await dapr.GetStateAsync<GuardedStateCommitReceipt>(target.ComponentName,ReceiptKey(target,receipt.OperationId),ConsistencyMode.Strong,Metadata(target),token).ConfigureAwait(false);
        if(Hash(nativeReceipt)!=Hash(plan.Receipt)) return false;
        foreach(var cell in plan.Cells)
        {
            if(!cells.TryGetValue(cell.CellId,out var owner) || !cellScopes.TryGetValue(cell.CellId,out string? scope) || cell.Transition.ScopeId!=scope) return false;
            var actual=await dapr.GetStateAsync<GuardedStateCell>(target.ComponentName,CellKey(target,cell.CellId),ConsistencyMode.Strong,Metadata(target),token).ConfigureAwait(false);
            if(Hash(actual)!=Hash(cell.After)) return false;
        }
        async Task<bool> Complete(ICustodyOwnerStateAuthority owner,AnchoredStateTransition transition)
        {
            if(!await owner.VerifyTransitionAsync(transition,token).ConfigureAwait(false))
            {
                if(!await owner.RecordTransitionAsync(transition,token).ConfigureAwait(false)
                    && !await owner.RecoverTransitionAsync(transition,token).ConfigureAwait(false)) return false;
            }
            return await owner.VerifyTransitionAsync(transition,token).ConfigureAwait(false)
                && await owner.ValidateStateAsync(transition.TargetRevision,transition.TargetDigest,token).ConfigureAwait(false);
        }
        foreach(var cell in plan.Cells) if(!await Complete(cells[cell.CellId],cell.Transition).ConfigureAwait(false)) return false;
        if(!await Complete(receiptOwner,plan.ReceiptTransition).ConfigureAwait(false)) return false;
        await originals.RetainAsync(Role,receipt.OperationId,Node(receipt),token).ConfigureAwait(false);_firstSequence.TryRemove(receipt.OperationId,out _);return true;
    }
    /// <summary>Prevents a partially anchored transaction or restored preimage from becoming an independently current cell read.</summary>
    public async Task<bool> ValidateCorrelationAsync(GuardedStateTransactionTarget target,string id,GuardedStateCell? cell,
        IReadOnlyDictionary<string,ICustodyOwnerStateAuthority> cells,IReadOnlyDictionary<string,ICustodyOwnerStateAuthority> receipts,CancellationToken token)
    {
        foreach(long revision in new[]{cell?.Revision??0,checked((cell?.Revision??0)+1)})
        {
            var lookup=await originals.ReadOriginalAsync(Role,Reference(id,revision),token).ConfigureAwait(false);if(!lookup.Known) return false;
            var reference=lookup.Request;if(reference is null) continue;
            string operation=reference["operationId"]!.GetValue<string>();var plan=await Read(operation,token).ConfigureAwait(false);
            if(plan is null || reference["proposalSha256"]?.GetValue<string>()!=Hash(plan)) return false;
            var proposed=plan.Cells.SingleOrDefault(value=>value.CellId==id);if(proposed is null) return false;
            if(_firstSequence.TryGetValue(operation,out int count) && count==1 && Hash(cell)==Hash(proposed.Before)) continue;
            if(!await ReconcileAsync(target,plan.Receipt,cells,receipts,token).ConfigureAwait(false)) return false;
        }
        return true;
    }
}
