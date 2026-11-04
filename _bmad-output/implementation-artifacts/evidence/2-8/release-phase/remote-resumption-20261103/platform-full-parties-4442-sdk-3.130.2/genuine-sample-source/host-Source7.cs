
using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Sample.Counter.Events;
/// <summary>
/// Event indicating the counter was incremented by one.
/// </summary>
public sealed record CounterIncremented : IEventPayload;
