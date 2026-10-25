using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Typed reader for actual retained envelope/capability substitution denials and independently read native audit surfaces.</summary>
public sealed class QualificationDeniedOriginalReader(IQualificationOriginalStore readOnlyOriginals,IQualificationNativeCaseAuditReader audit,
    Func<JsonObject,CancellationToken,Task<JsonObject?>> independentlyVerify):IQualificationObservedOriginalReader
{
    /// <inheritdoc />
    public async Task<QualificationObservedOriginal?> ReadAsync(JsonObject original,CancellationToken cancellationToken)
    {
        string? caseId=SecretsProof.Text(original,"caseId"),requestId=SecretsProof.Text(original,"requestId");
        if(caseId is null || requestId is null || original["enrolledScope"] is not JsonObject scope) return null;
        bool envelope=caseId.StartsWith("S2.substitution.",StringComparison.Ordinal) || caseId is "S2.replay-reject.changed-fields" or "S2.replay-reject.cross-tenant" or "S2.replay-reject.foreign-issuer";
        bool capability=caseId.StartsWith("S4.capability-substitution.",StringComparison.Ordinal);
        if(!envelope && !capability) return null;
        var raw=await readOnlyOriginals.LookupResultAsync("executor",requestId+":case-result",cancellationToken).ConfigureAwait(false);
        var native=await audit.ReadAsync(original,cancellationToken).ConfigureAwait(false);
        if(raw is null || native is null || native.OriginalRevision<1 || SecretsProof.Text(raw,"caseId")!=caseId || SecretsProof.Text(raw,"requestId")!=requestId) return null;
        var verifiedOriginal=await independentlyVerify(original,cancellationToken).ConfigureAwait(false);
        if(verifiedOriginal is null || !JsonNode.DeepEquals(raw,verifiedOriginal)) return null;
        if(envelope && (!Denial(SecretsProof.Text(raw,"firstStatus")) || !Denial(SecretsProof.Text(raw,"exactRetryStatus")))
            || capability && (raw["verified"] is not JsonValue verified || !verified.TryGetValue<bool>(out bool actual) || actual)) return null;
        string receipt=QualificationProtocolSchema.Digest(raw);
        var state=QualificationDeniedCaseState.Create(original,native.OriginalRevision,receipt);
        if(capability)
        {
            if(raw["payload"] is not JsonObject payload) return null;
            foreach(var pair in new[]{("sealId","DestructionSealId"),("batchId","BatchId"),("manifestDigest","ManifestDigest"),("attestationOrdinal","AttestationOrdinal"),("keyVersion","CapabilityKeyVersion")})
                state[pair.Item1]=payload[pair.Item2]?.DeepClone();
        }
        return new(requestId,receipt,"original:"+native.OriginalRevision,new JsonObject { ["state"]=state,["receipts"]=native.Receipts.DeepClone(),["targetMutations"]=native.TargetMutations.DeepClone(),
            ["surfaceScans"]=native.SurfaceScans.DeepClone(),["copyCensus"]=native.CopyCensus.DeepClone(),["linearization"]=new JsonObject(),["sourceOriginal"]=raw.DeepClone() });
    }
    private static bool Denial(string? value)=>Enum.TryParse<CustodyStatus>(value,out var status) && status is CustodyStatus.Denied or CustodyStatus.UnknownKey or CustodyStatus.RevokedKey or CustodyStatus.Invalid
        or CustodyStatus.StaleProfile or CustodyStatus.Expired or CustodyStatus.FutureIssued or CustodyStatus.OutsideKeyWindow or CustodyStatus.InvalidTag or CustodyStatus.ScopeMismatch;
}
