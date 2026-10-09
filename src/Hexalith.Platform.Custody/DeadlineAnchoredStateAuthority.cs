using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Custody;

/// <summary>Preserves the private whole-operation deadline across independent journal reads and conditional records.</summary>
internal sealed class DeadlineAnchoredStateAuthority(IAnchoredStateTransitionAuthority authority, PrivateOwnerOperationDeadline deadline) : IAnchoredStateTransitionAuthority
{
    /// <inheritdoc/>
    public Task<bool> AdmitTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => deadline.ReadAsync(() => authority.AdmitTransitionAsync(transition, CancellationToken.None));
    /// <inheritdoc/>
    public Task<bool> RecoverTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => deadline.ReadAsync(() => authority.RecoverTransitionAsync(transition, CancellationToken.None));
    /// <inheritdoc/>
    public Task<bool> RecordTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => deadline.ReadAsync(() => authority.RecordTransitionAsync(transition, CancellationToken.None));
    /// <inheritdoc/>
    public Task<bool> VerifyTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => deadline.ReadAsync(() => authority.VerifyTransitionAsync(transition, CancellationToken.None));
}
