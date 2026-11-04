using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Rest;

namespace Hexalith.EventStore.Sample.Counter.Commands;

/// <summary>
/// Command to permanently close the counter. Once closed, the aggregate
/// is tombstoned and rejects all further commands (FR66).
/// Implements <see cref="ICommandContract"/> and carries the REST route metadata consumed by the
/// generated command controller in the external-facing API host (<c>Hexalith.EventStore.Sample.Api</c>).
/// Tombstoning is proven by generated source, tests, and smoke — it is intentionally not exposed as a
/// casual demo UI action.
/// </summary>
/// <param name="CounterId">The counter aggregate identifier this command targets.</param>
[RestRoute(RestVerb.Post, "{counterId}/close", ApiScope = "counter")]
public sealed record CloseCounter(string CounterId = "counter-1") : ICommandContract
{
    /// <inheritdoc/>
    public static string Domain => "counter";

    /// <inheritdoc/>
    public static string CommandType => "close-counter";

    /// <inheritdoc/>
    public string AggregateId => CounterId;
}
