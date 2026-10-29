using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Protection;

/// <summary>Independent receiving-owner original for a real guard attempt, including a refused compare that does not append a committed guard receipt.</summary>
/// <param name="Transition">Exact actual command.</param><param name="TargetMutationDigest">Exact actual technical mutation digest.</param>
/// <param name="BeforeRevision">Native guard revision read before dispatch.</param><param name="AfterRevision">Native guard revision read after the actual result.</param>
/// <param name="Receipt">Actual guard result, without a synthetic successful dispatch.</param>
public sealed record ErasureRootGuardAttempt(GovernanceGuardTransition Transition, string TargetMutationDigest, long BeforeRevision, long AfterRevision,
    GovernanceProtocolReceipt Receipt);
