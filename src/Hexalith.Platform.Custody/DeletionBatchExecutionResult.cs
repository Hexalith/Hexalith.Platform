using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Custody;

/// <summary>Content-free exact protocol outcome; source completion and physical consumption are reported separately.</summary>
/// <param name="Status">Exact phase or unresolved result.</param><param name="Signing">Original retained public signer result.</param>
/// <param name="Protection">Original authenticated protection-owner result if known.</param><param name="Guard">Original conditional guard result if known.</param>
/// <param name="NextAttempt">Only an independently no-issue-proven stable successor; it grants no signing or issue authority.</param>
public sealed record DeletionBatchExecutionResult(string Status, DeletionCapabilitySigningOutcome? Signing = null, DeletionConsumptionOutcome? Protection = null,
    GovernanceProtocolReceipt? Guard = null, DeletionBatchCapabilityV1? NextAttempt = null);
