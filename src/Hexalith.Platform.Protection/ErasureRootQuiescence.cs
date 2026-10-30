namespace Hexalith.Platform.Protection;

/// <summary>Owning-process admission barrier. Shutdown closes admission before waiting for every admitted acquisition/verification to leave its finally block.</summary>
internal sealed class ErasureRootQuiescence
{
    private readonly object _gate = new();
    private readonly TaskCompletionSource _idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _stopping;
    private int _active;
    internal ErasureRootOperationLease? Enter()
    {
        lock (_gate)
        {
            if (_stopping) { return null; }
            _active++;
            return new ErasureRootOperationLease(Leave);
        }
    }
    internal void Close()
    {
        lock (_gate) { _stopping = true; if (_active == 0) { _idle.TrySetResult(); } }
    }
    internal Task WaitAsync(CancellationToken token) { Close(); return _idle.Task.WaitAsync(token); }
    private void Leave()
    {
        lock (_gate) { _active--; if (_stopping && _active == 0) { _idle.TrySetResult(); } }
    }
}
