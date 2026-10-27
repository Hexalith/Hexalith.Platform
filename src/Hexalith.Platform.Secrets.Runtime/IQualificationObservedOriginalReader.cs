using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Typed actual persisted owner reader. No scenario expected values or stage labels are inputs.</summary>
public interface IQualificationObservedOriginalReader
{
    /// <summary>Refuses before case effects unless the selected independent adapter supports measured stage, control and incarnation facts.</summary>
    Task<IReadOnlyList<string>> PreflightStagesAsync(JsonObject original,CancellationToken cancellationToken)
        =>Task.FromResult<IReadOnlyList<string>>(["External.IndependentNativeStageControlIncarnationReader."+GetType().Name]);
    /// <summary>Reads the exact retained source original under fresh current authority.</summary>
    /// <summary>Reads only an already retained signed inner stage; omitted lookups never authorize another measurement.</summary>
    Task<QualificationObservedOriginal?> LookupStageAsync(JsonObject original,QualificationObservationStageRequest stage,CancellationToken cancellationToken)
        =>Task.FromResult<QualificationObservedOriginal?>(null);
    /// <summary>Measures one new stage only when its exact independent original has not already been admitted.</summary>
    Task<QualificationObservedOriginal?> ReadStageAsync(JsonObject original,QualificationObservationStageRequest stage,CancellationToken cancellationToken)
        =>Task.FromException<QualificationObservedOriginal?>(new InvalidOperationException("InstalledIndependentConditionalStageOriginalAdmission"));
    /// <summary>Reads an original without claiming complete stage-bound qualification.</summary>
    Task<QualificationObservedOriginal?> ReadAsync(JsonObject original,CancellationToken cancellationToken);
}
