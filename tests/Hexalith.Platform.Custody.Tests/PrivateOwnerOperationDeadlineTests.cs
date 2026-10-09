using NSubstitute;
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
    /// <summary>A clock crossing the budget after Check cannot construct a negative or infinite wait on either helper.</summary>
    [Theory]
    [InlineData(false, 0)][InlineData(false, 1)][InlineData(false, 10000)][InlineData(false, 20000)]
    [InlineData(true, 0)][InlineData(true, 1)][InlineData(true, 10000)][InlineData(true, 20000)]
    public async Task NonpositiveRemainingTimeIsTimeoutWithSafeLateCompletion(bool turnOwned, long overrunTicks)
    {
        var clock = Substitute.For<TimeProvider>(); clock.TimestampFrequency.Returns(TimeSpan.TicksPerSecond);
        int reads = 0; clock.GetTimestamp().Returns(_ => Interlocked.Increment(ref reads) >= 3 ? TimeSpan.FromSeconds(30).Ticks + overrunTicks : 0L);
        var deadline = new PrivateOwnerOperationDeadline(clock, CancellationToken.None);
        var pending = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleaned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); int cleanups = 0;
        Task operation = turnOwned ? deadline.WaitAsync(pending.Task)
            : deadline.ReadAsync(() => { started.TrySetResult(); return pending.Task; }, value => { value.ShouldBe(17); Interlocked.Increment(ref cleanups); cleaned.TrySetResult(); });
        try
        {
            var error = await Should.ThrowAsync<TimeoutException>(() => operation.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
            error.Message.ShouldBe("Private owner operation unavailable."); operation.IsCompleted.ShouldBeTrue();
            pending.Task.IsCompleted.ShouldBeFalse();
            clock.DidNotReceive().CreateTimer(Arg.Any<TimerCallback>(), Arg.Any<object?>(), Arg.Any<TimeSpan>(), Arg.Any<TimeSpan>());
        }
        finally { pending.TrySetResult(17); }
        if (!turnOwned) { await started.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken); await cleaned.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken); cleanups.ShouldBe(1); }
        else { (await pending.Task).ShouldBe(17); cleanups.ShouldBe(0); }
    }

}
