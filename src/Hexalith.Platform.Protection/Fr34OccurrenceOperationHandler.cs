using System.Text.Json;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Authenticated receiving boundary for the actual bounded independent recipe producer. Enrollment and native holder evidence must be supplied independently.</summary>
public sealed class Fr34OccurrenceOperationHandler(Fr34OccurrenceRecipe recipe,Fr34OccurrenceProducer producer,TimeProvider clock):IIndependentOwnerOperationHandler
{
    /// <inheritdoc />
    public bool IsReadOnly(string method)=>method is "fr34-pending-material" or "fr34-read-erasure";
    /// <inheritdoc />
    public Task<object?> RecoverAsync(string method,JsonElement payload,IndependentCallerGrant caller,IndependentAuthorityState current,CancellationToken token)
        =>HandleAsync(method,payload,caller,current,token);
    /// <inheritdoc />
    public IReadOnlySet<string> Methods { get; }=new HashSet<string>(["fr34-resolve-occurrence","fr34-occurrence-lease","fr34-material-use","fr34-material-release","fr34-pending-material","fr34-read-erasure"],StringComparer.Ordinal);
    /// <inheritdoc />
    public async Task<object?> HandleAsync(string method,JsonElement payload,IndependentCallerGrant caller,IndependentAuthorityState current,CancellationToken token)
    {
        if(caller.Role!="fr34-recipes" || caller.ScopeReference!=recipe.Target.TenantId+"|fr34" || caller.Revoked || caller.ValidUntil<=clock.GetUtcNow()
            || current.ValidUntil<=clock.GetUtcNow() || current.Fr34Target!=recipe.Target || current.Fr34RecipeSha256!=QualificationProtocolSchema.Digest(JsonSerializer.SerializeToNode(recipe,CustodyBindingContract.Json)!) || !Methods.Contains(method)) throw new InvalidOperationException("CurrentIndependentBoundedFr34RecipeScope");
        switch(method)
        {
            case "fr34-resolve-occurrence": var resolve=payload.Deserialize<Resolve>(CustodyBindingContract.Json)!;return await producer.ResolveAsync(resolve.owner,resolve.occurrence,token).ConfigureAwait(false);
            case "fr34-occurrence-lease": var lease=payload.Deserialize<Lease>(CustodyBindingContract.Json)!;return new { Allowed=await producer.AcquireLeaseAsync(lease.binding,lease.completion,token).ConfigureAwait(false) };
            case "fr34-material-use": var material=payload.Deserialize<Material>(CustodyBindingContract.Json)!;return await producer.MaterialAsync(material.identity,material.writing,token).ConfigureAwait(false);
            case "fr34-material-release": var release=payload.Deserialize<Release>(CustodyBindingContract.Json)!;return new { Allowed=await producer.AdmitReleaseAsync(release.identity,release.profile,token).ConfigureAwait(false) };
            case "fr34-pending-material": return await producer.PendingAsync(token).ConfigureAwait(false);
            case "fr34-read-erasure": return await producer.ReadErasureAsync(payload.GetProperty("binding").Deserialize<InteractionProtectionBinding>(CustodyBindingContract.Json)!,token).ConfigureAwait(false);
            default: return null;
        }
    }
    private sealed record Resolve(AggregateIdentity owner,PayloadProtectionOccurrenceContext occurrence);
    private sealed record Lease(InteractionProtectionBinding binding,bool completion);
    private sealed record Material(InteractionOccurrenceIdentity identity,bool writing);
    private sealed record Release(InteractionOccurrenceIdentity identity,CustodyInteractionMaterialProfile profile);
}
