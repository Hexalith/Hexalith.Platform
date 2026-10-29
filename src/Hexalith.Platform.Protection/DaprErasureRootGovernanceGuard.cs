using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Credential-bound current guard adapter to the installed guard owner, reusing the existing signed independent-authority transport.</summary>
public sealed class DaprErasureRootGovernanceGuard(IndependentDaprAuthority guard) : IGovernanceScopeGuard
{
    /// <inheritdoc />
    public async Task<TenantGovernanceGuardState?> ReadAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var state = await guard.InvokeAsync<TenantGovernanceGuardState>("erc-read-guard", new { tenantId }, cancellationToken).ConfigureAwait(false);
        return state?.TenantId == tenantId ? state : null;
    }
    /// <inheritdoc />
    public Task<GovernanceProtocolReceipt?> ExecuteAsync(GovernanceGuardTransition transition, IReadOnlyList<GuardedStateMutation> targets, CancellationToken cancellationToken = default)
        => guard.InvokeAsync<GovernanceProtocolReceipt>("erc-execute-guard", new { transition, targets }, cancellationToken);
    /// <inheritdoc />
    public Task<GovernanceSigningRecovery?> ReadNoIssueAsync(DeletionBatchCapabilityV1 payload, string signingRequestId, string detachedJwsDigest, CancellationToken cancellationToken = default)
        => guard.InvokeAsync<GovernanceSigningRecovery>("erc-read-no-issue", new { payload, signingRequestId, detachedJwsDigest }, cancellationToken);
    /// <inheritdoc />
    public Task<GovernanceRejectedIssueOriginal?> ReadRejectedIssueAsync(DeletionBatchCapabilityV1 payload, string signingRequestId, string detachedJwsDigest, CancellationToken cancellationToken = default)
        => guard.InvokeAsync<GovernanceRejectedIssueOriginal>("erc-read-rejected-issue", new { payload, signingRequestId, detachedJwsDigest }, cancellationToken);
}
