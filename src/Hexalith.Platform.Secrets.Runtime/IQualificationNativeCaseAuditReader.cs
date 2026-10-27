using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Typed independent native history reader; unavailable selected protocols return no data rather than synthesized positions.</summary>
public interface IQualificationNativeCaseAuditReader
{
    /// <summary>Names actual unselected native stage/surface/incarnation/control protocols before any case effect.</summary>
    Task<IReadOnlyList<string>> PreflightStagesAsync(JsonObject original,CancellationToken cancellationToken)
        =>Task.FromResult<IReadOnlyList<string>>(["External.IndependentCompleteNativeStageSurfaceControlAndIncarnationAuditProtocol."+GetType().Name]);
    /// <summary>Reads the complete native audit and actual leakage scan results for this exact original.</summary>
    Task<QualificationNativeCaseAudit?> ReadAsync(JsonObject original,CancellationToken cancellationToken);
}
