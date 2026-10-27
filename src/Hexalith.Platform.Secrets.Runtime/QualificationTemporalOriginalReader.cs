using System.Text.Json.Nodes;
using System.Text.Json;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Reads the exact retained temporal evaluation and its independently persisted native receipt. A later clock reading cannot recreate a historical boundary evaluation.</summary>
public sealed class QualificationTemporalOriginalReader(IQualificationOriginalStore originals,IQualificationNativeCaseAuditReader audit):IQualificationObservedOriginalReader
{
    /// <inheritdoc />
    public Task<IReadOnlyList<string>> PreflightStagesAsync(JsonObject original,CancellationToken token)=>audit.PreflightStagesAsync(original,token);
    /// <inheritdoc />
    public async Task<QualificationObservedOriginal?> ReadAsync(JsonObject request,CancellationToken token)
    {
        string? requestId=SecretsProof.Text(request,"requestId");if(requestId is null) return null;
        var retained=await originals.LookupResultAsync("executor",requestId+":case-result",token).ConfigureAwait(false);
        var native=await audit.ReadAsync(request,token).ConfigureAwait(false);
        if(retained is null || native?.Facts?["temporalOriginal"] is not JsonObject independentlyPersisted || !JsonNode.DeepEquals(independentlyPersisted,retained)
            || retained["outcome"]?.GetValue<string>() is not ("Verified" or "Denied" or "Expired")) return null;
        if(retained["temporalEvidenceVersion"]?.GetValue<int>()!=2) return null; // Legacy raw originals remain readable but cannot establish exact evaluation timing.
        string caseId=request["caseId"]!.GetValue<string>();DateTimeOffset actualEvaluation;
        if(!caseId.StartsWith("S2.overlap-",StringComparison.Ordinal))
        {
            var decision=retained["authenticationDecision"]?.Deserialize<Hexalith.Platform.Custody.TrustedEnvelopeResult>(CustodyBindingContract.Json);
            if(decision?.EvaluatedAt is not { } evaluatedAt || evaluatedAt.ToUnixTimeMilliseconds()!=retained["checkedAt"]?.GetValue<long>() || decision.Status.ToString()!=retained["status"]?.GetValue<string>()) return null;
            actualEvaluation=evaluatedAt;
            if(retained["evaluatedAtExact"] is { } timestamp && timestamp.Deserialize<DateTimeOffset>()!=actualEvaluation) return null;
        }
        else
        {
            // Millisecond-only historical overlap bodies cannot prove which side of the native boundary ran.
            if(retained["evaluatedAtExact"] is not { } exact) return null;actualEvaluation=exact.Deserialize<DateTimeOffset>();
            if(actualEvaluation.ToUnixTimeMilliseconds()!=retained["checkedAt"]?.GetValue<long>()) return null;
        }
        if(caseId.StartsWith("S2.expiry.",StringComparison.Ordinal))
        {
            var edge=retained["originalEnvelope"]!["ExpiresAt"]!.Deserialize<DateTimeOffset>();
            if(!ExactSide(caseId,actualEvaluation,edge)) return null;
        }
        if(caseId.StartsWith("S2.overlap-",StringComparison.Ordinal))
        {
            if(native.Facts["rotationOriginal"] is not JsonObject rotation || rotation["rotatedAtExact"] is not { } exactRotation
                || retained["keyMetadata"] is not JsonObject metadata || metadata["RetiredAt"] is not { } retired) return null;
            var actualRotation=exactRotation.Deserialize<DateTimeOffset>();
            if(actualRotation!=retired.Deserialize<DateTimeOffset>() || actualRotation.ToUnixTimeMilliseconds()!=rotation["rotatedAt"]?.GetValue<long>()) return null;
            long overlap=retained["rotationOverlapMilliseconds"]!.GetValue<long>();
            if(overlap!=600000 || !ExactSide(caseId,actualEvaluation,actualRotation.AddMilliseconds(overlap))) return null;
        }
        string receipt=QualificationProtocolSchema.Digest(retained);var state=QualificationDeniedCaseState.Create(request,native.OriginalRevision,receipt);
        state["outcome"]=retained["outcome"]!.DeepClone();state["status"]=retained["outcome"]!.DeepClone();
        state["timing"]!["checkedAt"]=retained["checkedAt"]!.DeepClone();
        state["temporalEvaluation"]=new JsonObject { ["version"]=2,["evaluatedAt"]=retained["checkedAt"]!.DeepClone(),["originalResultSha256"]=receipt };
        if(request["caseId"]!.GetValue<string>().StartsWith("S2.expiry.",StringComparison.Ordinal))
        {
            long expires=retained["originalEnvelope"]!["ExpiresAt"]!.Deserialize<DateTimeOffset>().ToUnixTimeMilliseconds();
            state["exclusiveExpiry"]=expires;state["timing"]!["exclusiveExpiry"]=expires;
        }
        if(retained["replacementEnvelope"] is JsonObject replacement) state["keyVersion"]=replacement["SigningKeyVersion"]!.DeepClone();
        if(request["caseId"]!.GetValue<string>().StartsWith("S2.overlap-",StringComparison.Ordinal))
        {
            if(native.Facts["rotationOriginal"] is not JsonObject rotation) return null;
            state["rotation"]=rotation.DeepClone();state["rotation"]!.AsObject().Remove("rotatedAtExact");state["keyVersion"]=retained["originalEnvelope"]!["SigningKeyVersion"]!.DeepClone();
            state["timing"]!["rotatedAt"]=rotation["rotatedAt"]!.DeepClone();state["timing"]!["overlapEndsAt"]=rotation["rotatedAt"]!.GetValue<long>()+retained["rotationOverlapMilliseconds"]!.GetValue<long>();
        }
        DateTimeOffset? boundary=caseId.StartsWith("S2.expiry.",StringComparison.Ordinal)?retained["originalEnvelope"]!["ExpiresAt"]!.Deserialize<DateTimeOffset>():caseId.StartsWith("S2.overlap-",StringComparison.Ordinal)?retained["keyMetadata"]!["RetiredAt"]!.Deserialize<DateTimeOffset>().AddMilliseconds(600000):null;
        var precision=new JsonObject { ["evaluatedAtExact"]=JsonSerializer.SerializeToNode(actualEvaluation,CustodyBindingContract.Json),["boundaryExact"]=JsonSerializer.SerializeToNode(boundary,CustodyBindingContract.Json),["rotationOriginal"]=native.Facts?["rotationOriginal"]?.DeepClone() };
        if(!ValidPrecision(caseId,retained,state,precision)) return null;
        return new(requestId,receipt,"temporal:"+native.OriginalRevision,new JsonObject { ["nativeTemporalPrecision"]=precision,["state"]=state,["nativeStageBoundary"]=native.Facts?["nativeStageBoundary"]?.DeepClone(),["receipts"]=native.Receipts.DeepClone(),["nativeMutationSources"]=native.Facts?["nativeMutationSources"]?.DeepClone(),["targetMutations"]=native.TargetMutations.DeepClone(),
            ["surfaceScans"]=native.SurfaceScans.DeepClone(),["copyCensus"]=native.CopyCensus.DeepClone(),["linearization"]=native.Facts?["linearization"]?.DeepClone()??new JsonObject(),["independentTemporalVerification"]=independentlyPersisted.DeepClone() });
    }
    internal static bool ValidPrecision(string id,JsonObject original,JsonObject state,JsonObject? precision)
    {
        if(precision is null || precision["evaluatedAtExact"] is null) return false;
        bool overlap=id.StartsWith("S2.overlap-",StringComparison.Ordinal),expiry=id.StartsWith("S2.expiry.",StringComparison.Ordinal);
        var actual=overlap?original["evaluatedAtExact"]!.Deserialize<DateTimeOffset>():original["authenticationDecision"]!.Deserialize<Hexalith.Platform.Custody.TrustedEnvelopeResult>(CustodyBindingContract.Json)!.EvaluatedAt;
        if(actual is null || precision["evaluatedAtExact"]!.Deserialize<DateTimeOffset>()!=actual || original["checkedAt"]!.GetValue<long>()!=actual.Value.ToUnixTimeMilliseconds()
            || state["timing"]!["checkedAt"]!.GetValue<long>()!=actual.Value.ToUnixTimeMilliseconds()) return false;
        DateTimeOffset? edge=null;
        if(expiry)
        {
            edge=original["originalEnvelope"]!["ExpiresAt"]!.Deserialize<DateTimeOffset>();
            if(state["exclusiveExpiry"]!.GetValue<long>()!=edge.Value.ToUnixTimeMilliseconds() || state["timing"]!["exclusiveExpiry"]!.GetValue<long>()!=edge.Value.ToUnixTimeMilliseconds()) return false;
        }
        if(overlap)
        {
            if(precision["rotationOriginal"] is not JsonObject native || native["rotatedAtExact"] is null || original["rotationOverlapMilliseconds"]!.GetValue<long>()!=600000) return false;
            var retired=original["keyMetadata"]!["RetiredAt"]!.Deserialize<DateTimeOffset>();edge=retired.AddMilliseconds(600000);
            var projected=(JsonObject)native.DeepClone();projected.Remove("rotatedAtExact");
            if(native["rotatedAtExact"]!.Deserialize<DateTimeOffset>()!=retired || native["rotatedAt"]!.GetValue<long>()!=retired.ToUnixTimeMilliseconds() || !JsonNode.DeepEquals(projected,state["rotation"])
                || state["timing"]!["rotatedAt"]!.GetValue<long>()!=retired.ToUnixTimeMilliseconds() || state["timing"]!["overlapEndsAt"]!.GetValue<long>()!=edge.Value.ToUnixTimeMilliseconds()) return false;
        }
        if(edge is null) return precision["boundaryExact"] is null;
        return precision["boundaryExact"]?.Deserialize<DateTimeOffset>()==edge && ExactSide(id,actual.Value,edge.Value);
    }
    private static bool ExactSide(string id,DateTimeOffset evaluated,DateTimeOffset edge)
    {
        int actual=evaluated.CompareTo(edge);int projected=evaluated.ToUnixTimeMilliseconds().CompareTo(edge.ToUnixTimeMilliseconds());
        int required=id.EndsWith("before",StringComparison.Ordinal)?-1:id.EndsWith("after",StringComparison.Ordinal)?1:0;
        return Math.Sign(actual)==required && Math.Sign(projected)==required;
    }
}
