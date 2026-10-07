using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Custody;

/// <summary>
/// Bounds one expired-unit cleanup through existing custody, without owning keys, scheduling or receipt persistence.
/// The provider must authenticate the exact tenant/unit and resolve idempotent irreversible outcomes across all copies.
/// </summary>
public sealed class IdentityHistoryCleanup(TimeProvider clock, IIdentityHistoryCustody? custody = null)
{
    private static readonly TimeSpan _providerWait = TimeSpan.FromSeconds(5);
    private readonly TimeProvider _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    /// <summary>
    /// Attempts cleanup only after immutable exclusive expiry; unknown outcomes remain pending.
    /// Retries must pass the original evidence unchanged. Confirmation does not qualify a production backend.
    /// </summary>
    public async Task<IdentityHistoryCleanupOutcome> ProcessAsync(AggregateIdentity identity,
        IdentityHistoryPolicy policy, DateTimeOffset effectiveAt, IdentityHistoryCustodyEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (identity is null || identity.Domain != "party" || policy is null || evidence is null
            || !evidence.Satisfies(policy, effectiveAt))
        {
            return IdentityHistoryCleanupOutcome.Invalid;
        }

        if (_clock.GetUtcNow() < evidence.ExpiresAt)
        {
            return IdentityHistoryCleanupOutcome.NotExpired;
        }

        if (custody is null)
        {
            return IdentityHistoryCleanupOutcome.Pending;
        }

        try
        {
            bool destroyed = await AwaitProviderAsync(
                () => custody.DestroyExpiredAsync(identity, evidence, cancellationToken), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!destroyed)
            {
                return IdentityHistoryCleanupOutcome.Pending;
            }

            bool readable = await AwaitProviderAsync(
                () => custody.CanReadAsync(identity, evidence, cancellationToken), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return !readable && _clock.GetUtcNow() >= evidence.ExpiresAt
                ? IdentityHistoryCleanupOutcome.ProviderConfirmedDestroyed
                : IdentityHistoryCleanupOutcome.Pending;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return IdentityHistoryCleanupOutcome.Pending;
        }
    }

    private static async Task<bool> AwaitProviderAsync(Func<Task<bool>> operation, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Task<bool> pending = Task.Run(operation, token);
        try
        {
            return await pending.WaitAsync(_providerWait, token).ConfigureAwait(false);
        }
        catch (Exception)
        {
            _ = ObserveLateAsync(pending);
            throw;
        }
    }

    private static async Task ObserveLateAsync(Task<bool> pending)
    {
        try
        {
            _ = await pending.ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Observe unknown outcomes without disclosing provider diagnostics or fabricating a receipt.
        }
    }
}
