using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Private current-authenticated client of Fr34OccurrenceOperationHandler; contains no native credentials, keys or enrollment authority.</summary>
public sealed class DaprFr34OccurrenceEnrollment(IndependentDaprAuthority producer):IFr34OccurrenceEnrollment
{
    /// <inheritdoc />
    public Task<Fr34OccurrenceClaim?> ResolveAsync(AggregateIdentity owner,PayloadProtectionOccurrenceContext occurrence,CancellationToken token)=>producer.InvokeAsync<Fr34OccurrenceClaim>("fr34-resolve-occurrence",new { owner,occurrence },token);
    /// <inheritdoc />
    public async Task<bool> AcquireLeaseAsync(InteractionProtectionBinding binding,bool completion,CancellationToken token)=>(await producer.InvokeAsync<Permission>("fr34-occurrence-lease",new { binding,completion },token).ConfigureAwait(false))?.Allowed==true;
    /// <inheritdoc />
    public Task<CustodyInteractionMaterialProfile?> MaterialAsync(InteractionOccurrenceIdentity identity,bool writing,CancellationToken token)=>producer.InvokeAsync<CustodyInteractionMaterialProfile>("fr34-material-use",new { identity,writing },token);
    /// <inheritdoc />
    public async Task<bool> AdmitReleaseAsync(InteractionOccurrenceIdentity identity,CustodyInteractionMaterialProfile profile,CancellationToken token)=>(await producer.InvokeAsync<Permission>("fr34-material-release",new { identity,profile },token).ConfigureAwait(false))?.Allowed==true;
    /// <inheritdoc />
    public async Task<IReadOnlyList<CustodyInteractionMaterialProfile>> PendingAsync(CancellationToken token)=>await producer.InvokeAsync<CustodyInteractionMaterialProfile[]>("fr34-pending-material",new { },token).ConfigureAwait(false)??[];
    private sealed record Permission(bool Allowed);
}
