using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Rest;

namespace Hexalith.EventStore.Sample.Counter.Queries;

/// <summary>
/// Query contract for retrieving the current counter status.
/// Implements <see cref="IQueryContract"/> and carries the REST route metadata consumed by the
/// generated controller in the external-facing API host (<c>Hexalith.EventStore.Sample.Api</c>).
/// The interactive Blazor UI references this contract only for its static metadata and reaches
/// the projection through the platform gateway client.
/// </summary>
[RestRoute(RestVerb.Get, "{entityId}", ApiScope = "counter")]
public sealed record GetCounterStatusQuery : IQueryContract
{
    /// <inheritdoc/>
    public static string QueryType => "get-counter-status";

    /// <inheritdoc/>
    public static string Domain => "counter";

    /// <inheritdoc/>
    public static string ProjectionType => "counter";
}
