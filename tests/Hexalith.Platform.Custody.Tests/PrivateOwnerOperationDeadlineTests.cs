using Shouldly;

namespace Hexalith.Platform.Custody.Tests;

/// <summary>Actual private helper completion/cancellation cleanup behavior; no provider execution reclamation is inferred.</summary>
public sealed class PrivateOwnerOperationDeadlineTests
{
    /// <summary>A result that completes before the final check loses to controlled caller cancellation, with one asynchronous abandoned cleanup only.</summary>
    [Fact]
    public async Task CancellationAtCompletedResultRunsAbandonedCleanupOnlyOnce()
    {
        var clock = new CompletionCancellationClock(); using var caller = new CancellationTokenSource();
        var budget = new PrivateOwnerOperationDeadline(clock, caller.Token);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanupEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanupFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cleanupRelease = new ManualResetEventSlim(); int cleanups = 0;
        var pending = budget.ReadAsync(async () => { entered.TrySetResult(); int value = await result.Task; clock.AtCompletedRead = caller.Cancel; return value; }, value =>
        { value.ShouldBe(17); Interlocked.Increment(ref cleanups); cleanupEntered.TrySetResult(); cleanupRelease.Wait(); cleanupFinished.TrySetResult(); });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await clock.WaitBudgetRead.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); result.SetResult(17);
        try
        {
            (await Should.ThrowAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken))).CancellationToken.ShouldBe(caller.Token);
            await cleanupEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); cleanups.ShouldBe(1); cleanupFinished.Task.IsCompleted.ShouldBeFalse();
        }
        finally { cleanupRelease.Set(); }
        await cleanupFinished.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); cleanups.ShouldBe(1);
    }
}
