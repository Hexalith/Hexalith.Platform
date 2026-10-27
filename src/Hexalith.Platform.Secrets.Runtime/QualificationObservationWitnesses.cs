using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

// Publishes source-owned observation receipts, using actual independent original revisions rather than inventing backend read events or mixing native and observer ordinal domains.
internal static class QualificationObservationWitnesses
{
    internal static async Task<JsonObject> BindAsync(IQualificationOriginalStore originals,JsonObject request,QualificationObservedOriginal raw,
        QualificationObservationStageRequest stage,QualificationObservedOriginal? previous,TimeProvider clock,CancellationToken token)
    {
        if(raw.Source["receipts"] is not JsonArray receipts || receipts.Count is <1 or >99) throw new InvalidOperationException("Complete bounded native receipt history required.");
        var source=(JsonObject)raw.Source.DeepClone();var positions=new JsonObject();
        foreach(var node in receipts)
        {
            if(node is not JsonObject receipt || !QualificationProtocolSchema.ValidNativeReceipt(receipt)
                || !JsonNode.DeepEquals(receipt["requestId"],request["requestId"]) || !JsonNode.DeepEquals(receipt["tenantId"],request["enrolledScope"]!["tenantId"])
                || receipt["originalReceiptId"]?.GetValue<string>()!=raw.OriginalReceiptSha256) throw new InvalidOperationException("Exact correlated native receipt required.");
            string hash=QualificationProtocolSchema.Digest(receipt),id="native-receipt-v1:"+raw.RequestId+":"+hash;
            var body=new JsonObject { ["caseRequestSha256"]=stage.CaseRequestSha256,["nativeReceipt"]=receipt.DeepClone() };
            long revision=await RetainAsync(originals,id,body,token).ConfigureAwait(false);positions[hash]=revision;
        }
        source["independentReceiptPositions"]=positions;
        if(stage.Stage=="AfterLookup")
        {
            var native=QualificationObservationStage.RequireNative(raw,stage);
            if(previous?.StageEvidence is not { Boundary.Stage:"AfterRestore" } prior || native.SourceIncarnation!=prior.Native.SourceIncarnation
                || native.Stage=="AfterRestore" && (native.ControlOperationId!=prior.Native.ControlOperationId || native.ControlOriginalSha256!=prior.Native.ControlOriginalSha256))
                throw new InvalidOperationException("Exact native lookup and preceding verified restore boundary required.");
            var body=new JsonObject { ["protocol"]="EXT-SECRETS-1.observer-native-lookup.v1",["requestId"]=raw.RequestId,["caseRequestSha256"]=stage.CaseRequestSha256,
                ["lookupOperationId"]=raw.RequestId+":lookup-stage",["stage"]="AfterLookup",["originalReceiptSha256"]=raw.OriginalReceiptSha256,
                ["returnedSourceSha256"]=QualificationProtocolSchema.Digest(raw.Source),["authorityRevision"]=raw.AuthorityRevision,
                ["sourceIncarnation"]=native.SourceIncarnation,["sourceRevision"]=native.SourceRevision,["precedingObservationSha256"]=stage.PredecessorSha256,
                ["precedingControlOperationId"]=prior.Boundary.ControlOperationId,["precedingControlSha256"]=prior.Boundary.ControlOriginalSha256,
                ["occurredAt"]=clock.GetUtcNow().ToUnixTimeMilliseconds() };
            long revision=await RetainAsync(originals,"native-lookup-witness-v1:"+raw.RequestId,body,token).ConfigureAwait(false);
            source["readonlyLookupWitness"]=new JsonObject { ["body"]=body,["retainedRevision"]=revision };
        }
        return source;
    }
    private static async Task<long> RetainAsync(IQualificationOriginalStore originals,string id,JsonObject body,CancellationToken token)
    {
        await originals.BeginFinalAsync("observer",id,body,token).ConfigureAwait(false);
        var retained=await originals.ReadOriginalAsync("observer",id,token).ConfigureAwait(false);
        if(!retained.Known || retained.RetainedRevision<=0 || !JsonNode.DeepEquals(retained.Request,body)) throw new InvalidOperationException("InstalledExactIndependentObservationWitnessOriginalRevision");
        return retained.RetainedRevision;
    }
}
