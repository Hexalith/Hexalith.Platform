namespace Hexalith.Platform.Custody;

/// <summary>Durable version/purpose state and append-only safe signals; configured metadata does not establish physical key custody.</summary>
/// <param name="TenantId">Exact tenant.</param>
/// <param name="Purpose">Exact purpose.</param>
/// <param name="KeyAlias">Exact key alias.</param>
/// <param name="Revision">Durable compare revision.</param>
/// <param name="Versions">All original current/retained/revoked versions.</param>
/// <param name="Signals">Exact append-only rotation/revocation outcomes.</param>
public sealed record PlatformKeyInventorySnapshot(string TenantId, PlatformKeyPurpose Purpose, string KeyAlias, long Revision, IReadOnlyList<PlatformKeyInventoryEntry> Versions, IReadOnlyList<PlatformKeyInventorySignal> Signals);
