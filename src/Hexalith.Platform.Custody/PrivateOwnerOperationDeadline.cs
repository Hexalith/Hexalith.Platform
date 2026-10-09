namespace Hexalith.Platform.Custody;

/// <summary>Private whole-operation wait budget; provider invocations/callbacks never retain the query continuation. It grants no execution-isolation/capacity policy.</summary>
internal sealed class PrivateOwnerOperationDeadline(TimeProvider clock, CancellationToken token)
{
    private readonly long _start = clock.GetTimestamp();
    /// <summary>Checks caller first and the thirty-second operational budget.</summary>
    internal void Check()
    { token.ThrowIfCancellationRequested(); if (clock.GetElapsedTime(_start) >= TimeSpan.FromSeconds(30)) { throw new TimeoutException("Private owner operation unavailable."); } }
    /// <summary>Bounds synchronous invocation and noncooperative tasks, observes faults and clears abandoned owned material away from the caller.</summary>
    internal async Task<T> ReadAsync<T>(Func<Task<T>> operation, Action<T>? abandoned = null, Action? notStarted = null)
    {
        try { Check(); } catch { notStarted?.Invoke(); throw; }
        var pending = Task.Run(operation, CancellationToken.None);
        try
        {
            var result = await pending.WaitAsync(TimeSpan.FromSeconds(30) - clock.GetElapsedTime(_start), clock, token).ConfigureAwait(false);
            Check(); return result;
        }
        catch (Exception)
        {
            _ = pending.ContinueWith(task => { try { if (task.IsCompletedSuccessfully) { abandoned?.Invoke(task.Result); } else { _ = task.Exception; } } catch (Exception) { /* Cleanup failure is observed; it cannot release evidence. */ } },
                CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default); token.ThrowIfCancellationRequested(); throw;
        }
    }
}
