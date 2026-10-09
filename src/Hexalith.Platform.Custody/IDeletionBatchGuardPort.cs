using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Custody;

/// <summary>Private typed guard adapter; none of these methods grants authority independently of the owner's conditional transition.</summary>
public interface IDeletionBatchGuardPort
{
    /// <summary>Reads exact installed immutable scope/seal/manifest and current sole attestation.</summary>
    Task<DeletionBatchGuardSnapshot?> ReadAsync(DeletionBatchCapabilityV1 payload, CancellationToken cancellationToken = default);
    /// <summary>Commits exact signature at its intended revision, or returns persisted original stale/no-effect result.</summary>
    Task<GovernanceProtocolReceipt?> IssueAsync(DeletionCapabilitySigningOutcome signed, bool replacement, CancellationToken cancellationToken = default);
    /// <summary>Authorizes then conditionally commits exact active-attestation dispatch; an intervening guard mutation returns original stale.</summary>
    Task<GovernanceProtocolReceipt?> DispatchAsync(DeletionCapabilitySigningOutcome signed, CancellationToken cancellationToken = default);
    /// <summary>Mirrors the independently read original protection result, including exact ordered targets and conditional activation status.</summary>
    Task<GovernanceProtocolReceipt?> RecordProtectionAsync(DeletionCapabilitySigningOutcome signed, DeletionConsumptionOutcome outcome, bool activation, CancellationToken cancellationToken = default);
    /// <summary>Mirrors only the independently proved original issued-but-blocked disposition; omission grants no dispatch or effect.</summary>
    Task<GovernanceProtocolReceipt?> RecordBlockedReplacementAsync(DeletionCapabilitySigningOutcome signed, DeletionBlockedReplacementResult retained, CancellationToken cancellationToken = default)
        => Task.FromResult<GovernanceProtocolReceipt?>(null);
    /// <summary>Authorizes then conditionally commits completion only with all exact required outcomes and current dispositions.</summary>
    Task<GovernanceProtocolReceipt?> CompleteAsync(DeletionBatchCapabilityV1 payload, CancellationToken cancellationToken = default);
}
