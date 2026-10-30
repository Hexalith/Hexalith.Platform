namespace Hexalith.Platform.Protection;

/// <summary>Bounded by the actual outstanding queue; no holder, tenant, payload or credential identities enter diagnostics.</summary>
public sealed class ErasureRootCompletionDiagnostics
{
    private readonly Dictionary<object, long> _pending = new(ReferenceEqualityComparer.Instance);
    private readonly object _gate = new();
    private string _failure = "None";
    internal void Track(object holder) { lock (_gate) { _pending.TryAdd(holder, System.Diagnostics.Stopwatch.GetTimestamp()); } }
    internal void Complete(object holder) { lock (_gate) { _pending.Remove(holder); if (_pending.Count == 0) { _failure = "None"; } } }
    internal void Refused() { lock (_gate) { if (_pending.Count != 0) { _failure = "RefusedOrUnavailable"; } } }
    internal void Failed() { lock (_gate) { if (_pending.Count != 0) { _failure = "OperationUnavailable"; } } }
    /// <summary>Reads current count, age and a closed safe refusal/failure class without any native operation.</summary>
    public ErasureRootHolderCompletionHealth Read()
    {
        lock (_gate)
        {
            return new(_pending.Count, _pending.Count == 0 ? 0 : System.Diagnostics.Stopwatch.GetElapsedTime(_pending.Values.Min()).TotalSeconds, _failure);
        }
    }
}
