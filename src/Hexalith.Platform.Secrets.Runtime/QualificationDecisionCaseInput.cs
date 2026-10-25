using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Independently issued original approval and actual expected basis. Platform only verifies this artifact; it cannot request or sign an approval.</summary>
public sealed record QualificationDecisionCaseInput(string TenantId,DecisionAuthorityManifest OriginalManifest,string DetachedJws,DecisionAuthorityExpectedBasis ExpectedBasis,
    DecisionAuthorityPublishedProfile? OriginalForeignAnchor=null);
