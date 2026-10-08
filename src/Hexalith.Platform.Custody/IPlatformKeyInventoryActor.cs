using Dapr.Actors;

namespace Hexalith.Platform.Custody;

/// <summary>Private technical purpose/alias version owner; no key values or general signing capability.</summary>
public interface IPlatformKeyInventoryActor : IActor
{
    /// <summary>Applies exact authenticated conditional current/retained/revoked change or returns null without mutation.</summary>
    Task<PlatformKeyInventorySignal?> ApplyAsync(PlatformKeyInventoryChange change);
    /// <summary>Reads exact opaque version inventory and original signals.</summary>
    Task<PlatformKeyInventorySnapshot> ReadAsync(PlatformKeyVersion scope);
}
