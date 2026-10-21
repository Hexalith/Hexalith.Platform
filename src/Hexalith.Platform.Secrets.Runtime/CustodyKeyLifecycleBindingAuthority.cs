using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Custody.Component;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Outcome authority. Verification reads only the outside witness and rejects a self-reported outcome.
/// Admission, registration, effect, state, and anchored transitions require both an installed guard and a nonrollback journal.
/// Either port missing denies and writes nothing. <see cref="RecordRevisionAsync"/> stays denied.</summary>
internal sealed class CustodyKeyLifecycleBindingAuthority : ICustodyKeyLifecycleAuthority
{
    private readonly ICustodyOutsideWitness? _witness;
    private readonly ICustodyInstalledGuard? _guard;
    private readonly ICustodyNonrollbackJournal? _journal;

    internal CustodyKeyLifecycleBindingAuthority(ICustodyOutsideWitness? witness, ICustodyInstalledGuard? guard = null, ICustodyNonrollbackJournal? journal = null)
    {
        _witness = witness;
        _guard = guard;
        _journal = journal;
    }

    /// <inheritdoc/>
    public Task<bool> AuthorizeOperationAsync(CustodyKeyObjectIdentity identity, string operationId, string method, string requestDigest, CancellationToken cancellationToken = default)
    {
        if (identity is null || _guard is not { } guard || _journal is not { } journal || !OperationCurrent(guard, journal)) { return Deny(); }
        return Task.FromResult(guard.Accepts(identity.TenantId, operationId, method, requestDigest, guard.Revision, guard.StateDigest));
    }

    /// <inheritdoc/>
    public Task<bool> VerifyRegistrationAsync(CustodyKeyRegistration registration, CancellationToken cancellationToken = default)
    {
        if (registration?.Identity is null || _guard is not { } guard || _journal is not { } journal || !Checkpoint(guard, journal)) { return Deny(); }
        return Task.FromResult(registration.Identity.TenantId == guard.TenantId && registration.OperationId == guard.OperationId && registration.ExpectedRevision == journal.Head);
    }

    /// <inheritdoc/>
    public Task<bool> AuthorizeEffectAsync(CustodyKeyLifecycleRequest request, CancellationToken cancellationToken = default)
    {
        if (request?.Identity is null || _guard is not { } guard || _journal is not { } journal || !Checkpoint(guard, journal)) { return Deny(); }
        return Task.FromResult(request.Identity.TenantId == guard.TenantId && request.OperationId == guard.OperationId && request.ExpectedRevision == journal.Head);
    }

    /// <inheritdoc/>
    public Task<bool> VerifyOutcomeAsync(CustodyKeyLifecycleRequest request, CustodyKeyLifecycleOutcome outcome, CancellationToken cancellationToken = default)
    {
        if (_witness is null || request is null || outcome is null) { return Task.FromResult(false); }
        return Task.FromResult(_witness.Confirms(request, outcome));
    }

    /// <inheritdoc/>
    public Task<bool> ValidateStateAsync(string tenantId, long revision, string exactStateDigest, CancellationToken cancellationToken = default)
    {
        if (_guard is not { } guard || _journal is not { } journal) { return Deny(); }
        return Task.FromResult(tenantId == guard.TenantId && CurrentState(guard, journal, revision, exactStateDigest));
    }

    /// <inheritdoc/>
    public Task<bool> RecordRevisionAsync(string tenantId, long expectedRevision, long nextRevision, string exactStateDigest, CancellationToken cancellationToken = default) => Deny();

    /// <inheritdoc/>
    public Task<bool> AdmitTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
    {
        if (_guard is not { } guard || _journal is not { } journal || !Checkpoint(guard, journal) || !Predecessor(guard, journal, transition)) { return Deny(); }
        return Task.FromResult(journal.TryAdmit(transition));
    }

    /// <inheritdoc/>
    public Task<bool> RecoverTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
    {
        if (_guard is not { } guard || _journal is not { } journal || !Checkpoint(guard, journal) || !Predecessor(guard, journal, transition)) { return Deny(); }
        return Task.FromResult(journal.TryRecover(transition));
    }

    /// <inheritdoc/>
    public Task<bool> RecordTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
    {
        if (_guard is not { } guard || _journal is not { } journal || !Checkpoint(guard, journal) || !Predecessor(guard, journal, transition)) { return Deny(); }
        return Task.FromResult(journal.TryRecord(transition));
    }

    /// <inheritdoc/>
    public Task<bool> VerifyTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
    {
        if (_guard is not { } guard || _journal is not { } journal || transition is null || !InTenant(guard.TenantId, transition.ScopeId)) { return Deny(); }
        return Task.FromResult(journal.Verify(transition));
    }

    private static bool OperationCurrent(ICustodyInstalledGuard guard, ICustodyNonrollbackJournal journal)
    {
        if (journal.Head == guard.Revision) { return journal.HeadDigest == guard.StateDigest; }
        return RecordedSuccessor(guard, journal);
    }

    private static bool CurrentState(ICustodyInstalledGuard guard, ICustodyNonrollbackJournal journal, long revision, string? digest)
    {
        if (digest != journal.HeadDigest || revision != journal.Head) { return false; }
        if (journal.Head == guard.Revision) { return digest == guard.StateDigest; }
        return RecordedSuccessor(guard, journal);
    }

    private static bool RecordedSuccessor(ICustodyInstalledGuard guard, ICustodyNonrollbackJournal journal)
    {
        if (guard.Revision == long.MaxValue || journal.Head != guard.Revision + 1 || journal.Admitted is not { } admitted) { return false; }
        return admitted.ExpectedRevision == guard.Revision && admitted.PredecessorDigest == guard.StateDigest
            && admitted.TargetRevision == journal.Head && admitted.TargetDigest == journal.HeadDigest && InTenant(guard.TenantId, admitted.ScopeId);
    }

    private static bool Checkpoint(ICustodyInstalledGuard guard, ICustodyNonrollbackJournal journal)
        => journal.Head == guard.Revision && journal.HeadDigest == guard.StateDigest;

    private static bool Predecessor(ICustodyInstalledGuard guard, ICustodyNonrollbackJournal journal, AnchoredStateTransition? transition)
        => transition is not null && InTenant(guard.TenantId, transition.ScopeId) && transition.ExpectedRevision == journal.Head && transition.PredecessorDigest == journal.HeadDigest;

    private static bool InTenant(string tenantId, string? scopeId)
    {
        if (string.IsNullOrEmpty(scopeId)) { return false; }
        try
        {
            return scopeId.StartsWith(CustodyKeyLifecycleActor.GetActorId(tenantId) + "|", StringComparison.Ordinal);
        }
        catch (ArgumentException) { return false; }
    }

    private static Task<bool> Deny() => Task.FromResult(false);
}
