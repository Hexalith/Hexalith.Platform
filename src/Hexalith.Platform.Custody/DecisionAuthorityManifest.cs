namespace Hexalith.Platform.Custody;

/// <summary>Private candidate closed independently issued approval manifest; no installed public wire/profile or signing/request operation is supplied.</summary>
/// <param name="Issuer">Independent approved issuer.</param><param name="Audience">Exact consuming verification audience.</param><param name="DecisionId">Stable decision.</param>
/// <param name="ContractId">Exact approved contract.</param><param name="ContractVersion">Exact contract version.</param><param name="OutcomeVersion">Original approved outcome.</param>
/// <param name="EffectivePredecessorVersion">Exact effective predecessor, or null for root.</param><param name="RootPolicyId">Exact independent root policy only when no predecessor.</param>
/// <param name="AffectedEvaluations">Sorted distinct complete evaluation set.</param><param name="Approvers">Sorted distinct nonempty required actor-role evidence.</param>
/// <param name="IssuedAt">Original UTC issuer observation.</param><param name="ExclusiveExpiry">Independent policy-bounded exclusive expiry.</param><param name="SigningKeyVersion">Exact original independently governed verification version.</param>
public sealed record DecisionAuthorityManifest(string Issuer, string Audience, string DecisionId, string ContractId, long ContractVersion, long OutcomeVersion,
    long? EffectivePredecessorVersion, string? RootPolicyId, IReadOnlyList<string> AffectedEvaluations, IReadOnlyList<DecisionAuthorityApprover> Approvers,
    DateTimeOffset IssuedAt, DateTimeOffset ExclusiveExpiry, string SigningKeyVersion);
