using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;

namespace Hexalith.EventStore.Sample.Counter.Projections;

/// <summary>
/// Counter projection seam discovered and wired by the domain-service SDK. The SDK maps the canonical
/// <c>/project</c> endpoint and dispatches full-replay projection requests for the <c>counter</c> domain
/// here — the domain no longer hand-maps <c>/project</c> in <c>Program.cs</c>. The replay logic itself lives
/// in <see cref="CounterProjectionHandler"/>.
/// </summary>
public sealed class CounterProjection : IDomainProjectionHandler {
    /// <inheritdoc/>
    public string Domain => "counter";

    /// <inheritdoc/>
    public ProjectionResponse Project(ProjectionRequest request) => CounterProjectionHandler.Project(request);
}
