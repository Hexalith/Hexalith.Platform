using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Reads exact independently published source/policy/lookup/signing evidence from enrolled Dapr keys, with current custody authority before and after. No evidence is inferred from configuration flags.</summary>
public sealed class CustodyGovernanceGuardInstallation(DaprClient dapr,string independentComponent,
    IReadOnlyDictionary<string,QualificationGuardEvidenceEnrollment> enrolledEvidence,
    IReadOnlyDictionary<string,ICustodyOwnerStateAuthority> independentOwners,TimeProvider clock):IGovernanceGuardInstallation
{
    /// <inheritdoc />
    public Task<GovernanceGuardEvidence?> ReadLookupAsync(GovernanceGuardTransition transition,string intentDigest,string targetMutationDigest,CancellationToken token)
        =>ReadEvidence(transition,intentDigest,targetMutationDigest,"LookupGovernanceOriginal",token);
    /// <inheritdoc />
    public Task<GovernanceGuardEvidence?> ReadAsync(GovernanceGuardTransition transition,string intentDigest,string targetMutationDigest,TenantGovernanceGuardState state,CancellationToken token)
        =>state.TenantId==transition.TenantId?ReadEvidence(transition,intentDigest,targetMutationDigest,"ReadGovernanceSourcePolicy",token):Task.FromResult<GovernanceGuardEvidence?>(null);
    private async Task<GovernanceGuardEvidence?> ReadEvidence(GovernanceGuardTransition transition,string intentDigest,string mutationDigest,string operation,CancellationToken token)
    {
        var result=await Read<GovernanceGuardEvidence>(transition.OperationId,operation,token).ConfigureAwait(false);
        return result is not null && result.TenantId==transition.TenantId && result.IntentDigest==intentDigest && result.TargetMutationDigest==mutationDigest
            && result.ObservedAt<=clock.GetUtcNow() && result.ValidUntil>clock.GetUtcNow()?result:null;
    }
    /// <inheritdoc />
    public async Task<GovernanceSigningRecovery?> ReadSigningRecoveryAsync(GovernanceProtocolReceipt noIssue,TenantGovernanceGuardState state,CancellationToken token)
    {
        var result=await Read<GovernanceSigningRecovery>(noIssue.OperationId+":signing-recovery","ReadGovernanceSigningRecovery",token).ConfigureAwait(false);
        return result is not null && result.NoIssueReceiptId==noIssue.ReceiptId && result.SigningRequestId==noIssue.SigningRequestId && result.DetachedJwsDigest==noIssue.DetachedJwsDigest
            && result.CurrentGuardRevision==state.Revision && result.ObservedAt<=clock.GetUtcNow() && result.ValidUntil>clock.GetUtcNow()?result:null;
    }
    private async Task<T?> Read<T>(string id,string operation,CancellationToken token) where T:class
    {
        if(!enrolledEvidence.TryGetValue(id,out var resource) || !independentOwners.TryGetValue(id,out var owner) || resource.Revision<=0
            || !await owner.AuthorizeOperationAsync(id,operation,token).ConfigureAwait(false)) return null;
        var read=await dapr.GetStateAsync<T>(independentComponent,resource.StateKey,metadata:resource.Metadata,cancellationToken:token).WaitAsync(token).ConfigureAwait(false);
        if(read is null || !await owner.ValidateStateAsync(resource.Revision,Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(read))),token).ConfigureAwait(false)
            || !await owner.AuthorizeOperationAsync(id,operation,token).ConfigureAwait(false)) return null;
        return read;
    }
}
