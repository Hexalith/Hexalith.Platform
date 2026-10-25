using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Current authenticated independent observer read. The preserved inner signature also authenticates immutable archived stage originals.</summary>
public sealed class DaprQualificationObservedOriginalReader(IndependentDaprAuthority observer):IQualificationObservedOriginalReader
{
    /// <inheritdoc />
    public Task<QualificationObservedOriginal?> ReadAsync(JsonObject original,CancellationToken cancellationToken)
        =>observer.InvokeAsync<QualificationObservedOriginal>("qualification-observer-read-source",original,cancellationToken);
}
