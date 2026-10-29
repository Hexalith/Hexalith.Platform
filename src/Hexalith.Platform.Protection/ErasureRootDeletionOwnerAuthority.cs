using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;

namespace Hexalith.Platform.Protection;

/// <summary>Existing EventStore deletion-owner port over the independently enrolled ERC anchor, with owner revisions kept separate from guard and KV versions.</summary>
public sealed class ErasureRootDeletionOwnerAuthority : IDeletionConsumptionOwnerStateAuthority
{
    private readonly ErasureRootTransitionAuthority _transitions;
    private readonly IErasureRootAuthorization _authorization;

    /// <summary>Creates an adapter for the canonical tenant deletion actor scope. Does not enroll genesis.</summary>
    public ErasureRootDeletionOwnerAuthority(ErasureRootCustody custody, IErasureRootAuthorization authorization, string tenantId)
    {
        _authorization = authorization;
        _transitions = new(custody, authorization, tenantId, DeletionConsumptionActor.GetOwnerScope(tenantId));
    }
    /// <inheritdoc />
    public string TenantId => _transitions.TenantId;
    /// <inheritdoc />
    public string ScopeId => _transitions.ScopeId;
    /// <inheritdoc />
    public Task<bool> ValidateStateAsync(long ownerRevision, string exactStateDigest, CancellationToken cancellationToken = default)
        => _transitions.ValidateStateAsync(ownerRevision, exactStateDigest, cancellationToken);
    /// <inheritdoc />
    public async Task<bool> AuthorizeOperationAsync(string identity, string operation, CancellationToken cancellationToken = default)
    {
        try { return await _authorization.AuthorizeAsync(TenantId, identity, operation, ErasureRootCustody.Hash(new { identity, operation, ScopeId }), cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); return false; }
    }
    /// <inheritdoc />
    public Task<bool> AdmitTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default) => _transitions.AdmitTransitionAsync(transition, cancellationToken);
    /// <inheritdoc />
    public Task<bool> RecoverTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default) => _transitions.RecoverTransitionAsync(transition, cancellationToken);
    /// <inheritdoc />
    public Task<bool> RecordTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default) => _transitions.RecordTransitionAsync(transition, cancellationToken);
    /// <inheritdoc />
    public Task<bool> VerifyTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default) => _transitions.VerifyTransitionAsync(transition, cancellationToken);
}
