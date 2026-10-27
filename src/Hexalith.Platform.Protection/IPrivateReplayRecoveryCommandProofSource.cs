using Hexalith.EventStore.Contracts.Commands;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Distinct replay terminal-recovery signing capability with no new first-seen or general dispatcher authority.</summary>
public interface IPrivateReplayRecoveryCommandProofSource
{
    /// <summary>Authenticates the same immutable replay body and independently proven terminal transport before signing its deterministic successor.</summary>
    ValueTask<string?> IssueReplayRecoveryAsync(BoundWorkloadCommandRequest request,PrivateReplayRecoveryProof proof,CancellationToken cancellationToken);
}
