using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Security;

/// <summary>
/// Fail-closed online consumption authority. It verifies one published detached ES256 entry, then rereads the guard.
/// It does not cache that read, self-issue an anchor, or authorize a physical effect.
/// </summary>
public sealed class PublishedTrustDeletionConsumptionAuthority : IDeletionConsumptionAuthority
{
    private const string KeyFamily = "DeletionBatchCapabilitySigningKey";
    private const string CurveOid = "1.2.840.10045.3.1.7";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly PublishedDeletionCapabilityTrust[] _publishedTrust;
    private readonly IGovernanceScopeGuard _guard;
    private readonly IAnchoredStateTransitionAuthority? _journal;
    private readonly IPublishedTrustRevocationEvidence? _revocations;
    private readonly IDeletionConsumptionOwnerStateAuthority[] _ownerAuthorities;

    /// <summary>Creates the authority with the existing fail-closed public signature and no owner installation.</summary>
    /// <param name="publishedTrust">Independently published retained capability trust.</param>
    /// <param name="guard">Fresh exact guard reader.</param>
    /// <param name="journal">Separate restrictive evidence journal; omission denies those checks.</param>
    /// <param name="revocations">Independent revocation/successor evidence.</param>
    public PublishedTrustDeletionConsumptionAuthority(
        IReadOnlyList<PublishedDeletionCapabilityTrust> publishedTrust,
        IGovernanceScopeGuard guard,
        IAnchoredStateTransitionAuthority? journal = null,
        IPublishedTrustRevocationEvidence? revocations = null)
        : this(publishedTrust, guard, journal, revocations, null)
    {
    }

    /// <summary>Creates the authority from independently published trust and explicit owner installations.</summary>
    /// <param name="publishedTrust">Published trust entries. Dispatch requires one unrevoked match; exact independently authenticated successor reconciliation may use its retained verifier.</param>
    /// <param name="guard">Fresh guard reader. Each dispatch check reads it twice and keeps neither result.</param>
    /// <param name="journal">Independent transition journal. Null denies the separate admission, revocation and successor evidence checks. This authority does not mint anchors.</param>
    /// <param name="revocations">Injected revocation evidence. Null denies revocation and activation.</param>
    /// <param name="ownerAuthorities">Independently installed tenant/actor-scope owner authorities. Missing or ambiguous enrollment denies owner operations, currentness and transitions.</param>
    public PublishedTrustDeletionConsumptionAuthority(
        IReadOnlyList<PublishedDeletionCapabilityTrust> publishedTrust,
        IGovernanceScopeGuard guard,
        IAnchoredStateTransitionAuthority? journal,
        IPublishedTrustRevocationEvidence? revocations,
        IReadOnlyList<IDeletionConsumptionOwnerStateAuthority>? ownerAuthorities)
    {
        ArgumentNullException.ThrowIfNull(publishedTrust);
        ArgumentNullException.ThrowIfNull(guard);
        if (publishedTrust.Any(static entry => entry is null))
        {
            throw new ArgumentException("Published trust contains an absent entry.", nameof(publishedTrust));
        }

        _publishedTrust = publishedTrust.ToArray();
        _guard = guard;
        _journal = journal;
        _revocations = revocations;
        _ownerAuthorities = ownerAuthorities?.ToArray() ?? [];
    }

    /// <inheritdoc />
    public async Task<bool> AuthorizeOperationAsync(string tenantId, string identity, string operation, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Text(identity) || !Text(operation))
        {
            return false;
        }

