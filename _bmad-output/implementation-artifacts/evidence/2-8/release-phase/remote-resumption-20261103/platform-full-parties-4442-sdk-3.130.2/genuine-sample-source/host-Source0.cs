using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Sample.Counter.Commands;
using Hexalith.EventStore.Sample.Counter.Projections;
using Hexalith.EventStore.Sample.Greeting;
using Hexalith.EventStore.Sample.Greeting.Commands;

[assembly: RouteCatalogDeclarationSource(typeof(Hexalith.EventStore.Sample.Counter.SampleCatalog))]

namespace Hexalith.EventStore.Sample.Counter;

/// <summary>Offline owner inventory. It does not register adapters, enable admission, or provision secrets.</summary>
public static class SampleCatalog
{
    /// <summary>Gets actual receiver methods; the canonical projection actor query belongs to the gateway.</summary>
    public static IReadOnlyDictionary<string, string> ReceiverMethods => Routes.ToDictionary(route => route.RouteId,
        route => route.Execution?.TargetKind == "projection-actor" ? "QueryAsync" : route.Kind == "command" ? "process" : "project",
        StringComparer.Ordinal);

    /// <summary>Gets all actual command policies.</summary>
    public static IReadOnlyList<CommandIntentDeclaration> Commands { get; } = [.. CounterCatalogDeclarations.Commands, GreetingCatalogDeclarations.Command];

    /// <summary>Gets the one callable projection and its actual output.</summary>
    public static RouteContractDeclaration Projection { get; } = new("counter-projection", "projection", "counter", null, "counter", "v1", null,
        "workload", EventStoreOperationIdentifiers.DomainServiceProject, null, [],
        new(typeof(CounterProjection).FullName!, "exact", ["counter"], "domain-service", null, ["counter"]));

    /// <summary>Gets the complete Sample route set.</summary>
    public static IReadOnlyList<RouteContractDeclaration> Routes { get; } = [.. Commands.Select(c => c.Route), CounterCatalogDeclarations.Query, Projection];

    /// <summary>Gets complete schema-three operations for the existing public UI and internal projection routes.</summary>
    public static IReadOnlyList<RouteOperationContract> Operations => [
        .. CounterCatalogDeclarations.Operations,
        RouteOperationSchema.Create(GreetingCatalogDeclarations.Command.Route, typeof(SendGreeting), typeof(SubmitCommandResponse),
            "public", "UI-only", System.Text.Json.JsonSerializerOptions.Web),
        RouteOperationSchema.Create(Projection, typeof(ProjectionRequest), typeof(ProjectionResponse),
            "internal", "UI-only", System.Text.Json.JsonSerializerOptions.Web)];

    /// <summary>Returns the existing target-only owner adapters without activating hosted admission.</summary>
    public static IReadOnlyList<IDeclaredIdempotencyIntentAdapter> CreateAdapters() => [
        Adapter<IncrementCounter>(), Adapter<DecrementCounter>(), Adapter<ResetCounter>(), Adapter<CloseCounter>(), Adapter<SendGreeting>()];

    private static DeclaredIdempotencyIntentAdapter<T> Adapter<T>() => new(
        Commands.Single(c => c.Route.Execution!.HandlerType == typeof(T).FullName), _ => new Dictionary<string, object?>());
}
