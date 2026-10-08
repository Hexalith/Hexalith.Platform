namespace Hexalith.Platform.Custody;

/// <summary>Actual tenant technical wrapped-key lifecycle metadata, separately bound to independent exact-state restore anchor.</summary>
/// <param name="TenantId">Exact tenant.</param><param name="Revision">Durable lifecycle revision.</param><param name="Keys">All original phase-pinned objects/operations/holds.</param>
public sealed record CustodyKeyLifecycleLedger(string TenantId, long Revision, IReadOnlyList<CustodyKeyLifecycleEntry> Keys);
