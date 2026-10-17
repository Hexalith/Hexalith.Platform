namespace Hexalith.Platform.Secrets.Inventory;

/// <summary>Current native stream identity and revision; an inactive tenant grants no inventory access.</summary>
/// <param name="TenantId">Exact persisted tenant identity.</param>
/// <param name="Revision">Digest of the complete current native stream.</param>
/// <param name="Active">Whether the native tenant state remains active.</param>
public sealed record NativeEnrollment(string TenantId, string Revision, bool Active);
