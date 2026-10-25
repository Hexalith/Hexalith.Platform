using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Actual typed guard/protection/signer/native source reader. It accepts no owner outcome flags or fabricated receipt vectors.</summary>
public sealed class QualificationTypedOwnerReader(IGovernanceScopeGuard guard,IDeletionProtectionOwner protection,IDeletionCapabilitySigningActor signer,
    QualificationNativeSourceReader native,IndependentDaprAuthority originals,IReadOnlyDictionary<string,QualificationOwnerReadScope> enrolledScopes) : IQualificationObservedOriginalReader
{
    /// <inheritdoc />
    public async Task<QualificationObservedOriginal?> ReadAsync(JsonObject request,CancellationToken cancellationToken)
    {
        string? caseId=SecretsProof.Text(request,"caseId"),requestId=SecretsProof.Text(request,"requestId");
        if(caseId is null || requestId is null || !enrolledScopes.TryGetValue(caseId,out var scope) || request["enrolledScope"] is not JsonObject enrolled
            || SecretsProof.Text(enrolled,"tenantId")!=scope.TenantId || SecretsProof.Text(enrolled,"batchId")!=scope.BatchId) return null;
        var original=await originals.InvokeAsync<IndependentOriginal>("qualification-lookup-original",new { role="executor",requestId=requestId+":case-result" },cancellationToken).ConfigureAwait(false);
        if(original?.Result is not { } originalResult) return null;
        var guardState=await guard.ReadAsync(scope.TenantId,cancellationToken).ConfigureAwait(false);
        var protectedBatch=await protection.LookupAsync(scope.TenantId,scope.BatchId,cancellationToken).ConfigureAwait(false);
        var signing=scope.Signing is null?null:await signer.LookupAsync(scope.Signing).WaitAsync(cancellationToken).ConfigureAwait(false);
        var streams=await native.ReadAsync(scope.NativeStreams,cancellationToken).ConfigureAwait(false);
        if(guardState is null || guardState.TenantId!=scope.TenantId || protectedBatch.TenantId!=scope.TenantId || protectedBatch.BatchId!=scope.BatchId
            || streams is null || signing is not null && signing.Payload!=scope.Signing) return null;
        var deletion=guardState.Deletions.SingleOrDefault(value=>value.RequestId==scope.DeletionRequestId);
        if(deletion is null) return null;
        var source=(JsonObject)JsonSerializer.SerializeToNode(new { originalResult,guard=guardState,protection=protectedBatch,signing,streams },CustodyBindingContract.Json)!;
        string receipt=Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(originalResult,CustodyBindingContract.Json)));
        return new(requestId,receipt,guardState.InstallationId+":"+guardState.Revision,source);
    }
}
