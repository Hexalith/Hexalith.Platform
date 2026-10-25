using System.Text.Json.Nodes;

namespace Hexalith.Platform.Protection;

/// <summary>Narrow independent native-source publication. The phase owner holds only a current remote credential and public verification trust.</summary>
public interface IJointPhaseNativeSourcePublisher
{
    /// <summary>Checks actual independent read/current scopes before the phase may admit any native effect.</summary>
    Task<IReadOnlyList<string>> PreflightAsync(string caseId,JsonObject phaseRequest,CancellationToken cancellationToken)
        =>Task.FromResult<IReadOnlyList<string>>(["IndependentNativeSourceReadinessProtocol"]);
    /// <summary>Reads/publishes the exact original source evidence under its independently enrolled signing/read authority.</summary>
    Task<JsonObject?> PublishAsync(string caseId,JsonObject phaseRequest,CancellationToken cancellationToken);
}
