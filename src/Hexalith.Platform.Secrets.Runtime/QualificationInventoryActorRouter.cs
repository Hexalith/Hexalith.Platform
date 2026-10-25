using Dapr.Actors;
using Dapr.Actors.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Routes inventory calls to the existing published deterministic actor address; no caller-selected actor identity is accepted.</summary>
public sealed class QualificationInventoryActorRouter:IPlatformKeyInventoryActor
{
    private static IPlatformKeyInventoryActor Actor(PlatformKeyVersion key)=>ActorProxy.Create<IPlatformKeyInventoryActor>(new ActorId(PlatformKeyInventoryActor.GetActorId(key)),PlatformKeyInventoryActor.ActorTypeName);
    /// <inheritdoc />
    public Task<PlatformKeyInventorySnapshot> ReadAsync(PlatformKeyVersion scope)=>Actor(scope).ReadAsync(scope);
    /// <inheritdoc />
    public Task<PlatformKeyInventorySignal?> ApplyAsync(PlatformKeyInventoryChange change)=>Actor(change.Key).ApplyAsync(change);
}
