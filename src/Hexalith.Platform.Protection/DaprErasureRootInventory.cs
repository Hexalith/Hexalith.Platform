using Dapr.Actors.Client;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Protection;

/// <summary>Tenant/purpose-scoped proxy routing to the existing independently authorized inventory actors.</summary>
public sealed class DaprErasureRootInventory(IActorProxyFactory proxies) : IPlatformKeyInventoryActor
{
    private IPlatformKeyInventoryActor For(PlatformKeyVersion key)
        => proxies.CreateActorProxy<IPlatformKeyInventoryActor>(new(PlatformKeyInventoryActor.GetActorId(key)), PlatformKeyInventoryActor.ActorTypeName);
    /// <inheritdoc />
    public Task<PlatformKeyInventorySignal?> ApplyAsync(PlatformKeyInventoryChange change) => For(change.Key).ApplyAsync(change);
    /// <inheritdoc />
    public Task<PlatformKeyInventorySnapshot> ReadAsync(PlatformKeyVersion scope) => For(scope).ReadAsync(scope);
}
