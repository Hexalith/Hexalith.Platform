using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.Memories.EventStore.Domain.Commands;

[assembly: RouteCatalogDeclarationSource(typeof(Hexalith.Memories.EventStore.Routing.MemoriesCatalogDeclarations))]

namespace Hexalith.Memories.EventStore.Routing;

/// <summary>The six existing internal service commands; no REST or MCP operation is added.</summary>
public static class MemoriesCatalogDeclarations
{
    /// <summary>Exact existing command payload schemas; every operation remains an internal workload operation.</summary>
    public static IReadOnlyList<RouteOperationContract> Operations => Routes.Select(route => RouteOperationSchema.Create(route,
        typeof(MemoriesCatalogDeclarations).Assembly.GetType(route.Execution!.HandlerType, throwOnError: true)!,
        typeof(Hexalith.EventStore.Contracts.Results.DomainServiceWireResult), "internal", "UI-only",
        new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))).ToArray();
    /// <summary>Actual owner receiver methods, selected without changing declaration identities.</summary>
    public static IReadOnlyDictionary<string, string> ReceiverMethods => Routes.ToDictionary(route => route.RouteId,
        route => route.Kind switch { "command" => "process", "query" => "query", _ => "project/v2" }, StringComparer.Ordinal);

    /// <summary>Owner-approved local Mutation/24-hour policies covering canonical target and the complete typed payload.</summary>
    public static IReadOnlyList<CommandIntentDeclaration> Commands { get; } = [
        Command<RegisterTenantCommand>(),
        Command<UpdateTenantLifecycleStatusCommand>(),
        Command<CreateCaseCommand>(),
        Command<DeleteCaseCommand>(),
        Command<RequestAnnotationCommand>(),
        Command<DeleteMemoryUnitCommand>()];

    /// <summary>Complete existing command-only receiver inventory.</summary>
    public static IReadOnlyList<RouteContractDeclaration> Routes { get; } = Commands.Select(c => c.Route).ToArray();

    private static CommandIntentDeclaration Command<T>() where T : IMemoriesCommandContract
    {
        string operation = T.Domain + ":" + T.CommandType;
        return new(new(T.Domain + "-" + T.CommandType, "command", T.Domain, T.CommandType, null, "v1", 1,
            "workload", EventStoreOperationIdentifiers.DomainServiceProcess, null, [],
            new(typeof(T).FullName!, "clr-short-name-v1", [T.CommandType, typeof(T).Name], "domain-service", null, [])),
            "hexalith:" + operation + ":intent-v1", operation, operation + ":local-policy-v1",
            new("hexalith:" + operation + ":descriptor-v1", 1, [new("target", "string"), new("payload-sha256", "string")]),
            IdempotencyReplayRetentionTier.Mutation, false);
    }
}

