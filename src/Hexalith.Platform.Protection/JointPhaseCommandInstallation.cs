using System.Text.Json.Nodes;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>CaseOriginals and SecretsOriginals contain independently approved request recipes without runId/requestId; execution binds the already retained attempt. Explicit independently pinned command source/target/key/resource inputs. Missing groups remain unexecutable; no path or predicate is supplied by stdin.</summary>
public sealed record JointPhaseCommandInstallation(JointPhaseCommandScope Scope,string RuntimeConfigurationFile,QualificationProtectionInstallation? Protection,
    IndependentDaprAuthorityBinding ExecutorOriginals,Dictionary<string,IReadOnlyList<JsonObject>> CaseOriginals,
    string EvidenceDirectory,string OwnerSigningKeyFile,string OwnerPublicKeyFile,IndependentDaprAuthorityBinding? IndependentSource,
    Dictionary<string,QualificationInstallation>? SecretsRoles=null,QualificationCallerContext? SecretsCaller=null,Dictionary<string,JsonObject>? SecretsOriginals=null,
    IndependentDaprAuthorityBinding? JointSecretsOriginals=null,IndependentDaprAuthorityBinding? JointCommandOriginals=null,QualificationCohortAttemptBinding? CohortAttempt=null);
