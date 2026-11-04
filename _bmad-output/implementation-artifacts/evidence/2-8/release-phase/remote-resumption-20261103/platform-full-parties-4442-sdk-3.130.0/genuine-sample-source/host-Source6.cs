
using Hexalith.EventStore.Contracts.Aggregates;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Sample.Counter.Events;

namespace Hexalith.EventStore.Sample.Counter.State;
/// <summary>
/// Aggregate state for the Counter domain. Tracks the current count value
/// and applies events to reconstruct state from event replay.
/// Implements <see cref="ITerminatable"/> for tombstoning support (FR66).
/// </summary>
public sealed class CounterState : ITerminatable {
    // Only two scalar fields are retained; no serializer/converter or nested graph is involved.
    internal CounterState DetachedCopy() => new() { Count = Count, IsTerminated = IsTerminated };

    /// <summary>Gets the current count value.</summary>
    public int Count { get; private set; }

    /// <summary>Gets a value indicating whether this counter has been permanently closed.</summary>
    public bool IsTerminated { get; private set; }

    /// <summary>Applies a counter incremented event.</summary>
    public void Apply(CounterIncremented e) => Count++;

    /// <summary>Applies a counter decremented event.</summary>
    public void Apply(CounterDecremented e) => Count--;

    /// <summary>Applies a counter reset event.</summary>
    public void Apply(CounterReset e) => Count = 0;

    /// <summary>Applies a counter closed event, marking the aggregate as terminated.</summary>
    public void Apply(CounterClosed e) => IsTerminated = true;

    /// <summary>No-op — required because rejection events are persisted to the event stream and replayed during rehydration.</summary>
    public void Apply(AggregateTerminated e) {
        // No-op — AggregateTerminated is a framework rejection event persisted after tombstoning
    }
}
