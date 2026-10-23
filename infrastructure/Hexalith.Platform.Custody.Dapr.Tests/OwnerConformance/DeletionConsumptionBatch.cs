using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Security;

/// <summary>Private immutable batch/target identity plus current attestation and durable post-dispatch outcome.</summary>
/// <param name="Original">Original immutable batch identity.</param>
/// <param name="Current">Sole current exact attestation.</param>
/// <param name="Outcome">Original durable state receipt/vector.</param>
internal sealed record DeletionConsumptionBatch(DeletionBatchConsumptionRequest Original, DeletionBatchConsumptionRequest Current, DeletionConsumptionOutcome Outcome)
{
    public DeletionBlockedReplacementReconciliation? BlockedReplacement { get; init; }
}
