namespace Hexalith.Platform.Identity;

/// <summary>One GET deadline and the shared suspend-aware dependency-chain deadline.</summary>
internal sealed class P1ReceiptHttpDeadline : IAsyncDisposable
{
    private const long RequestNanoseconds = 10_000_000_000;
    private const long ChainNanoseconds = 300_000_000_000;
    private readonly P1BootTimeClock _clock;
    private readonly long _chainStart;
    private readonly long _requestStart;
    private readonly DateTimeOffset _authenticatedUtc;
    private readonly CancellationTokenSource _source;
    private readonly object _cancelGate = new();
    private Task? _cancelTask;
    private int _deadlineFailed;
    private int _disposed;

    internal P1ReceiptHttpDeadline(P1BootTimeClock clock, long chainStart, DateTimeOffset authenticatedUtc,
        CancellationToken callerToken)
    {
        _clock = clock;
        _chainStart = chainStart;
        _authenticatedUtc = authenticatedUtc;
        _source = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
        _requestStart = clock.TryRead(out long now) ? now : -1;
        if (_requestStart < 0 || !Valid || !P1ReceiptHttpDeadlineMonitor.TryRegister(this))
        {
            FailDeadline();
        }
    }

    internal CancellationToken Token => _source.Token;

    internal bool Valid => TryAuthenticatedUtc(out _);

    internal bool DeadlineFailed => Volatile.Read(ref _deadlineFailed) != 0;

    internal bool TryAuthenticatedUtc(out DateTimeOffset now)
    {
        now = default;
        if (DeadlineFailed || Volatile.Read(ref _disposed) != 0 || _source.IsCancellationRequested)
        {
            return false;
        }

        if (_requestStart < 0 || !_clock.TryRead(out long tick)
            || tick < _chainStart || tick < _requestStart
            || tick - _requestStart >= RequestNanoseconds || tick - _chainStart >= ChainNanoseconds)
        {
            FailDeadline();
            return false;
        }

        try
        {
            now = _authenticatedUtc.AddTicks((tick - _chainStart) / 100);
            if (now.Offset == TimeSpan.Zero)
            {
                return true;
            }
        }
        catch (ArgumentOutOfRangeException)
        {
        }

        FailDeadline();
        return false;
    }

    internal void Poll() => _ = Valid;

    private void FailDeadline()
    {
        if (Interlocked.Exchange(ref _deadlineFailed, 1) == 0)
        {
            _ = StartCancellation();
        }
    }

    // CancelAsync marks the token cancelled synchronously but runs callbacks away from
    // the sole boottime monitor. Keep the registration until callbacks really exit.
    private Task StartCancellation()
    {
        lock (_cancelGate)
        {
            return _cancelTask ??= _source.CancelAsync();
        }
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return ValueTask.CompletedTask;
        }

        Task cancellation = StartCancellation();
        _ = cancellation.ContinueWith(completed =>
        {
            if (completed.IsFaulted) _ = completed.Exception;
            P1ReceiptHttpDeadlineMonitor.Unregister(this);
            _source.Dispose();
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return ValueTask.CompletedTask;
    }
}
