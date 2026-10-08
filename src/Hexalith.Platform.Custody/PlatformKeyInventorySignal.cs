namespace Hexalith.Platform.Custody;

/// <summary>Safe durable rotation/revocation invalidation signal; publication qualification remains external.</summary>
/// <param name="OperationId">Original complete change identity.</param>
/// <param name="RequestDigest">Exact input fingerprint.</param>
/// <param name="InventoryRevision">Durable original revision.</param>
/// <param name="Action">Rotation/current installation or emergency revocation.</param>
/// <param name="Key">Exact opaque purpose/version metadata.</param>
public sealed record PlatformKeyInventorySignal(string OperationId, string RequestDigest, long InventoryRevision, PlatformKeyInventoryAction Action, PlatformKeyVersion Key);
