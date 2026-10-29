using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Protection;

/// <summary>Qualification-only exact lookup/acknowledgement controls around the actual independently anchored guard.</summary>
public sealed class ProtectionQualificationControlledGuard(IGovernanceScopeGuard native, ProtectionQualificationFaults controls) : IGovernanceScopeGuard
{
    /// <inheritdoc />
    public async Task<TenantGovernanceGuardState?> ReadAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        await controls.CheckAsync("guard:" + tenantId + ":read", "before", cancellationToken).ConfigureAwait(false);
        return await native.ReadAsync(tenantId, cancellationToken).ConfigureAwait(false);
    }
    /// <inheritdoc />
    public async Task<GovernanceProtocolReceipt?> ExecuteAsync(GovernanceGuardTransition transition, IReadOnlyList<GuardedStateMutation> targets, CancellationToken cancellationToken = default)
    {
        await controls.CheckAsync("guard:" + transition.TenantId + ":execute", "before", cancellationToken).ConfigureAwait(false);
        var result = await native.ExecuteAsync(transition, targets, cancellationToken).ConfigureAwait(false);
        await controls.CheckAsync("guard:" + transition.TenantId + ":execute", "after", cancellationToken).ConfigureAwait(false); return result;
    }
    /// <inheritdoc />
    public Task<GovernanceSigningRecovery?> ReadNoIssueAsync(DeletionBatchCapabilityV1 payload, string signingRequestId, string detachedJwsDigest, CancellationToken cancellationToken = default)
        => native.ReadNoIssueAsync(payload, signingRequestId, detachedJwsDigest, cancellationToken);
    /// <inheritdoc />
    public Task<GovernanceRejectedIssueOriginal?> ReadRejectedIssueAsync(DeletionBatchCapabilityV1 payload, string signingRequestId, string detachedJwsDigest, CancellationToken cancellationToken = default)
        => native.ReadRejectedIssueAsync(payload, signingRequestId, detachedJwsDigest, cancellationToken);
}
