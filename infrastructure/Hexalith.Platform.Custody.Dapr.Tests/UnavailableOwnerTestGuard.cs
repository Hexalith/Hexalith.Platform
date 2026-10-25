using Hexalith.EventStore.Contracts.Security;

/// <summary>Owner bridge component checks must never query guard state as owner authority.</summary>
internal sealed class UnavailableOwnerTestGuard : IGovernanceScopeGuard
{
    /// <inheritdoc />
    public Task<TenantGovernanceGuardState?> ReadAsync(string tenantId, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Guard revision is not owner currentness.");

    /// <inheritdoc />
    public Task<GovernanceProtocolReceipt?> ExecuteAsync(GovernanceGuardTransition transition, IReadOnlyList<GuardedStateMutation> targets, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Owner bridge cannot mutate a guard.");

    /// <inheritdoc />
    public Task<GovernanceSigningRecovery?> ReadNoIssueAsync(DeletionBatchCapabilityV1 payload, string signingRequestId, string detachedJwsDigest, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Guard recovery is separate from owner recovery.");
}
