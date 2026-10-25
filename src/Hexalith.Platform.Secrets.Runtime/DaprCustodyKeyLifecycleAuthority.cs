using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Registration locator. Local data never substitutes for the independent custody original.</summary>
public interface ICustodyKeyRegistrationSource
{
    /// <summary>Locates the retained complete registration for an exact identity.</summary>
    Task<CustodyKeyRegistration?> FindAsync(CustodyKeyObjectIdentity identity, CancellationToken cancellationToken);
}

/// <summary>Production authority over independently enrolled Dapr owner state and authenticated originals; no local journal/witness authority.</summary>
public sealed class DaprCustodyKeyLifecycleAuthority(string tenantId, ICustodyOwnerStateAuthority owner,
    CustodyLifecycleClient client, ICustodyKeyLifecycleProvider lifecycle, ICustodyKeyRegistrationSource registrations) : ICustodyKeyLifecycleAuthority
{
    /// <inheritdoc />
    public Task<bool> AuthorizeOperationAsync(CustodyKeyObjectIdentity identity, string operationId, string method, string requestDigest,
        CancellationToken cancellationToken = default)
        => InvokeAsync(budget => identity.TenantId == tenantId && CustodyBindingContract.Digest(requestDigest)
            ? owner.AuthorizeOperationAsync(CustodyBindingContract.Hash(JsonSerializer.SerializeToUtf8Bytes(new { identity, operationId, requestDigest })), method, budget.Token)
            : Task.FromResult(false), cancellationToken);
    /// <inheritdoc />
    public Task<bool> VerifyRegistrationAsync(CustodyKeyRegistration registration, CancellationToken cancellationToken = default)
        => InvokeAsync(async budget =>
    {
        if (registration.Identity.TenantId != tenantId) return false;
        var result = await client.LifecycleAsync(registration, null, true, budget).WaitAsync(budget.Token);
        budget.Check();
        return result.Status == CustodyKeyLifecycleStatus.Wrapped && result.ReceiptId == registration.WrapReceiptId;
    }, cancellationToken);
    /// <inheritdoc />
    public Task<bool> AuthorizeEffectAsync(CustodyKeyLifecycleRequest request, CancellationToken cancellationToken = default)
        => AuthorizeOperationAsync(request.Identity, request.OperationId, "custody-lifecycle/" + request.Action,
            CustodyBindingContract.Hash(JsonSerializer.SerializeToUtf8Bytes(request)), cancellationToken);
    /// <inheritdoc />
    public Task<bool> VerifyOutcomeAsync(CustodyKeyLifecycleRequest request, CustodyKeyLifecycleOutcome outcome, CancellationToken cancellationToken = default)
        => InvokeAsync(async budget =>
    {
        if (request.Identity.TenantId != tenantId || request.Identity != outcome.Identity || request.OperationId != outcome.OperationId) return false;
        var registration = await registrations.FindAsync(request.Identity, budget.Token).WaitAsync(budget.Token);
        budget.Check();
        if (registration is null) return false;
        var original = await lifecycle.LookupAsync(registration, request, outcome.RequestDigest, budget.Token).WaitAsync(budget.Token);
        budget.Check();
        return original == outcome && original.Status is CustodyKeyLifecycleStatus.Pinned or CustodyKeyLifecycleStatus.Unpinned or CustodyKeyLifecycleStatus.Destroyed or CustodyKeyLifecycleStatus.NotPerformed;
    }, cancellationToken);
    /// <inheritdoc />
    public Task<bool> ValidateStateAsync(string tenant, long revision, string exactStateDigest, CancellationToken cancellationToken = default)
        => InvokeAsync(budget => tenant == tenantId ? owner.ValidateStateAsync(revision, exactStateDigest, budget.Token) : Task.FromResult(false), cancellationToken);
    /// <inheritdoc />
    public Task<bool> RecordRevisionAsync(string tenant, long expectedRevision, long nextRevision, string exactStateDigest,
        CancellationToken cancellationToken = default) => InvokeAsync(_ => Task.FromResult(false), cancellationToken);
    /// <inheritdoc />
    public Task<bool> AdmitTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default) => InvokeAsync(budget => owner.AdmitTransitionAsync(transition, budget.Token), cancellationToken);
    /// <inheritdoc />
    public Task<bool> RecordTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default) => InvokeAsync(budget => owner.RecordTransitionAsync(transition, budget.Token), cancellationToken);
    /// <inheritdoc />
    public Task<bool> RecoverTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default) => InvokeAsync(budget => owner.RecoverTransitionAsync(transition, budget.Token), cancellationToken);
    /// <inheritdoc />
    public Task<bool> VerifyTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default) => InvokeAsync(budget => owner.VerifyTransitionAsync(transition, budget.Token), cancellationToken);

    private Task<bool> InvokeAsync(Func<CustodyOperationBudget, Task<bool>> operation, CancellationToken caller)
        => client.InBudgetAsync(caller, async budget =>
        {
            bool result = await operation(budget).WaitAsync(budget.Token).ConfigureAwait(false);
            budget.Check(); return result;
        }, () => false);
}
