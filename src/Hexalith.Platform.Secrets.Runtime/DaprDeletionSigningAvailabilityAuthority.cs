using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Adds an independently authenticated read-only availability grant to an existing qualified signing/transition authority.</summary>
public sealed class DaprDeletionSigningAvailabilityAuthority(IDeletionCapabilitySigningAuthority original,
    IndependentDaprAuthority availabilityAuthority, string providerIdentity, TimeProvider? clock = null) : IDeletionCapabilitySigningAuthority
{
    /// <inheritdoc />
    public async Task<bool> AuthorizeReadinessAsync(DeletionCapabilitySigningReadinessRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ProviderIdentity != providerIdentity || string.IsNullOrWhiteSpace(providerIdentity)) { return false; }
        var grant = await availabilityAuthority.InvokeAsync<DeletionCapabilitySigningAvailability>("signing-readiness-permission", request, cancellationToken).ConfigureAwait(false);
        var now = (clock ?? TimeProvider.System).GetUtcNow();
        string[] required = ["SignDeletionCapability", "LookupDeletionCapability", "ObsoleteUnissued", "RecoverSigningSuccessor"];
        return grant is not null && grant.Request == request && grant.ProviderIdentity == providerIdentity && !string.IsNullOrWhiteSpace(grant.AuthorityRevision)
            && grant.ObservedAt <= now && now < grant.ValidUntil && grant.ValidUntil - grant.ObservedAt <= TimeSpan.FromSeconds(30)
            && grant.Operations is not null && grant.Operations.Length == required.Length && grant.Operations.Order(StringComparer.Ordinal).SequenceEqual(required.Order(StringComparer.Ordinal));
    }
    /// <inheritdoc />
    public Task<bool> AuthorizeOperationAsync(DeletionBatchCapabilityV1 payload, string signingRequestId, string operation, CancellationToken cancellationToken = default)
        => original.AuthorizeOperationAsync(payload, signingRequestId, operation, cancellationToken);
    /// <inheritdoc />
    public Task<bool> AuthorizeAsync(DeletionBatchCapabilityV1 payload, string signingRequestId, CancellationToken cancellationToken = default) => original.AuthorizeAsync(payload, signingRequestId, cancellationToken);
    /// <inheritdoc />
    public Task<bool> ValidateStateAsync(DeletionBatchCapabilityV1 identity, string signingRequestId, string exactStateDigest, CancellationToken cancellationToken = default) => original.ValidateStateAsync(identity, signingRequestId, exactStateDigest, cancellationToken);
    /// <inheritdoc />
    public Task<bool> RecordStateAsync(DeletionBatchCapabilityV1 identity, string signingRequestId, string expectedStateDigest, string nextStateDigest, CancellationToken cancellationToken = default) => original.RecordStateAsync(identity, signingRequestId, expectedStateDigest, nextStateDigest, cancellationToken);
    /// <inheritdoc />
    public Task<AnchoredStateTransition?> ReadAdmittedOriginalAsync(string scopeId, CancellationToken cancellationToken = default) => original.ReadAdmittedOriginalAsync(scopeId, cancellationToken);
    /// <inheritdoc />
    public Task<AnchoredStateTransition?> ReadCurrentOriginalAsync(string scopeId, CancellationToken cancellationToken = default) => original.ReadCurrentOriginalAsync(scopeId, cancellationToken);
    /// <inheritdoc />
    public Task<bool> AdmitTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default) => original.AdmitTransitionAsync(transition, cancellationToken);
    /// <inheritdoc />
    public Task<bool> RecordTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default) => original.RecordTransitionAsync(transition, cancellationToken);
    /// <inheritdoc />
    public Task<bool> RecoverTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default) => original.RecoverTransitionAsync(transition, cancellationToken);
    /// <inheritdoc />
    public Task<bool> VerifyTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default) => original.VerifyTransitionAsync(transition, cancellationToken);
}
