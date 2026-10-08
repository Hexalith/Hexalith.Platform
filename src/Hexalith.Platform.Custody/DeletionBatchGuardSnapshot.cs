using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Custody;

/// <summary>Exact private installed current source facts, independently authenticated by the EventStore owner.</summary>
/// <param name="State">Installed tenant guard.</param><param name="Deletion">Exact original request/predicate/seal.</param><param name="Batch">Exact immutable complete batch and sole active artifact.</param>
public sealed record DeletionBatchGuardSnapshot(TenantGovernanceGuardState State, GovernanceDeletionState Deletion, GovernanceBatchState Batch);
