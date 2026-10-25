using System.Text.Json.Nodes;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Concrete available EventStore audit composition. Missing recovery history and complete log/trace/copy readers are named and refuse full observation, never replaced by empty scans.</summary>
public sealed class QualificationEventStoreCaseAuditReader(QualificationEventStoreAuditReader? native,IQualificationNativeCaseAuditReader? completeIndependentSupplement=null):IQualificationNativeCaseAuditReader
{
    /// <summary>Gets missing installed protocols from the latest read.</summary>
    public IReadOnlyList<string> MissingInputs { get; private set; }=[];
    /// <inheritdoc />
    public async Task<QualificationNativeCaseAudit?> ReadAsync(JsonObject original,CancellationToken token)
    {
        if(native is null) { MissingInputs=["External.ExactPrivateEventStoreEndpointWorkloadReadGrantAndEnrolledCaseScopes"];return null; }
        var observed=await native.ReadAsync(original,token).ConfigureAwait(false);if(observed is null) { MissingInputs=["External.CurrentAuthoritativeEventStoreOriginalStatusStreamAndProjectionReads"];return null; }
        var supplement=completeIndependentSupplement is null?null:await completeIndependentSupplement.ReadAsync(original,token).ConfigureAwait(false);
        if(supplement is null) { MissingInputs=observed.MissingInputs;return null; }
        if(supplement.Facts?["nativeSourceSnapshot"] is not JsonObject expected || !JsonNode.DeepEquals(expected,System.Text.Json.JsonSerializer.SerializeToNode(observed,CustodyBindingContract.Json)))
        { MissingInputs=["ExactIndependentNativeAuditSourceCorrelation"];return null; }
        MissingInputs=[];return supplement;
    }
}
