using System.Text.Json.Nodes;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Actual independently authenticated source-receiving client. It never handles the native source private key or source-read credentials.</summary>
public sealed class DaprJointPhaseNativeSourcePublisher(IndependentDaprAuthority independentSource):IJointPhaseNativeSourcePublisher
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> PreflightAsync(string caseId,JsonObject phaseRequest,CancellationToken token)
    {
        var result=await independentSource.InvokeAsync<Readiness>("joint-phase-native-source-preflight",new { caseId,phaseRequest },token).ConfigureAwait(false);
        return result?.MissingInputs??["IndependentNativeSourceReadinessUnavailable"];
    }
    private sealed record Readiness(IReadOnlyList<string> MissingInputs);
    /// <inheritdoc />
    public Task<JsonObject?> PublishAsync(string caseId,JsonObject phaseRequest,CancellationToken token)
        =>independentSource.InvokeAsync<JsonObject>("joint-phase-publish-native-source",new { caseId,phaseRequest },token);
}
