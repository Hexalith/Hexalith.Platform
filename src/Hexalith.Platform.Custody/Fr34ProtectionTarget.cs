namespace Hexalith.Platform.Custody;

/// <summary>Exact independently committed production protection engine and custody target; no default or inferred version.</summary>
/// <param name="EngineId">Committed engine identity.</param><param name="EngineVersion">Committed immutable engine version.</param>
/// <param name="CustodyTargetId">Exact enrolled custody target.</param><param name="TenantId">Dedicated canary tenant.</param>
public sealed record Fr34ProtectionTarget(string EngineId, string EngineVersion, string CustodyTargetId, string TenantId);
