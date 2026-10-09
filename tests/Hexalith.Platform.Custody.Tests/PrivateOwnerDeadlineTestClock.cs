using NSubstitute;

namespace Hexalith.Platform.Custody.Tests;

/// <summary>Controlled monotonic private-operation budget clock; timer callbacks fire only when the test advances its retained operational time.</summary>
internal static class PrivateOwnerDeadlineTestClock
{
    /// <summary>Creates a scoped deterministic clock and its explicit elapsed-time/timer advancement.</summary>
    internal static (TimeProvider Clock, Action<TimeSpan> Advance) Create()
    {
        var clock = Substitute.For<TimeProvider>(); long ticks = 0;
        var callbacks = new System.Collections.Concurrent.ConcurrentDictionary<int, (long Due, Action Fire)>(); int timerId = 0;
        var observations = new System.Collections.Concurrent.ConcurrentDictionary<int, long>();
        clock.TimestampFrequency.Returns(TimeSpan.TicksPerSecond); clock.GetTimestamp().Returns(_ =>
        {
            long observed = Interlocked.Read(ref ticks); observations[Environment.CurrentManagedThreadId] = observed; return observed;
        });
        clock.GetUtcNow().Returns(_ => new DateTimeOffset(2026, 10, 9, 0, 0, 0, TimeSpan.Zero).AddTicks(Interlocked.Read(ref ticks)));
        clock.CreateTimer(Arg.Any<TimerCallback>(), Arg.Any<object?>(), Arg.Any<TimeSpan>(), Arg.Any<TimeSpan>()).Returns(call =>
        {
            int id = Interlocked.Increment(ref timerId); var timer = Substitute.For<ITimer>();
            // ReadAsync computes remaining time from the immediately preceding timestamp observation.
            // A clock advance between that observation and registration must not restart its deadline.
            long basis = observations.GetValueOrDefault(Environment.CurrentManagedThreadId, Interlocked.Read(ref ticks));
            callbacks[id] = (basis + call.ArgAt<TimeSpan>(2).Ticks, () => call.Arg<TimerCallback>()(call.ArgAt<object?>(1)));
            timer.When(t => t.Dispose()).Do(callInfo => callbacks.TryRemove(id, out _));
            timer.DisposeAsync().Returns(callInfo => { callbacks.TryRemove(id, out _); return ValueTask.CompletedTask; });
            if (callbacks[id].Due <= Interlocked.Read(ref ticks)) { callbacks[id].Fire(); }
            return timer;
        });
        return (clock, elapsed =>
        {
            long current = Interlocked.Add(ref ticks, elapsed.Ticks);
            foreach (var callback in callbacks.Values.ToArray()) { if (callback.Due <= current) { callback.Fire(); } }
        });
    }
}
