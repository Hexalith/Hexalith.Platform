using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Custody.Component;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Outcome authority. A null witness denies every check. Verification reads the witness and rejects a self-reported outcome.
/// Actor admission, registration, effect, state, and transition checks stay denied while
/// runtime.allCopyLifecycleAuthorityReference, runtime.allCopyInventoryReference, runtime.nonrollbackJournalReference,
/// runtime.restoreTarget, runtime.protectionOwnerReference, runtime.compromiseGuardMirrorReference,
/// runtime.currentRevocationReference, runtime.publicTrustReference, and runtime.publicAnchorRetentionReference are null.</summary>
internal sealed class CustodyKeyLifecycleBindingAuthority : ICustodyKeyLifecycleAuthority
{
    private readonly ICustodyOutsideWitness? _witness;

    internal CustodyKeyLifecycleBindingAuthority(ICustodyOutsideWitness? witness) => _witness = witness;

    /// <inheritdoc/>
    public Task<bool> AuthorizeOperationAsync(CustodyKeyObjectIdentity identity, string operationId, string method, string requestDigest, CancellationToken cancellationToken = default)
        => Deny();

    /// <inheritdoc/>
    public Task<bool> VerifyRegistrationAsync(CustodyKeyRegistration registration, CancellationToken cancellationToken = default) => Deny();

    /// <inheritdoc/>
    public Task<bool> AuthorizeEffectAsync(CustodyKeyLifecycleRequest request, CancellationToken cancellationToken = default) => Deny();

    /// <inheritdoc/>
    public Task<bool> VerifyOutcomeAsync(CustodyKeyLifecycleRequest request, CustodyKeyLifecycleOutcome outcome, CancellationToken cancellationToken = default)
    {
        if (_witness is null || request is null || outcome is null) { return Task.FromResult(false); }
        return Task.FromResult(_witness.Confirms(request, outcome));
    }

    /// <inheritdoc/>
    public Task<bool> ValidateStateAsync(string tenantId, long revision, string exactStateDigest, CancellationToken cancellationToken = default) => Deny();

    /// <inheritdoc/>
    public Task<bool> RecordRevisionAsync(string tenantId, long expectedRevision, long nextRevision, string exactStateDigest, CancellationToken cancellationToken = default) => Deny();

    /// <inheritdoc/>
    public Task<bool> AdmitTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default) => Deny();

    /// <inheritdoc/>
    public Task<bool> RecoverTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default) => Deny();

    /// <inheritdoc/>
    public Task<bool> RecordTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default) => Deny();

    /// <inheritdoc/>
    public Task<bool> VerifyTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default) => Deny();

    private static Task<bool> Deny() => Task.FromResult(false);
}
