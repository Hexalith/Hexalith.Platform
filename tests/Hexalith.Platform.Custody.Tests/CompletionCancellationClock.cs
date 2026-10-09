namespace Hexalith.Platform.Custody.Tests;

/// <summary>Controls cancellation/expiry after an actually completed provider result and after the wait duration was captured.</summary>
internal sealed class CompletionCancellationClock : TimeProvider
{
    private int _reads;
    internal TaskCompletionSource WaitBudgetRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Action? AtCompletedRead { get; set; }
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp()
    {
        if (Interlocked.Increment(ref _reads) >= 3) { WaitBudgetRead.TrySetResult(); }
        if (AtCompletedRead is { } action) { action(); return TimeSpan.FromSeconds(30).Ticks; }
        return 0;
    }
}
