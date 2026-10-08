namespace Hexalith.Platform.Custody;

/// <summary>Independent verification-only policy/issuer/role boundary, absent by default. There is deliberately no signing or approval-request operation.</summary>
public interface IIndependentDecisionAuthority
{
    /// <summary>Resolves an independently authenticated approved public verification profile for the exact contract/version and original issuer key.</summary>
    Task<DecisionAuthorityPublishedProfile?> ResolveAsync(DecisionAuthorityExpectedBasis expected, string signingKeyVersion, CancellationToken cancellationToken = default);
    /// <summary>Checks complete required roles/evaluations and exact effective predecessor or independently authorized root against the actual independent policy.</summary>
    Task<bool> VerifyExpectedBasisAsync(DecisionAuthorityExpectedBasis expected, DecisionAuthorityPublishedProfile profile, CancellationToken cancellationToken = default);
    /// <summary>Authenticates the exact historic stable actor/role/source/binding/evidence at original issuance and current applicable revocation.</summary>
    Task<bool> VerifyApproverAsync(DecisionAuthorityExpectedBasis expected, DecisionAuthorityApprover approver, DateTimeOffset issuedAt, CancellationToken cancellationToken = default);
}
