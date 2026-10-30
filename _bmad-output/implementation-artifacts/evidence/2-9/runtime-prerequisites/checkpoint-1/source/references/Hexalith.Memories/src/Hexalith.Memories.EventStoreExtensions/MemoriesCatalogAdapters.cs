using Hexalith.EventStore.DomainService;
using Hexalith.Memories.EventStore.Domain.Commands;
using Hexalith.Memories.EventStore.Routing;

[assembly: RouteCatalogAdapterSource(typeof(Hexalith.Memories.EventStoreExtensions.MemoriesCatalogAdapters))]

namespace Hexalith.Memories.EventStoreExtensions;

/// <summary>Typed local policy adapters owned by the existing Memories command contracts.</summary>
public static class MemoriesCatalogAdapters
{
    /// <summary>Creates the exact six internal command adapters without enabling hosted admission.</summary>
    public static IReadOnlyList<IDeclaredIdempotencyIntentAdapter> Create() => [
        Adapter<RegisterTenantCommand>(),
        Adapter<UpdateTenantLifecycleStatusCommand>(),
        Adapter<CreateCaseCommand>(),
        Adapter<DeleteCaseCommand>(),
        Adapter<RequestAnnotationCommand>(),
        Adapter<DeleteMemoryUnitCommand>()];

    private static DeclaredIdempotencyIntentAdapter<T> Adapter<T>() => new(
        MemoriesCatalogDeclarations.Commands.Single(c => c.Route.Execution!.HandlerType == typeof(T).FullName),
        payload => new Dictionary<string, object?> { ["payload-sha256"] = CanonicalIntentPayload.Sha256(payload) });
}

