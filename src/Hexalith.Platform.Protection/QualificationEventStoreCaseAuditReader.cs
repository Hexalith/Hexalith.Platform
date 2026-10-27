using System.Text.Json.Nodes;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Concrete available EventStore audit composition. Missing recovery history and complete log/trace/copy readers are named and refuse full observation, never replaced by empty scans.</summary>
public sealed class QualificationEventStoreCaseAuditReader(QualificationEventStoreAuditReader? native,IQualificationNativeCaseAuditReader? completeIndependentSupplement=null):IQualificationNativeCaseAuditReader
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> PreflightStagesAsync(JsonObject original,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(native is null) return ["InstalledPrivateEventStoreEndpointAndCaseReadScopes"];
        var scope=native.PreflightScope(original);if(scope.Count!=0) return scope;
        var mutationScope=native.PreflightMutationScope(original);if(mutationScope.Count!=0) return mutationScope;
        return completeIndependentSupplement is null
            ?["External.IndependentCompleteSurfaceSnapshotStageAndNativeControlCorrelationProtocol","External.IndependentProcessAndStorageIncarnationHistoryProtocol"]
            :await completeIndependentSupplement.PreflightStagesAsync(original,token).ConfigureAwait(false);
    }
    /// <summary>Gets missing installed protocols from the latest read.</summary>
    public IReadOnlyList<string> MissingInputs { get; private set; }=[];
    /// <inheritdoc />
    public async Task<QualificationNativeCaseAudit?> ReadAsync(JsonObject original,CancellationToken token)
    {
        if(native is null) { MissingInputs=["External.ExactPrivateEventStoreEndpointWorkloadReadGrantAndEnrolledCaseScopes"];return null; }
        var observed=await native.ReadAsync(original,token).ConfigureAwait(false);if(observed is null) { MissingInputs=["External.CurrentAuthoritativeEventStoreOriginalStatusStreamAndProjectionReads"];return null; }
        var supplement=completeIndependentSupplement is null?null:await completeIndependentSupplement.ReadAsync(original,token).ConfigureAwait(false);
        if(supplement is null) { MissingInputs=observed.MissingInputs;return null; }
        if(supplement.Facts?["nativeSourceSnapshot"] is not JsonObject expected || !JsonNode.DeepEquals(expected,System.Text.Json.JsonSerializer.SerializeToNode(observed with { MutationEvidence=null },CustodyBindingContract.Json)))
        { MissingInputs=["ExactIndependentNativeAuditSourceCorrelation"];return null; }
        if(observed.MutationEvidence is { } actualMutations)
        {
            if(supplement.TargetMutations.Count!=actualMutations.Count || supplement.TargetMutations.Any(value=>value is not JsonObject))
            { MissingInputs=["CompleteExactNativeEventMutationCensusV2"];return null; }
            var mutations=(JsonArray)supplement.TargetMutations.DeepClone();var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(var row in mutations.Cast<JsonObject>())
            {
                string? id=row["nativeMutationId"]?.GetValue<string>();var actual=actualMutations.SingleOrDefault(value=>value.MutationId==id);
                if(actual is null || !seen.Add(id!) || row["targetTenantId"]?.GetValue<string>()!=actual.Source.TenantId
                    || row["targetDomain"]?.GetValue<string>()!=actual.Source.Domain || row["targetObjectId"]?.GetValue<string>()!=actual.Source.AggregateId || row["targetId"]?.GetValue<string>()!=actual.TargetId)
                { MissingInputs=["ExactNativeMutationTargetAndAuthenticatedDispatchOriginalV2"];return null; }
                row["authorizationReceiptId"]=actual.AuthorizationId;row["authorizationEvidence"]=System.Text.Json.JsonSerializer.SerializeToNode(actual,CustodyBindingContract.Json);
            }
            var facts=supplement.Facts is null?new JsonObject():(JsonObject)supplement.Facts.DeepClone();facts["nativeMutationSources"]=System.Text.Json.JsonSerializer.SerializeToNode(observed.NativeStreams,CustodyBindingContract.Json);
            supplement=supplement with { TargetMutations=mutations,Facts=facts };
        }
        else if(supplement.TargetMutations.Count!=0) { MissingInputs=["LegacyNativeMutationCensusMissingAuthenticatedDispatchOriginalAndExactTargetV2"];return null; }
        MissingInputs=[];return supplement;
    }
}
