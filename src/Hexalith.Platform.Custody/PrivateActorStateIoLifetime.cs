namespace Hexalith.Platform.Custody;

/// <summary>Turn-owned exclusion while an abandoned StateManager Task is unfinished. It grants no persistence or backend isolation authority.</summary>
internal sealed class PrivateActorStateIoLifetime
{
    private Task? _pending;
    internal void CheckReady()
    {
        if (_pending is { IsCompleted: false }) { throw new TimeoutException("Previous private actor state I/O is unfinished."); }
        if (_pending is not null) { _ = _pending.Exception; _pending = null; }
    }
    internal async Task<T> ReadAsync<T>(PrivateOwnerOperationDeadline budget, Func<Task<T>> operation)
    {
        budget.Check(); CheckReady();
        // Invocation remains on the actor turn. Only the returned task's wait is bounded.
        Task<T> pending = operation(); _pending = pending;
        return await budget.WaitAsync(pending).ConfigureAwait(false);
    }
    internal async Task ReadAsync(PrivateOwnerOperationDeadline budget, Func<Task> operation)
    {
        budget.Check(); CheckReady();
        Task pending = operation(); _pending = pending;
        await budget.WaitAsync(pending).ConfigureAwait(false);
    }
}
