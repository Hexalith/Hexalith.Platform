namespace Hexalith.Platform.Secrets.Runtime;

// One operation owns one deadline. Nested stages share this instance, never reset its thirty seconds.
internal sealed class CustodyOperationBudget : IDisposable
{
    private readonly TimeProvider _clock;
    private readonly long _started;
    private readonly CancellationTokenSource _deadline, _linked;
    internal CustodyOperationBudget(CancellationToken caller, TimeProvider clock)
    {
        Caller = caller; _clock = clock; _started = clock.GetTimestamp();
        _deadline = new(TimeSpan.FromSeconds(30), clock);
        _linked = CancellationTokenSource.CreateLinkedTokenSource(caller, _deadline.Token);
    }
    internal CancellationToken Caller { get; }
    internal CancellationToken Token => _linked.Token;
    internal TimeSpan Remaining
    {
        get
        {
            Check();
            return TimeSpan.FromSeconds(30) - _clock.GetElapsedTime(_started);
        }
    }
    internal void Check()
    {
        Caller.ThrowIfCancellationRequested();
        Token.ThrowIfCancellationRequested();
        if (_clock.GetElapsedTime(_started) >= TimeSpan.FromSeconds(30)) throw new OperationCanceledException(Token);
    }
    public void Dispose() { _linked.Dispose(); _deadline.Dispose(); }
}
