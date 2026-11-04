using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Rest;

namespace Hexalith.EventStore.Sample.Counter.Commands;

/// <summary>
/// Command to increment the counter by one.
/// Implements <see cref="ICommandContract"/> and carries the REST route metadata consumed by the
/// generated command controller in the external-facing API host (<c>Hexalith.EventStore.Sample.Api</c>).
/// The interactive Blazor UI submits this command through the platform gateway client.
/// </summary>
/// <param name="CounterId">The counter aggregate identifier this command targets.</param>
[RestRoute(RestVerb.Post, "{counterId}/increment", ApiScope = "counter")]
public sealed record IncrementCounter(string CounterId = "counter-1") : ICommandContract
{
    /// <inheritdoc/>
    public static string Domain => "counter";

    /// <inheritdoc/>
    public static string CommandType => "increment-counter";

    /// <inheritdoc/>
    public string AggregateId => CounterId;
}
