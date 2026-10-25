using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Separately installed exact process/restore controller. No default or self-reported implementation exists.</summary>
public interface IQualificationRecoveryController
{
    /// <summary>Reports exact missing protocol/resource inputs before any case effect or recovery operation.</summary>
    Task<IReadOnlyList<string>> PreflightAsync(JsonObject original,CancellationToken cancellationToken);
    /// <summary>Performs one independently authenticated isolated restart or restore, retaining exact identity across uncertainty.</summary>
    Task<QualificationRecoveryControlOriginal?> ExecuteAsync(string operation,JsonObject original,CancellationToken cancellationToken);
    /// <summary>Reads only the exact control original after loss; never repeats an unknown control.</summary>
    Task<QualificationRecoveryControlOriginal?> LookupAsync(string operation,JsonObject original,CancellationToken cancellationToken);
    /// <summary>Verifies externally enrolled retained public trust and actual process/storage incarnation evidence.</summary>
    bool Verify(QualificationRecoveryControlOriginal original,JsonObject request);
}
