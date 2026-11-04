using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Sample.Counter.Commands;
using Hexalith.EventStore.Sample.Counter.Queries;

[assembly: RouteCatalogDeclarationSource(typeof(Hexalith.EventStore.Sample.Counter.CounterCatalogDeclarations))]

namespace Hexalith.EventStore.Sample.Counter;

/// <summary>Owner-approved v1 intent policies and v2 route declarations; no runtime activation.</summary>
public static class CounterCatalogDeclarations
{
    /// <summary>Gets complete compiled declarations consumed by generated hosts.</summary>
    public static IReadOnlyList<RouteContractDeclaration> Routes => [.. Commands.Select(c => c.Route), Query];
    /// <summary>Gets actual receiver methods; the canonical projection actor query belongs to the gateway.</summary>
    public static IReadOnlyDictionary<string, string> ReceiverMethods => Routes.ToDictionary(route => route.RouteId,
        route => route.Execution?.TargetKind == "projection-actor" ? "QueryAsync" : route.Kind == "command" ? "process" : "project",
        StringComparer.Ordinal);

    /// <summary>Gets all four owner-approved MessageId v1, Mutation/24-hour policies.</summary>
    public static IReadOnlyList<CommandIntentDeclaration> Commands { get; } = [
        Command<IncrementCounter>(), Command<DecrementCounter>(), Command<ResetCounter>(), Command<CloseCounter>()];

    /// <summary>Gets the query that uses EventStore's ProjectionActor, not a Sample HTTP handler.</summary>
    public static RouteContractDeclaration Query { get; } = new("counter-status-query", "query", GetCounterStatusQuery.Domain,
        GetCounterStatusQuery.QueryType, GetCounterStatusQuery.ProjectionType, "v1", null, "workload", EventStoreOperationIdentifiers.ProjectionQuery, null, [],
        new(typeof(GetCounterStatusQuery).FullName!, "exact", [GetCounterStatusQuery.QueryType], "projection-actor", "projection", [], ActorType: "ProjectionActor"));

    /// <summary>Gets the existing UI-only Counter operation schemas without granting agent ingress.</summary>
    public static IReadOnlyList<RouteOperationContract> Operations => Routes.Select(route =>
        RouteOperationSchema.Create(route,
            route.Kind == "query" ? typeof(GetCounterStatusQuery)
                : typeof(CounterCatalogDeclarations).Assembly.GetType(route.Execution!.HandlerType, throwOnError: true)!,
            route.Kind == "query" ? typeof(CounterStatusPayload) : typeof(SubmitCommandResponse),
            "public", "UI-only", System.Text.Json.JsonSerializerOptions.Web)).ToArray();

    private static CommandIntentDeclaration Command<T>() where T : ICommandContract
    {
        string operation = T.CommandType;
        return new(new("counter-" + operation, "command", T.Domain, T.CommandType, null, "v1", 1,
            "workload", EventStoreOperationIdentifiers.DomainServiceProcess, null, [],
            new(typeof(T).FullName!, "clr-short-name-v1", [T.CommandType, typeof(T).Name], "domain-service", null, [])),
            $"hexalith:counter:{operation}:intent-v1", $"counter:{operation}", $"counter:{operation}:policy-v1",
            new($"hexalith:counter:{operation}:descriptor-v1", 1, [new("target", "string")]), IdempotencyReplayRetentionTier.Mutation, false);
    }
}
