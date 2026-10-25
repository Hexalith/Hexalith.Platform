using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Client;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Narrow production credential read: one independently enrolled exact version/field, current authority before and after, no enumeration or credential output.</summary>
public sealed class QualificationProviderCredentialWorkflow(DaprClient client,IPlatformKeyInventoryActor inventory,IPlatformKeyInventoryAuthority authority,QualificationProviderCredentialInput input)
{
    /// <summary>Registers the concrete source under its only permitted case.</summary>
    public QualificationSourceWorkflow Registration(IQualificationOriginalStore originals)=>QualificationRuntimeWorkflowCatalog.Retained("S1.exact-provider-credential",originals,ResolveAsync);
    /// <summary>Resolves one exact opaque credential reference and immediately discards its API response.</summary>
    public async Task<JsonObject?> ResolveAsync(JsonObject original,CancellationToken cancellationToken)
    {
        if(SecretsProof.Text(original,"caseId")!="S1.exact-provider-credential" || original["enrolledScope"] is not JsonObject scope || QualificationProtocolSchema.Digest(scope)!=input.EnrolledScopeSha256
            || SecretsProof.Text(scope,"tenantId")!=input.Key.TenantId || input.Key.Purpose!=PlatformKeyPurpose.ProviderCredential
            || new[]{input.SecretStore,input.SecretReference,input.SecretField,input.SecretVersion}.Any(value=>!SecretsProvisioning.IsReference(value))) return null;
        using var budget=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);budget.CancelAfter(TimeSpan.FromSeconds(30));
        async Task<PlatformKeyInventorySnapshot?> Current()
        {
            if(!await authority.AuthorizeOperationAsync(input.Key,"ResolveProviderCredential",budget.Token).ConfigureAwait(false)) return null;
            var state=await inventory.ReadAsync(input.Key).WaitAsync(budget.Token).ConfigureAwait(false);
            return state.TenantId==input.Key.TenantId && state.Purpose==input.Key.Purpose && state.KeyAlias==input.Key.KeyAlias && state.Versions.Count(value=>value.Key==input.Key && value.State==PlatformHmacKeyState.Active)==1
                && await authority.ValidateStateAsync(input.Key,state.Revision,CustodyBindingContract.Hash(JsonSerializer.SerializeToUtf8Bytes(state)),budget.Token).ConfigureAwait(false)?state:null;
        }
        var before=await Current().ConfigureAwait(false);if(before is null) return null;
        Dictionary<string,string>? response=null;int referenceCount;
        try
        {
            var pending=client.GetSecretAsync(input.SecretStore,input.SecretReference,new Dictionary<string,string>{{"version_id",input.SecretVersion}},budget.Token);
            try { response=await pending.WaitAsync(budget.Token).ConfigureAwait(false); }
            catch
            {
                _=pending.ContinueWith(task=> { if(task.Status==TaskStatus.RanToCompletion) task.Result.Clear();else _=task.Exception; },CancellationToken.None,TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);
                throw;
            }
            referenceCount=response.Count;
            if(referenceCount!=1 || !response.TryGetValue(input.SecretField,out string? credential) || string.IsNullOrWhiteSpace(credential)) return null;
            // Dapr returns immutable strings; discard the sole response immediately. It is never hashed, cached, logged or serialized.
        }
        finally { response?.Clear(); }
        var after=await Current().ConfigureAwait(false);
        if(after is null || !JsonNode.DeepEquals(JsonSerializer.SerializeToNode(before),JsonSerializer.SerializeToNode(after))) return null;
        return new JsonObject { ["requestId"]=original["requestId"]!.DeepClone(),["caseId"]="S1.exact-provider-credential",["outcome"]="Resolved",["exactResolvedReferenceCount"]=referenceCount,["inventoryRevision"]=after.Revision };
    }
}
