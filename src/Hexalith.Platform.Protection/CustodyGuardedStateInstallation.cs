using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Current independently anchored installed target/cell adapter. It creates no cells or revision-zero enrollment and cannot repair an absent restore anchor.</summary>
public sealed class CustodyGuardedStateInstallation(GuardedStateTransactionTarget enrolledTarget,long targetRevision,
    ICustodyOwnerStateAuthority targetAuthority,IReadOnlyDictionary<string,ICustodyOwnerStateAuthority> cellAuthorities,
    IReadOnlyDictionary<string,ICustodyOwnerStateAuthority> receiptAuthorities,TimeProvider clock,CustodyGuardedTransactionAnchors? transactionAnchors=null):IGuardedStateInstallation
{
    /// <summary>Names the actual installed inputs required to retain proposals and publish exact cell/receipt anchors.</summary>
    public static IReadOnlyList<string> MissingCommitInputs { get; }=["IndependentGuardedTransactionOriginalStoreAndExactCellReceiptScopes"];
    /// <inheritdoc />
    public async Task<GuardedStateTransactionTarget?> GetCurrentAsync(string tenantId,CancellationToken token)
    {
        if(tenantId!=enrolledTarget.TenantId || targetRevision<=0 || enrolledTarget.ObservedAt>clock.GetUtcNow() || enrolledTarget.ValidUntil<=clock.GetUtcNow()
            || !cellAuthorities.ContainsKey(enrolledTarget.GuardCellId) || string.IsNullOrWhiteSpace(enrolledTarget.WriterRevocationReceipt)) return null;
        return await targetAuthority.ValidateStateAsync(targetRevision,Hash(enrolledTarget),token).ConfigureAwait(false)
            && await targetAuthority.AuthorizeOperationAsync(Hash(enrolledTarget),"ReadGuardedInstallation",token).ConfigureAwait(false)?enrolledTarget:null;
    }
    private async Task<bool> Current(GuardedStateTransactionTarget target,CancellationToken token)=>target==enrolledTarget && await GetCurrentAsync(target.TenantId,token).ConfigureAwait(false)==target;
    /// <inheritdoc />
    public async Task<bool> AuthorizeReadAsync(GuardedStateTransactionTarget target,string cellId,CancellationToken token)
        =>await Current(target,token).ConfigureAwait(false) && cellAuthorities.TryGetValue(cellId,out var owner)
            && await owner.AuthorizeOperationAsync(Hash(new { target.TenantId,target.InstallationId,cellId }),"ReadGuardedCell",token).ConfigureAwait(false);
    /// <inheritdoc />
    public async Task<bool> AuthorizeLookupAsync(GuardedStateTransactionTarget target,string operationId,string logicalIntentDigest,CancellationToken token)
        =>await Current(target,token).ConfigureAwait(false) && receiptAuthorities.TryGetValue(operationId,out var owner)
            && await owner.AuthorizeOperationAsync(Hash(new { target.TenantId,target.InstallationId,operationId,logicalIntentDigest }),"LookupGuardedOriginal",token).ConfigureAwait(false);
    /// <inheritdoc />
    public async Task<bool> AuthorizeAsync(GuardedStateTransactionTarget target,GuardedStateCommitRequest request,string requestDigest,string operation,CancellationToken token)
    {
        // Permission-only admission cannot write unanchored cells. The exact retained proposal and its once-only first sequence are mandatory for a new commit.
        if(operation=="Commit" && transactionAnchors is null) return false;
        if(!await Current(target,token).ConfigureAwait(false) || request.TenantId!=target.TenantId || request.Guard.CellId!=target.GuardCellId
            || request.Guard.ExpectedRevision<=0 || request.Targets.Count>1000 || request.Targets.Any(value=>!cellAuthorities.ContainsKey(value.CellId))
            || !receiptAuthorities.ContainsKey(request.OperationId) || !string.Equals(requestDigest,Hash(request),StringComparison.OrdinalIgnoreCase)) return false;
        return await targetAuthority.AuthorizeOperationAsync(Hash(new { target.TenantId,target.InstallationId,request.OperationId,requestDigest,operation }),"CommitGuardedTransaction",token).ConfigureAwait(false)
            && (operation!="Commit" || await transactionAnchors!.AdmitAsync(target,request,cellAuthorities,receiptAuthorities,token).ConfigureAwait(false));
    }
    /// <inheritdoc />
    public async Task<GuardedCellHistory> ReadCellAsync(GuardedStateTransactionTarget target,string cellId,GuardedStateCell? cell,CancellationToken token)
    {
        if(!await AuthorizeReadAsync(target,cellId,token).ConfigureAwait(false) || !cellAuthorities.TryGetValue(cellId,out var owner)) return GuardedCellHistory.Unknown;
        if(cell is not null && (cell.TenantId!=target.TenantId || cell.InstallationId!=target.InstallationId || cell.CellId!=cellId || cell.Revision<=0)) return GuardedCellHistory.Unknown;
        // A null cell passes only an independently installed exact revision-zero absence anchor. Restored missing state has no matching current anchor.
        bool exact=await owner.ValidateStateAsync(cell?.Revision??0,Hash(cell),token).ConfigureAwait(false);
        if(transactionAnchors is not null && !await transactionAnchors.ValidateCorrelationAsync(target,cellId,cell,cellAuthorities,receiptAuthorities,token).ConfigureAwait(false)) return GuardedCellHistory.Unknown;
        return exact?cell is null?GuardedCellHistory.NeverWritten:GuardedCellHistory.Occupied:cell is null?GuardedCellHistory.RestoredAbsent:GuardedCellHistory.Unknown;
    }
    /// <inheritdoc />
    public async Task<bool> ValidateReceiptAsync(GuardedStateTransactionTarget target,GuardedStateCommitReceipt receipt,CancellationToken token)
        =>await Current(target,token).ConfigureAwait(false) && receipt.TenantId==target.TenantId && receipt.InstallationId==target.InstallationId
            && receipt.GuardRevision>0 && receiptAuthorities.TryGetValue(receipt.OperationId,out var owner)
            && (transactionAnchors is null || await transactionAnchors.ReconcileAsync(target,receipt,cellAuthorities,receiptAuthorities,token).ConfigureAwait(false))
            && await owner.ValidateStateAsync(transactionAnchors is null?receipt.GuardRevision:1,Hash(receipt),token).ConfigureAwait(false)
            && await owner.AuthorizeOperationAsync(Hash(receipt),"ReadGuardedReceipt",token).ConfigureAwait(false);
    private static string Hash<T>(T value)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
}
