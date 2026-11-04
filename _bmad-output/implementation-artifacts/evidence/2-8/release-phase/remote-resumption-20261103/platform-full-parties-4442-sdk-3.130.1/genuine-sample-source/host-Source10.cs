using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Sample.Counter.Events;
/// <summary>
/// Terminal event indicating the counter has been permanently closed (FR66).
/// Once applied, the aggregate is tombstoned and rejects all further commands.
/// </summary>
public sealed record CounterClosed : IEventPayload;
