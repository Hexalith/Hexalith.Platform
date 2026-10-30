namespace Hexalith.Platform.Protection;

/// <summary>Exactly-once release of an owning-process admission.</summary>
internal sealed class ErasureRootOperationLease(Action release) : IDisposable
{
    private Action? _release = release;
    public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
}
