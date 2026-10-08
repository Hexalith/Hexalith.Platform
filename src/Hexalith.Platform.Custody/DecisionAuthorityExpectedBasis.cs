namespace Hexalith.Platform.Custody;

/// <summary>Exact caller's expected decision basis; it must separately pass independent current policy verification, never define its own required approvers.</summary>
/// <param name="DecisionId">Exact decision.</param><param name="ContractId">Exact contract.</param><param name="ContractVersion">Required version.</param><param name="OutcomeVersion">Required outcome.</param>
/// <param name="EffectivePredecessorVersion">Required independently effective predecessor, or null for root.</param><param name="RootPolicyId">Required independently authorized root.</param>
/// <param name="AffectedEvaluations">Complete policy-required evaluation set.</param><param name="RequiredRoles">Sorted distinct nonempty independently required roles.</param>
public sealed record DecisionAuthorityExpectedBasis(string DecisionId, string ContractId, long ContractVersion, long OutcomeVersion, long? EffectivePredecessorVersion,
    string? RootPolicyId, IReadOnlyList<string> AffectedEvaluations, IReadOnlyList<string> RequiredRoles);
