using System.Text.Json.Nodes;
using System.Text.Json;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Reads the exact retained temporal evaluation and its independently persisted native receipt. A later clock reading cannot recreate a historical boundary evaluation.</summary>
public sealed class QualificationTemporalOriginalReader(IQualificationOriginalStore originals,IQualificationNativeCaseAuditReader audit):IQualificationObservedOriginalReader
{
    /// <inheritdoc />
    public async Task<QualificationObservedOriginal?> ReadAsync(JsonObject request,CancellationToken token)
    {
        string? requestId=SecretsProof.Text(request,"requestId");if(requestId is null) return null;
        var retained=await originals.LookupResultAsync("executor",requestId+":case-result",token).ConfigureAwait(false);
        var native=await audit.ReadAsync(request,token).ConfigureAwait(false);
        if(retained is null || native?.Facts?["temporalOriginal"] is not JsonObject independentlyPersisted || !JsonNode.DeepEquals(independentlyPersisted,retained)
            || retained["outcome"]?.GetValue<string>() is not ("Verified" or "Denied" or "Expired")) return null;
        string receipt=QualificationProtocolSchema.Digest(retained);var state=QualificationDeniedCaseState.Create(request,native.OriginalRevision,receipt);
        state["outcome"]=retained["outcome"]!.DeepClone();state["status"]=retained["outcome"]!.DeepClone();
        state["timing"]!["checkedAt"]=retained["checkedAt"]!.DeepClone();
        if(request["caseId"]!.GetValue<string>().StartsWith("S2.expiry.",StringComparison.Ordinal))
        {
            long expires=retained["originalEnvelope"]!["ExpiresAt"]!.Deserialize<DateTimeOffset>().ToUnixTimeMilliseconds();
            state["exclusiveExpiry"]=expires;state["timing"]!["exclusiveExpiry"]=expires;
        }
        if(retained["replacementEnvelope"] is JsonObject replacement) state["keyVersion"]=replacement["SigningKeyVersion"]!.DeepClone();
        if(request["caseId"]!.GetValue<string>().StartsWith("S2.overlap-",StringComparison.Ordinal))
        {
            if(native.Facts["rotationOriginal"] is not JsonObject rotation) return null;
            state["rotation"]=rotation.DeepClone();state["keyVersion"]=retained["originalEnvelope"]!["SigningKeyVersion"]!.DeepClone();
            state["timing"]!["rotatedAt"]=rotation["rotatedAt"]!.DeepClone();state["timing"]!["overlapEndsAt"]=rotation["rotatedAt"]!.GetValue<long>()+retained["rotationOverlapMilliseconds"]!.GetValue<long>();
        }
        return new(requestId,receipt,"temporal:"+native.OriginalRevision,new JsonObject { ["state"]=state,["receipts"]=native.Receipts.DeepClone(),["targetMutations"]=native.TargetMutations.DeepClone(),
            ["surfaceScans"]=native.SurfaceScans.DeepClone(),["copyCensus"]=native.CopyCensus.DeepClone(),["linearization"]=new JsonObject(),["independentTemporalVerification"]=independentlyPersisted.DeepClone() });
    }
}
