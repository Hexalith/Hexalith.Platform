using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Protection;

/// <summary>Existing lifecycle authority over fresh native ERC registration/outcome reads and an independently enrolled lifecycle actor anchor.</summary>
public sealed class ErasureRootLifecycleAuthority(ErasureRootCustody custody, IErasureRootAuthorization authorization,
    ErasureRootTransitionAuthority transitions) : ICustodyKeyLifecycleAuthority
{
    /// <inheritdoc />
    public async Task<bool> AuthorizeOperationAsync(CustodyKeyObjectIdentity identity, string operationId, string method, string requestDigest, CancellationToken cancellationToken = default)
    {
        if (identity.TenantId != transitions.TenantId) { return false; }
        try { return await authorization.AuthorizeAsync(identity.TenantId, operationId, method, requestDigest, cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); return false; }
    }
    /// <inheritdoc />
    public async Task<bool> VerifyRegistrationAsync(CustodyKeyRegistration registration, CancellationToken cancellationToken = default)
    {
        if (registration.Identity.TenantId != transitions.TenantId) { return false; }
        try
        {
            if (!await AuthorizeOperationAsync(registration.Identity, registration.OperationId, "VerifyWrappedRegistration", ErasureRootCustody.Hash(registration), cancellationToken).ConfigureAwait(false)) { return false; }
            var read = await custody.ReadAsync(registration.Identity.TenantId, cancellationToken).ConfigureAwait(false);
            return read is { } current && current.Head.Pending is null
                && current.Head.Targets.TryGetValue(ErasureRootCustody.TargetId(registration.Identity), out var root) && root.Registration == registration;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); return false; }
    }
    /// <inheritdoc />
    public async Task<bool> AuthorizeEffectAsync(CustodyKeyLifecycleRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Identity.TenantId != transitions.TenantId) { return false; }
        try { return await authorization.VerifyLifecycleAsync(request, cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); return false; }
    }
    /// <inheritdoc />
    public async Task<bool> VerifyOutcomeAsync(CustodyKeyLifecycleRequest request, CustodyKeyLifecycleOutcome outcome, CancellationToken cancellationToken = default)
    {
        if (request.Identity.TenantId != transitions.TenantId || outcome.Identity != request.Identity || outcome.OperationId != request.OperationId
            || outcome.RequestDigest != ErasureRootCustody.Hash(request) || outcome.Status is CustodyKeyLifecycleStatus.Unknown or CustodyKeyLifecycleStatus.Unavailable) { return false; }
        try
        {
            var read = await custody.ReadAsync(request.Identity.TenantId, cancellationToken).ConfigureAwait(false);
            if (read is not { } current || current.Head.Pending is not null) { return false; }
            if (current.Head.LifecycleOutcomes.TryGetValue(request.OperationId, out var retained))
            {
                return ErasureRootCustody.Hash(retained) == ErasureRootCustody.Hash(outcome)
                    && await AuthorizeOperationAsync(request.Identity, request.OperationId, "VerifyLifecycleOutcome", outcome.RequestDigest, cancellationToken).ConfigureAwait(false);
            }
            if (!current.Head.Erased.TryGetValue(ErasureRootCustody.TargetId(request.Identity), out var erased)) { return false; }
            var original = await custody.LookupAsync(erased.Registration, request, outcome.RequestDigest, cancellationToken).ConfigureAwait(false);
            return ErasureRootCustody.Hash(original) == ErasureRootCustody.Hash(outcome);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); return false; }
    }
    /// <inheritdoc />
    public Task<bool> ValidateStateAsync(string tenantId, long revision, string exactStateDigest, CancellationToken cancellationToken = default)
        => tenantId == transitions.TenantId ? transitions.ValidateStateAsync(revision, exactStateDigest, cancellationToken) : Task.FromResult(false);
    /// <inheritdoc />
    public Task<bool> RecordRevisionAsync(string tenantId, long expectedRevision, long nextRevision, string exactStateDigest, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // The legacy digest-only method cannot manufacture an admitted original.
        return Task.FromResult(false);
    }
    /// <inheritdoc />
    public Task<bool> AdmitTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default) => transitions.AdmitTransitionAsync(transition, cancellationToken);
    /// <inheritdoc />
    public Task<bool> RecoverTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default) => transitions.RecoverTransitionAsync(transition, cancellationToken);
    /// <inheritdoc />
    public Task<bool> RecordTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default) => transitions.RecordTransitionAsync(transition, cancellationToken);
    /// <inheritdoc />
    public Task<bool> VerifyTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default) => transitions.VerifyTransitionAsync(transition, cancellationToken);
}
