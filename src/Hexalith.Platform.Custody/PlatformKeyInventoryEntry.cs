namespace Hexalith.Platform.Custody;

/// <summary>Current/retained/revoked opaque version. Old public verifier records are never deleted by rotation/revocation.</summary>
/// <param name="Key">Original immutable metadata.</param>
/// <param name="State">Current Active, healthy Retained or irreversible Revoked.</param>
/// <param name="ChangedAtRevision">Original durable state transition revision.</param>
public sealed record PlatformKeyInventoryEntry(PlatformKeyVersion Key, PlatformHmacKeyState State, long ChangedAtRevision);
