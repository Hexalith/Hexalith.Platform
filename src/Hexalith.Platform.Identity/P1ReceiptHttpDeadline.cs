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
    private readonly Task _monitor;

    internal P1ReceiptHttpDeadline(P1BootTimeClock clock, long chainStart, DateTimeOffset authenticatedUtc,
        CancellationToken callerToken)
    {
        _clock = clock;
        _chainStart = chainStart;
        _authenticatedUtc = authenticatedUtc;
        _source = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
        _requestStart = clock.TryRead(out long now) ? now : -1;
        if (_requestStart < 0 || !Valid)
        {
            _source.Cancel();
        }

        _monitor = Task.Run(MonitorAsync);
    }

    internal CancellationToken Token => _source.Token;

    internal bool Valid => TryAuthenticatedUtc(out _);

    internal bool TryAuthenticatedUtc(out DateTimeOffset now)
    {
        now = default;
        if (_source.IsCancellationRequested || _requestStart < 0 || !_clock.TryRead(out long tick)
            || tick < _chainStart || tick < _requestStart
            || tick - _requestStart >= RequestNanoseconds || tick - _chainStart >= ChainNanoseconds)
        {
            return false;
        }

        try
        {
            now = _authenticatedUtc.AddTicks((tick - _chainStart) / 100);
            return now.Offset == TimeSpan.Zero;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    /// <summary>Polling reads CLOCK_BOOTTIME after resume and cancels pending transport operations.</summary>
    private async Task MonitorAsync()
    {
        try
        {
            while (!_source.IsCancellationRequested)
            {
                await Task.Delay(20, _source.Token).ConfigureAwait(false);
                if (!Valid)
                {
                    _source.Cancel();
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        _source.Cancel();
        await _monitor.ConfigureAwait(false);
        _source.Dispose();
    }
}
