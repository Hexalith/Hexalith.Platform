using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Registers the concrete measured projector and closed source workflows in the owning receiving path.</summary>
public static class QualificationDriverComposition
{
    /// <summary>Builds the same executable path for installed source operations and isolated fixture verification.</summary>
    public static IReadOnlyDictionary<string,IQualificationOwnerCaseDriver> Create(IReadOnlyList<QualificationSourceWorkflow> workflows,
        IQualificationOriginalStore executorOriginals,IQualificationRecoveryController recovery,IQualificationObservedOriginalReader independentReader,
        QualificationProofBinding observerTrust,IReadOnlyDictionary<string,IReadOnlyList<string>> missingReaders,TimeProvider? clock=null)
    {
        if(workflows.Select(value=>value.CaseId).Distinct(StringComparer.Ordinal).Count()!=workflows.Count || workflows.Any(value=>!QualificationProtocolSchema.CaseIds.Contains(value.CaseId,StringComparer.Ordinal)))
            throw new ArgumentException("Exact shipped case workflow identities required.",nameof(workflows));
        IQualificationMeasuredCaseProjector projector=new QualificationMeasuredCaseProjector(observerTrust,recovery,missingReaders,clock);
        var observation=new QualificationObservationOrchestrator(executorOriginals,recovery,independentReader);
        return workflows.ToDictionary(value=>value.CaseId,value=>(IQualificationOwnerCaseDriver)new QualificationBoundSourceCaseDriver(value.CaseId,
            async (original,token)=>(await value.Preflight(original,token).ConfigureAwait(false)).Concat(await recovery.PreflightAsync(original,token).ConfigureAwait(false)).Distinct(StringComparer.Ordinal).ToArray(),
            value.Execute,value.Lookup,observation,projector,executorOriginals),StringComparer.Ordinal);
    }
}
