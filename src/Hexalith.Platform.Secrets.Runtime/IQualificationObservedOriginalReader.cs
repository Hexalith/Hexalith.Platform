using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Typed actual persisted owner reader. No scenario expected values or stage labels are inputs.</summary>
public interface IQualificationObservedOriginalReader
{
    /// <summary>Reads the exact retained source original under fresh current authority.</summary>
    Task<QualificationObservedOriginal?> ReadAsync(JsonObject original,CancellationToken cancellationToken);
}
