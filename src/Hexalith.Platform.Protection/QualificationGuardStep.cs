using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>One independently enrolled typed technical step; unrelated union branches are refused.</summary>
public sealed record QualificationGuardStep(QualificationGuardStepKind Kind, GovernanceGuardTransition? Transition = null,
    IReadOnlyList<GuardedStateMutation>? Targets = null, DeletionBatchConsumptionRequest? Batch = null,
    DeletionBatchBlockRequest? Block = null, DeletionCapabilityRevocationEnvelope? Compromise = null,
    DeletionReattestationActivation? Activation = null, DeletionBatchCapabilityV1? Signing = null, string? SignedRevocationEvidence = null);
