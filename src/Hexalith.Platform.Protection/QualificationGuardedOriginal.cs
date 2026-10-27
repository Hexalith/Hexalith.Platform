using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Protection;

/// <summary>Actual independently authenticated physical guarded commit and its decoded logical original. Physical generation and logical high water remain distinct.</summary>
public sealed record QualificationGuardedOriginal(GovernanceGuardTransition Transition,string TargetMutationDigest,GuardedStateCommitReceipt Receipt,GovernanceProtocolReceipt Outcome);
