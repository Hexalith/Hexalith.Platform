namespace Hexalith.Platform.Custody;

/// <summary>Private candidate non-PII actor/role evidence; only independent role authority can validate the binding.</summary>
/// <param name="Role">Exact Product, Governance, Security, Architecture or named maintainer role required by policy.</param><param name="ActorId">Stable independent actor.</param>
/// <param name="AuthoritySourceId">Independent role-authority source.</param><param name="BindingVersion">Exact retained actor-role binding.</param><param name="EvidenceId">Original independently verifiable decision evidence.</param>
public sealed record DecisionAuthorityApprover(string Role, string ActorId, string AuthoritySourceId, string BindingVersion, string EvidenceId);
