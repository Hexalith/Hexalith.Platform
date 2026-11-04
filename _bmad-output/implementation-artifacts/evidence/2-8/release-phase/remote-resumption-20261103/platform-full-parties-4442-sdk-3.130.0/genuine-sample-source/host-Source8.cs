
using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Sample.Counter.Events;
/// <summary>
/// Event indicating the counter was decremented by one.
/// </summary>
public sealed record CounterDecremented : IEventPayload;
