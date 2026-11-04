
using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Sample.Counter.Events;
/// <summary>
/// Event indicating the counter was reset to zero.
/// </summary>
public sealed record CounterReset : IEventPayload;
