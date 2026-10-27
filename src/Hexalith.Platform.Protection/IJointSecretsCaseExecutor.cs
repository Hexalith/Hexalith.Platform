using System.Text.Json.Nodes;

namespace Hexalith.Platform.Protection;

/// <summary>Distinct authenticated secrets phase protocol; no native-source evidence shape substitutes its three signed roles.</summary>
public interface IJointSecretsCaseExecutor
{
    /// <summary>Requires independently current complete role/driver/enrollment readiness before phase admission.</summary>
    Task<IReadOnlyList<string>> PreflightAsync(string caseId,JsonObject phaseRequest,CancellationToken cancellationToken)
        =>Task.FromResult<IReadOnlyList<string>>(["CompleteAuthenticatedSecretsRoleAndDriverReadiness"]);
    /// <summary>Executes or looks up one retained exact case and returns request/execution/observation/enrollment bound to this cohort request.</summary>
    Task<JsonObject?> ExecuteAsync(string caseId,JsonObject phaseRequest,CancellationToken cancellationToken);
}
