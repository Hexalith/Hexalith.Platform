using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Protection;

/// <summary>Exact receiving-owner attempt retained before dispatch, allowing authenticated transaction-original recovery after result loss.</summary>
internal sealed record ErasureRootGuardAdmission(GovernanceGuardTransition Transition, GuardedStateMutation[] Targets, long BeforeRevision);
