using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Current authenticated independent observer read. The preserved inner signature also authenticates immutable archived stage originals.</summary>
public sealed class DaprQualificationObservedOriginalReader(IndependentDaprAuthority observer):IQualificationObservedOriginalReader
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> PreflightStagesAsync(JsonObject original,CancellationToken token)
        =>await observer.InvokeAsync<string[]>("qualification-observer-source-preflight",original,token).ConfigureAwait(false)??["InstalledIndependentObserverStagePreflightRoleGrantAndSourceReader"];

    /// <inheritdoc />
    public Task<QualificationObservedOriginal?> LookupStageAsync(JsonObject original,QualificationObservationStageRequest stage,CancellationToken cancellationToken)
        =>observer.InvokeAsync<QualificationObservedOriginal>("qualification-observer-lookup-source",new { original,observationContext=stage },cancellationToken);
    /// <inheritdoc />
    public Task<QualificationObservedOriginal?> ReadStageAsync(JsonObject original,QualificationObservationStageRequest stage,CancellationToken cancellationToken)
        =>observer.InvokeAsync<QualificationObservedOriginal>("qualification-observer-read-source",new { original,observationContext=stage },cancellationToken);
    /// <inheritdoc />
    public Task<QualificationObservedOriginal?> ReadAsync(JsonObject original,CancellationToken cancellationToken)
        =>observer.InvokeAsync<QualificationObservedOriginal>("qualification-observer-read-source",original,cancellationToken);
}