        try
        {
            IDeletionConsumptionOwnerStateAuthority? owner = OwnerForTenant(tenantId);
            bool authorized = owner is not null && await owner.AuthorizeOperationAsync(identity, operation, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return authorized;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ValidateStateAsync(string tenantId, long revision, string exactStateDigest, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (revision < 0 || !Hex(exactStateDigest))
        {
            return false;
        }

        try
        {
            IDeletionConsumptionOwnerStateAuthority? owner = OwnerForTenant(tenantId);
            // Revision zero is only a candidate: the independent owner must select the
            // exact enrolled genesis. Local absence never enrolls or resets that owner.
            bool current = owner is not null && await owner.ValidateStateAsync(revision, exactStateDigest, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return current;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return false;
        }
    }

    /// <inheritdoc />
    public Task<bool> RecordRevisionAsync(string tenantId, long expectedRevision, long nextRevision, string exactStateDigest, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Compatibility is read-only: only an actual admitted transition may advance.
        return expectedRevision < 0 || expectedRevision == long.MaxValue || nextRevision != expectedRevision + 1
            ? Task.FromResult(false)
            : ValidateStateAsync(tenantId, nextRevision, exactStateDigest, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> VerifyDispatchAsync(DeletionBatchConsumptionRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request is null)
            {
                return false;
            }

            DeletionBatchConsumptionRequest captured = DeletionConsumptionIdentity.Capture(request);
            if (!DeletionConsumptionIdentity.Same(captured, request) || !SignatureMatches(captured))
            {
                return false;
            }

            TenantGovernanceGuardState? first = await _guard.ReadAsync(captured.Capability.TenantId, cancellationToken).ConfigureAwait(false);
            byte[]? firstBytes = first is null ? null : JsonSerializer.SerializeToUtf8Bytes(first);
            TenantGovernanceGuardState? second = await _guard.ReadAsync(captured.Capability.TenantId, cancellationToken).ConfigureAwait(false);
            byte[]? secondBytes = second is null ? null : JsonSerializer.SerializeToUtf8Bytes(second);
            if (firstBytes is null || secondBytes is null || !firstBytes.AsSpan().SequenceEqual(secondBytes))
            {
                return false;
            }

            // Admit only the compared bytes, never collections still owned by the guard reader.
            TenantGovernanceGuardState? snapshot = JsonSerializer.Deserialize<TenantGovernanceGuardState>(secondBytes);
            return snapshot is not null && Admitted(snapshot, captured);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> VerifyAdmissionBlockAsync(DeletionBatchBlockRequest request, CancellationToken cancellationToken = default)
    {
        if (_journal is null || request is null || !Text(request.TenantId) || !Text(request.BatchId) || !Text(request.OperationId) || !Text(request.AdmissionEvidenceId))
        {
            return false;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            TenantGovernanceGuardState? state = await _guard.ReadAsync(request.TenantId, cancellationToken).ConfigureAwait(false);
            if (state is null || state.TenantId != request.TenantId || SingleBatch(state, request.BatchId) is null
                || !HasCommittedReceipt(state, request.AdmissionEvidenceId, request.BatchId))
            {
                return false;
            }

            return await _journal.VerifyTransitionAsync(Proof("admission-block", request.TenantId, request.BatchId, state.Revision, request.AdmissionEvidenceId), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> VerifyRevocationAsync(DeletionCapabilityRevocationEnvelope envelope, CancellationToken cancellationToken = default)
    {
        if (_journal is null || _revocations is null || envelope is null || envelope.KeyFamily != KeyFamily || envelope.RevocationRevision < 1
            || envelope.TrustProfileRevision < 1 || !Text(envelope.TenantId) || !Text(envelope.Issuer) || !Text(envelope.Audience)
            || !Text(envelope.KeyVersion) || !Text(envelope.EventIdentity) || !Hex(envelope.SignatureDigest)
            || !SoleTrust(envelope.Issuer, envelope.Audience, envelope.TenantId, envelope.KeyVersion))
        {
            return false;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await CurrentRevocationAsync(envelope, cancellationToken).ConfigureAwait(false)) { return false; }
            TenantGovernanceGuardState? state = await _guard.ReadAsync(envelope.TenantId, cancellationToken).ConfigureAwait(false);
            if (state is null || state.TenantId != envelope.TenantId)
            {
                return false;
            }

            return await _journal.VerifyTransitionAsync(Proof("revocation", envelope.TenantId, envelope.EventIdentity, state.Revision, envelope.SignatureDigest), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> VerifyActivationAsync(DeletionReattestationActivation activation, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_journal is null || _revocations is null || activation is null)
            {
                return false;
            }

            var captured = activation with { Replacement = DeletionConsumptionIdentity.Capture(activation.Replacement) };
            if (DeletionConsumptionIdentity.Digest(captured) != DeletionConsumptionIdentity.Digest(activation)
                || captured.ExpectedKeyBlockSetRevision < 0 || !Text(captured.OperationId)
                || !Text(captured.CompromiseBlockReceiptId) || !Text(captured.GuardReplacementReceiptId)
                || !await CurrentActivationAsync(captured, cancellationToken).ConfigureAwait(false)
                || !SignatureMatches(captured.Replacement, retainedVerifier: true))
            {
                return false;
            }

            DeletionBatchConsumptionRequest replacement = captured.Replacement;
            DeletionBatchCapabilityV1 capability = replacement.Capability;
            TenantGovernanceGuardState? state = await ReadSnapshotAsync(capability.TenantId, cancellationToken).ConfigureAwait(false);
            GovernanceBatchState? batch = state is null ? null : SingleBatch(state, capability.BatchId);
            if (state is null || batch is null
                || !IssuedReplacement(state, batch, capability, replacement.DetachedJws,
                    DeletionBatchCapabilityIdentity.SigningRequestId(capability), replacement.CommittedIssuedGuardRevision, replacement.Targets)
                || batch.ProtectionReceiptId != captured.CompromiseBlockReceiptId
                || batch.IssueReceiptId != captured.GuardReplacementReceiptId
                || captured.CompromiseBlockReceiptId == captured.GuardReplacementReceiptId
                || captured.CompromiseBlockReceiptId == replacement.DispatchReceiptId
                || batch.DispatchReceiptId != replacement.DispatchReceiptId
                || batch.DispatchGuardRevision != replacement.DispatchGuardRevision
                || batch.DispatchGuardRevision <= batch.IssuedGuardRevision || batch.DispatchGuardRevision > state.Revision
                || batch.IssueReceiptId == batch.DispatchReceiptId
                || !Committed(state, batch.DispatchReceiptId, batch.DispatchGuardRevision, capability, batch.SigningRequestId, batch.AttestationDigest)
                || !DistinctOperations(state, batch.IssueReceiptId, batch.DispatchReceiptId)
                || EarlierHoldRestricts(state, state.Deletions.Single(deletion => deletion.Batches.Contains(batch)), batch))
            {
                return false;
            }

            // This authenticates the retained successor. Only the protection owner's atomic
            // key-block comparison may choose activated versus replacement-key-blocked.
            return await _journal.VerifyTransitionAsync(Proof("activation", capability.TenantId, capability.BatchId,
                state.Revision, DeletionConsumptionIdentity.Digest(captured)), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> VerifyBlockedReplacementAsync(DeletionBlockedReplacementReconciliation request, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_journal is null || _revocations is null || request is null)
            {
                return false;
            }

            DeletionBlockedReplacementReconciliation captured = DeletionConsumptionIdentity.Capture(request);
            if (DeletionConsumptionIdentity.Digest(captured) != DeletionConsumptionIdentity.Digest(request)
                || !await CurrentBlockedReplacementAsync(captured, cancellationToken).ConfigureAwait(false)
                || !await CurrentRevocationAsync(captured.RevocationReceipt.Envelope, cancellationToken).ConfigureAwait(false)
                || !SignatureMatches(captured.Capability, captured.DetachedJws, captured.Targets, retainedVerifier: true))
            {
                return false;
            }

            DeletionBatchCapabilityV1 capability = captured.Capability;
            TenantGovernanceGuardState? state = await ReadSnapshotAsync(capability.TenantId, cancellationToken).ConfigureAwait(false);
            GovernanceBatchState? batch = state is null ? null : SingleBatch(state, capability.BatchId);
            if (captured.UnissuedProof is { } refused)
            {
                if (state is null || batch?.Capability is not { } issued || refused.CurrentGuardRevision != state.Revision
                    || refused.CurrentGuardStateDigest != DeletionConsumptionIdentity.Digest(state) || batch.ProtectionOutcome != "ConsumptionBlocked:CapabilityKeyCompromise"
                    || batch.ProtectionReceiptId != captured.CompromiseBlockReceiptId || capability.AttestationOrdinal != checked(batch.AttestationOrdinal + 1)
                    || issued.TenantId != capability.TenantId || issued.DeletionRequestId != capability.DeletionRequestId || issued.DestructionSealId != capability.DestructionSealId
                    || issued.BatchId != capability.BatchId || issued.ManifestDigest != capability.ManifestDigest || issued.BatchKind != capability.BatchKind || issued.BatchOrdinal != capability.BatchOrdinal
                    || issued.GuardStreamId != capability.GuardStreamId || !SameTargets(batch.Targets, captured.Targets)
                    || !state.CompromisedKeyVersions.Contains(capability.CapabilityKeyVersion, StringComparer.Ordinal)
                    || !state.Revocations.Any(value => DeletionConsumptionIdentity.Digest(value) == DeletionConsumptionIdentity.Digest(captured.RevocationReceipt))
                    || state.Receipts.Any(value => value.Status == "Committed" && (value.Capability == capability || value.SigningRequestId == captured.SigningRequestId))) { return false; }
                return await _journal.VerifyTransitionAsync(Proof("blocked-replacement", capability.TenantId, capability.BatchId,
                    state.Revision, DeletionConsumptionIdentity.Digest(captured)), cancellationToken).ConfigureAwait(false);
            }
            if (state is null || batch is null
                || !IssuedReplacement(state, batch, capability, captured.DetachedJws, captured.SigningRequestId,
                    captured.CommittedIssuedGuardRevision, captured.Targets)
                || batch.ProtectionReceiptId != captured.CompromiseBlockReceiptId
                || batch.IssueReceiptId != captured.GuardReplacementReceiptId
                || captured.CompromiseBlockReceiptId == captured.GuardReplacementReceiptId
                || state.CompromisedKeyVersions is null || !state.CompromisedKeyVersions.Contains(capability.CapabilityKeyVersion, StringComparer.Ordinal)
                || state.Revocations is null
                || state.Revocations.Count(receipt => receipt is not null
                    && (receipt.Envelope.EventIdentity == captured.RevocationReceipt.Envelope.EventIdentity
                        || receipt.Envelope.KeyVersion == capability.CapabilityKeyVersion
                            && receipt.Envelope.RevocationRevision == captured.RevocationReceipt.Envelope.RevocationRevision)) != 1
                || !state.Revocations.Any(receipt => DeletionConsumptionIdentity.Digest(receipt) == DeletionConsumptionIdentity.Digest(captured.RevocationReceipt))
                || !await _revocations.VerifyCurrentBlockedReplacementAsync(captured, state.Revision,
                    DeletionConsumptionIdentity.Digest(state), cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            // Exact issue correlation deliberately requires no dispatch. No destructive grant follows.
            return await _journal.VerifyTransitionAsync(Proof("blocked-replacement", capability.TenantId, capability.BatchId,
                state.Revision, DeletionConsumptionIdentity.Digest(captured)), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public Task<AnchoredStateTransition?> ReadAdmittedOriginalAsync(string scopeId, CancellationToken cancellationToken = default)
    {
        var owners = _ownerAuthorities.Where(owner => owner.ScopeId == scopeId && OwnerForTenant(owner.TenantId) == owner).ToArray();
        return owners.Length == 1 ? owners[0].ReadAdmittedOriginalAsync(scopeId, cancellationToken) : Task.FromResult<AnchoredStateTransition?>(null);
    }
    /// <inheritdoc />
    public Task<AnchoredStateTransition?> ReadCurrentOriginalAsync(string scopeId, CancellationToken cancellationToken = default)
    {
        var owners = _ownerAuthorities.Where(owner => owner.ScopeId == scopeId && OwnerForTenant(owner.TenantId) == owner).ToArray();
        return owners.Length == 1 ? owners[0].ReadCurrentOriginalAsync(scopeId, cancellationToken) : Task.FromResult<AnchoredStateTransition?>(null);
    }

    private async Task<TenantGovernanceGuardState?> ReadSnapshotAsync(string tenantId, CancellationToken cancellationToken)
    {
        TenantGovernanceGuardState? state = await _guard.ReadAsync(tenantId, cancellationToken).ConfigureAwait(false);
        // Detach nested collections before any predicate or external evidence callback.
        return state is null ? null : JsonSerializer.Deserialize<TenantGovernanceGuardState>(JsonSerializer.SerializeToUtf8Bytes(state));
    }

    private static bool IssuedReplacement(TenantGovernanceGuardState state, GovernanceBatchState batch,
        DeletionBatchCapabilityV1 capability, string detachedJws, string signingId, long issuedRevision, IReadOnlyList<ProtectionTarget> targets)
    {
        GovernanceDeletionState? owner = state.Deletions.SingleOrDefault(deletion => deletion?.Batches?.Contains(batch) == true);
        string jwsDigest = Convert.ToHexString(SHA256.HashData(StrictUtf8.GetBytes(detachedJws)));
        return state.TenantId == capability.TenantId && state.Revision >= issuedRevision && issuedRevision > 0
            && state.Holds is not null && state.Holds.All(hold => ValidHold(hold, state.TenantId))
            && state.Holds.Select(hold => hold.HoldId).Distinct(StringComparer.Ordinal).Count() == state.Holds.Count
            && owner is not null && ValidScope(owner.Scope, state.TenantId) && !owner.IntegrityCompromised && !owner.Completed
            && owner.RequestId == capability.DeletionRequestId && owner.SealId == capability.DestructionSealId
            && batch.ProtectionOutcome == "ReplacementAwaitingActivation" && Text(batch.ProtectionReceiptId)
            && batch.Capability == capability && batch.DetachedJws == detachedJws && batch.SigningRequestId == signingId
            && batch.Kind == capability.BatchKind && batch.Ordinal == capability.BatchOrdinal && batch.ManifestDigest == capability.ManifestDigest
            && batch.AttestationOrdinal == capability.AttestationOrdinal && capability.AttestationOrdinal > 1
            && batch.CapabilityKeyVersion == capability.CapabilityKeyVersion && batch.AttestationDigest == jwsDigest
            && batch.IssuedGuardRevision == issuedRevision && issuedRevision == checked(capability.IntendedIssuedGuardRevision + 1)
            && batch.TargetReceiptIds is { Count: 0 } && SameTargets(batch.Targets, targets)
            && Committed(state, batch.IssueReceiptId, issuedRevision, capability, signingId, jwsDigest);
    }

    /// <inheritdoc />
    public Task<bool> AdmitTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => DelegateTransition(transition, static (journal, value, token) => journal.AdmitTransitionAsync(value, token), cancellationToken);

    /// <inheritdoc />
    public Task<bool> RecoverTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => DelegateTransition(transition, static (journal, value, token) => journal.RecoverTransitionAsync(value, token), cancellationToken);

    /// <inheritdoc />
    public Task<bool> RecordTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => DelegateTransition(transition, static (journal, value, token) => journal.RecordTransitionAsync(value, token), cancellationToken);

    /// <inheritdoc />
    public Task<bool> VerifyTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => DelegateTransition(transition, static (journal, value, token) => journal.VerifyTransitionAsync(value, token), cancellationToken);

    private async Task<bool> DelegateTransition(
        AnchoredStateTransition transition,
        Func<IAnchoredStateTransitionAuthority, AnchoredStateTransition, CancellationToken, Task<bool>> call,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (transition is null)
            {
                return false;
            }

            IDeletionConsumptionOwnerStateAuthority[] owners = _ownerAuthorities
                .Where(owner => owner is not null && owner.ScopeId == transition.ScopeId && OwnerForTenant(owner.TenantId) == owner).ToArray();
            if (owners.Length != 1)
            {
                return false;
            }

            // Forward the actor's original scope, predecessor, target and bytes unchanged.
            bool accepted = await call(owners[0], transition, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return accepted;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return false;
        }
    }

    private IDeletionConsumptionOwnerStateAuthority? OwnerForTenant(string tenantId)
    {
        if (!Text(tenantId))
        {
            return null;
        }

        string scope = DeletionConsumptionActor.GetOwnerScope(tenantId);
        // Actor identities canonicalize tenants; callers must already use that canonical tenant.
        if (!scope.StartsWith(tenantId + ":", StringComparison.Ordinal))
        {
            return null;
        }

        IDeletionConsumptionOwnerStateAuthority[] matches = _ownerAuthorities
            .Where(owner => owner is not null && owner.TenantId == tenantId).ToArray();
        return matches.Length == 1 && matches[0].ScopeId == scope ? matches[0] : null;
    }

    private static AnchoredStateTransition Proof(string kind, string tenantId, string identity, long revision, string material)
    {
        if (revision < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(revision));
        }

        long expected = checked(revision - 1);
        long targetRevision = checked(expected + 1);
        byte[] bytes = StrictUtf8.GetBytes(string.Join('\n', kind, tenantId, identity, revision.ToString(CultureInfo.InvariantCulture), material));
        string target = Convert.ToHexString(SHA256.HashData(bytes));
        string predecessor = Convert.ToHexString(SHA256.HashData(StrictUtf8.GetBytes(kind + "|predecessor|" + tenantId + "|" + expected.ToString(CultureInfo.InvariantCulture))));
        return new("published-trust|" + kind + "|" + tenantId + "|" + identity, expected, targetRevision, predecessor, target, bytes);
    }

    private Task<bool> CurrentRevocationAsync(DeletionCapabilityRevocationEnvelope envelope, CancellationToken token)
        => _revocations is IAsyncPublishedTrustRevocationEvidence current ? current.VerifyRevocationEvidenceAsync(envelope, token)
            : Task.FromResult(_revocations is not null && _revocations.Matches(envelope) && !_revocations.Pending(envelope));
    private Task<bool> CurrentActivationAsync(DeletionReattestationActivation activation, CancellationToken token)
        => _revocations is IAsyncPublishedTrustRevocationEvidence current ? current.VerifyActivationEvidenceAsync(activation, token)
            : Task.FromResult(_revocations is not null && _revocations.MatchesActivation(activation) && !_revocations.PendingActivation(activation));
    private Task<bool> CurrentBlockedReplacementAsync(DeletionBlockedReplacementReconciliation request, CancellationToken token)
        => _revocations is IAsyncPublishedTrustRevocationEvidence current ? current.VerifyBlockedReplacementEvidenceAsync(request, token)
            : Task.FromResult(_revocations is not null && _revocations.MatchesBlockedReplacement(request) && !_revocations.PendingBlockedReplacement(request));

    private bool SoleTrust(DeletionBatchCapabilityV1 capability)
        => SoleTrust(capability.Issuer, capability.Audience, capability.TenantId, capability.CapabilityKeyVersion);

    private bool SoleTrust(string issuer, string audience, string tenantId, string keyVersion)
    {
        int matches = _publishedTrust.Count(entry => entry is not null
            && !entry.IsRevoked
            && entry.TrustProfileRevision > 0
            && entry.KeyFamily == KeyFamily
            && entry.Issuer == issuer
            && entry.Audience == audience
            && entry.TenantId == tenantId
            && entry.CapabilityKeyVersion == keyVersion
            && !string.IsNullOrWhiteSpace(entry.PublicAnchorId)
            && !string.IsNullOrWhiteSpace(entry.PublicAnchorVersion));
        return matches == 1;
    }

    private static GovernanceBatchState? SingleBatch(TenantGovernanceGuardState state, string batchId)
    {
        if (state.Deletions is null)
        {
            return null;
        }

        GovernanceBatchState[] batches = state.Deletions.Where(static deletion => deletion?.Batches is not null)
            .SelectMany(deletion => deletion.Batches.Where(batch => batch is not null && batch.BatchId == batchId))
            .ToArray();
        if (batches.Length != 1)
        {
            return null;
        }

        GovernanceDeletionState? owner = state.Deletions.SingleOrDefault(deletion => deletion?.Batches?.Any(batch => batch?.BatchId == batchId) == true);
        return owner?.Scope is not null && owner.Scope.TenantId == state.TenantId ? batches[0] : null;
    }

    private static bool HasCommittedReceipt(TenantGovernanceGuardState state, string receiptId, string referenceId)
    {
        if (state.Receipts is null || string.IsNullOrWhiteSpace(receiptId))
        {
            return false;
        }

        GovernanceProtocolReceipt[] found = state.Receipts.Where(receipt => receipt is not null && receipt.ReceiptId == receiptId && receipt.ReferenceId == referenceId).ToArray();
        return found.Length == 1 && found[0].Status == "Committed";
    }

    private static bool Hex(string? value)
        => value is { Length: 64 } && value.All(static character => character is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f');

    private bool SignatureMatches(DeletionBatchConsumptionRequest request, bool retainedVerifier = false)
        => SignatureMatches(request.Capability, request.DetachedJws, request.Targets, retainedVerifier);

    private bool SignatureMatches(DeletionBatchCapabilityV1 capability, string detachedJws, IReadOnlyList<ProtectionTarget> targets, bool retainedVerifier)
    {
        if (capability is null)
        {
            return false;
        }

        PublishedDeletionCapabilityTrust[] matches = _publishedTrust.Where(entry => entry is not null
            && (retainedVerifier || !entry.IsRevoked)
            && entry.TrustProfileRevision > 0
            && entry.KeyFamily == KeyFamily
            && entry.Issuer == capability.Issuer
            && entry.Audience == capability.Audience
            && entry.TenantId == capability.TenantId
            && entry.CapabilityKeyVersion == capability.CapabilityKeyVersion
            && !string.IsNullOrWhiteSpace(entry.PublicAnchorId)
            && !string.IsNullOrWhiteSpace(entry.PublicAnchorVersion)
            && entry.SubjectPublicKeyInfo is { Length: > 0 }).ToArray();
        if (matches.Length != 1)
        {
            return false;
        }

        if (targets is null || targets.Count is < 1 or > 1000)
        {
            return false;
        }

        ProtectionTarget? previous = null;
        foreach (ProtectionTarget target in targets)
        {
            if (target is null || target.TenantId != capability.TenantId
                || previous is not null && (StringComparer.Ordinal.Compare(previous.AgentInteractionId, target.AgentInteractionId) > 0
                    || previous.AgentInteractionId == target.AgentInteractionId
                        && StringComparer.Ordinal.Compare(previous.TargetProtectionKeyAlias, target.TargetProtectionKeyAlias) >= 0))
            {
                return false;
            }

            previous = target;
        }

        byte[] payload = DeletionBatchCapabilityIdentity.CanonicalPayload(capability);
        string digest = DeletionBatchCapabilityIdentity.TargetManifestDigest(targets);
        if (!string.Equals(digest, capability.ManifestDigest, StringComparison.Ordinal))
        {
            return false;
        }

        return Verify(Header(matches[0]), payload, detachedJws, matches[0].SubjectPublicKeyInfo);
    }

    private static bool Admitted(TenantGovernanceGuardState state, DeletionBatchConsumptionRequest request)
    {
        DeletionBatchCapabilityV1 capability = request.Capability;
        if (state.TenantId != capability.TenantId || state.Revision < request.DispatchGuardRevision
            || state.Deletions is null || state.Receipts is null || state.Holds is null || state.CompromisedKeyVersions is null
            || state.Holds.Any(hold => !ValidHold(hold, state.TenantId))
            || state.Holds.Select(hold => hold.HoldId).Distinct(StringComparer.Ordinal).Count() != state.Holds.Count)
        {
            return false;
        }

        GovernanceBatchState[] batches = state.Deletions.Where(deletion => deletion is not null && deletion.Batches is not null)
            .SelectMany(deletion => deletion.Batches.Where(batch => batch is not null && batch.BatchId == capability.BatchId)
                .Select(batch => batch))
            .ToArray();
        if (batches.Length != 1)
        {
            return false;
        }

        GovernanceDeletionState? owner = state.Deletions.SingleOrDefault(deletion => deletion?.Batches?.Any(batch => batch?.BatchId == capability.BatchId) == true);
        GovernanceBatchState batch = batches[0];
        string jwsDigest = Convert.ToHexString(SHA256.HashData(StrictUtf8.GetBytes(request.DetachedJws)));
        string signingId = DeletionBatchCapabilityIdentity.SigningRequestId(capability);
        if (owner is null || !ValidScope(owner.Scope, state.TenantId)
            || owner.IntegrityCompromised || owner.RequestId != capability.DeletionRequestId || owner.SealId != capability.DestructionSealId
            || batch.Capability != capability || batch.DetachedJws != request.DetachedJws || batch.SigningRequestId != signingId
            || batch.Kind != capability.BatchKind || batch.Ordinal != capability.BatchOrdinal || batch.ManifestDigest != capability.ManifestDigest
            || batch.AttestationOrdinal != capability.AttestationOrdinal || batch.CapabilityKeyVersion != capability.CapabilityKeyVersion
            || !string.Equals(batch.AttestationDigest, jwsDigest, StringComparison.Ordinal)
            || batch.IssuedGuardRevision != request.CommittedIssuedGuardRevision || batch.DispatchGuardRevision != request.DispatchGuardRevision
            || request.CommittedIssuedGuardRevision != checked(capability.IntendedIssuedGuardRevision + 1)
            || string.IsNullOrWhiteSpace(batch.IssueReceiptId) || batch.IssueReceiptId == batch.DispatchReceiptId
            || batch.DispatchReceiptId != request.DispatchReceiptId || batch.ProtectionOutcome != "Unconsumed"
            || state.CompromisedKeyVersions.Contains(batch.CapabilityKeyVersion, StringComparer.Ordinal)
            || !SameTargets(batch.Targets, request.Targets)
            || batch.DispatchGuardRevision <= batch.IssuedGuardRevision)
        {
            return false;
        }

        // Each batch holds its current attestation in place. The global exact-BatchId
        // lookup above rejects duplicate credentials; other containment batches may
        // independently have their own dispatched current attestation.
        return Committed(state, batch.IssueReceiptId, batch.IssuedGuardRevision, capability, signingId, jwsDigest)
            && Committed(state, batch.DispatchReceiptId, batch.DispatchGuardRevision, capability, signingId, jwsDigest)
            && DistinctOperations(state, batch.IssueReceiptId, batch.DispatchReceiptId)
            && !EarlierHoldRestricts(state, owner, batch);
    }

    private static bool EarlierHoldRestricts(TenantGovernanceGuardState state, GovernanceDeletionState deletion, GovernanceBatchState batch)
    {
        foreach (GovernanceHoldState hold in state.Holds)
        {
            if (!hold.Active || !Overlaps(hold.Scope, deletion.Scope))
            {
                continue;
            }

            GovernanceProtocolReceipt[] receipts = state.Receipts.Where(receipt => receipt is not null && receipt.Status == "Committed" && receipt.ReferenceId == hold.HoldId).ToArray();
            if (receipts.Length != 1 || !Text(receipts[0].OperationId) || !Text(receipts[0].ReceiptId)
                || receipts[0].IntentDigest is not { Length: 64 } || !receipts[0].IntentDigest.All(char.IsAsciiHexDigit)
                || receipts[0].GuardHighWater <= batch.DispatchGuardRevision || receipts[0].GuardHighWater > state.Revision)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ValidHold(GovernanceHoldState? hold, string tenantId)
        => hold is not null && Text(hold.HoldId) && Text(hold.DispositionVersion)
            && ValidScope(hold.Scope, tenantId)
            && (hold.Active ? hold.ReleaseReceiptId == "" : Text(hold.ReleaseReceiptId));

    private static bool ValidScope(GovernanceScopeV1? scope, string tenantId)
        => scope is not null && scope.TenantId == tenantId
            && (scope.Kind == "ExactInteraction" && Text(scope.AgentInteractionId) && scope.SourceConversationId == ""
                || scope.Kind == "SourceDeletionExactConversation" && Text(scope.SourceConversationId) && scope.AgentInteractionId == "");

    private static bool Text(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 2048) { return false; }
        try { _ = StrictUtf8.GetByteCount(value); return true; }
        catch (EncoderFallbackException) { return false; }
    }

    private static bool DistinctOperations(TenantGovernanceGuardState state, string issueReceiptId, string dispatchReceiptId)
    {
        GovernanceProtocolReceipt? issue = state.Receipts.SingleOrDefault(receipt => receipt?.ReceiptId == issueReceiptId);
        GovernanceProtocolReceipt? dispatch = state.Receipts.SingleOrDefault(receipt => receipt?.ReceiptId == dispatchReceiptId);
        return issue is not null && dispatch is not null
            && !string.IsNullOrWhiteSpace(issue.OperationId) && !string.IsNullOrWhiteSpace(dispatch.OperationId)
            && issue.OperationId != dispatch.OperationId;
    }

    private static bool Committed(TenantGovernanceGuardState state, string receiptId, long revision, DeletionBatchCapabilityV1 capability, string signingId, string jwsDigest)
    {
        GovernanceProtocolReceipt[] receipts = state.Receipts.Where(receipt => receipt is not null && receipt.ReceiptId == receiptId).ToArray();
        if (receipts.Length != 1)
        {
            return false;
        }

        GovernanceProtocolReceipt receipt = receipts[0];
        return Text(receipt.OperationId) && Text(receipt.ReceiptId) && Hex(receipt.IntentDigest)
            && receipt.Status == "Committed" && receipt.GuardHighWater == revision && receipt.ReferenceId == capability.BatchId
            && receipt.Capability == capability && receipt.SigningRequestId == signingId
            && string.Equals(receipt.DetachedJwsDigest, jwsDigest, StringComparison.Ordinal);
    }

    private static bool Overlaps(GovernanceScopeV1 left, GovernanceScopeV1 right)
        => left.TenantId == right.TenantId && (left.Kind != right.Kind || left == right);

    private static bool SameTargets(IReadOnlyList<GovernanceProtectionTarget> left, IReadOnlyList<ProtectionTarget> right)
    {
        if (left is null || right is null || left.Count != right.Count)
        {
            return false;
        }

        for (int index = 0; index < left.Count; index++)
        {
            GovernanceProtectionTarget item = left[index];
            ProtectionTarget expected = right[index];
            if (item is null || expected is null || item.TenantId != expected.TenantId || item.AgentInteractionId != expected.AgentInteractionId
                || item.TargetProtectionKeyAlias != expected.TargetProtectionKeyAlias)
            {
                return false;
            }
        }

        return true;
    }

    private static string Header(PublishedDeletionCapabilityTrust trust)
    {
        var text = new StringBuilder("{\"alg\":\"ES256\",\"anchorVersion\":");
        Append(text, trust.PublicAnchorVersion);
        text.Append(",\"keyVersion\":");
        Append(text, trust.CapabilityKeyVersion);
        text.Append(",\"kid\":");
        Append(text, trust.PublicAnchorId);
        text.Append(",\"typ\":\"DeletionBatchCapabilityV1\"}");
        return Base64Url(StrictUtf8.GetBytes(text.ToString()));
    }

    private static void Append(StringBuilder text, string value)
    {
        _ = StrictUtf8.GetByteCount(value);
        text.Append('"');
        foreach (char character in value)
        {
            switch (character)
            {
                case '"':
                    text.Append("\\\"");
                    break;
                case '\\':
                    text.Append("\\\\");
                    break;
                case '\b':
                    text.Append("\\b");
                    break;
                case '\t':
                    text.Append("\\t");
                    break;
                case '\n':
                    text.Append("\\n");
                    break;
                case '\f':
                    text.Append("\\f");
                    break;
                case '\r':
                    text.Append("\\r");
                    break;
                default:
                    if (character < 0x20)
                    {
                        text.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        text.Append(character);
                    }

                    break;
            }
        }

        text.Append('"');
    }

    private static bool Verify(string header, ReadOnlySpan<byte> payload, string detachedJws, byte[] subjectPublicKeyInfo)
    {
        byte[]? input = null;
        try
        {
            if (detachedJws is null || detachedJws.Length != (long)header.Length + 88
                || !detachedJws.AsSpan(0, header.Length).SequenceEqual(header)
                || detachedJws[header.Length] != '.' || detachedJws[header.Length + 1] != '.')
            {
                return false;
            }

            string signatureText = detachedJws[(header.Length + 2)..];
            byte[] signature = Decode(signatureText);
            if (signature.Length != 64 || !string.Equals(Base64Url(signature), signatureText, StringComparison.Ordinal))
            {
                return false;
            }

            using ECDsa key = ECDsa.Create();
            key.ImportSubjectPublicKeyInfo(subjectPublicKeyInfo, out int bytesRead);
            if (bytesRead != subjectPublicKeyInfo.Length || key.KeySize != 256 || key.ExportParameters(false).Curve.Oid.Value != CurveOid)
            {
                return false;
            }

            input = Encoding.ASCII.GetBytes(header + "." + Base64Url(payload));
            return key.VerifyData(input, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException or ArgumentException or PlatformNotSupportedException)
        {
            return false;
        }
        finally
        {
            if (input is not null)
            {
                CryptographicOperations.ZeroMemory(input);
            }
        }
    }

    private static string Base64Url(ReadOnlySpan<byte> bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Decode(string value)
        => Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/') + new string('=', (4 - (value.Length % 4)) % 4));
}
