namespace Hexalith.Platform.Composition;

// The explicit composition scope owns this from first acquisition. Successful
// Build resolves the same factory-owned instance into the host's disposal scope.
internal sealed class AcceptedLaunchLeaseOwner:IDisposable
{
    private readonly List<AcceptedLaunchArtifactLease> _leases=[];
    private bool _disposed;
    internal void Add(AcceptedLaunchArtifactLease lease)
    {
        if(_disposed) { lease.Dispose();throw new ObjectDisposedException(nameof(AcceptedLaunchLeaseOwner)); }
        _leases.Add(lease);
    }
    public void Dispose()
    {
        if(_disposed) return;
        _disposed=true;
        foreach(var lease in _leases) lease.Dispose();
    }
}
