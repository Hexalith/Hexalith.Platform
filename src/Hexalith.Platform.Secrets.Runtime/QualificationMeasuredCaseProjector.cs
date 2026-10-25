using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Concrete projection of independently signed exact native state/receipt reads. Missing actual facts are explicit blockers; no expected metric values are accepted.</summary>
public sealed class QualificationMeasuredCaseProjector(QualificationProofBinding independentlyEnrolledObserver,IQualificationRecoveryController controlVerifier,
    IReadOnlyDictionary<string,IReadOnlyList<string>> missingInstalledReaders,TimeProvider? clock=null):IQualificationMeasuredCaseProjector
{
    private static readonly HashSet<string> SupportedMetrics=new(["immutableIdentity","immutableOutcome","persistedProofCount","activeCredentials","destroyedTargets","signingAttempts","originalEffects","activations","forbiddenEffects","foreignEffects","leakCount","allCopyRevival","atomicAllOrNone","exactIndependentDecision","currentActorRoleEvidence","exactReferenceCount","independentlyCurrentInventory","authenticatedWrap","ownedPlaintextCleared","detachedEs256","publicAnchorRetained"],StringComparer.Ordinal);
    /// <summary>Gets precise unavailable facts from the most recent projection.</summary>
    public IReadOnlyList<string> MissingReadInputs { get; private set; }=[];
    /// <inheritdoc />
    public IReadOnlyList<string> MissingInputs(string caseId)=>(missingInstalledReaders.TryGetValue(caseId,out var missing)?missing:["IndependentCompleteNativeCaseReader."+caseId])
        .Concat(QualificationProtocolSchema.MetricNames(caseId).Where(metric=>!SupportedMetrics.Contains(metric)).Select(metric=>"SourceMetricProjection."+caseId+"."+metric)).ToArray();
    /// <inheritdoc />
    public JsonObject? Project(string role,JsonObject original,JsonObject observations)
    {
        var missing=new List<string>();
        try
        {
            string id=SecretsProof.Text(original,"caseId")!,requestId=SecretsProof.Text(original,"requestId")!;
            if(role is not ("executor" or "observer") || MissingInputs(id).Count!=0 || observations["requestId"]?.GetValue<string>()!=requestId || observations["snapshots"] is not JsonArray snapshots || snapshots.Count!=4
                || observations["controls"] is not JsonArray controls || controls.Count!=2) return Block("ExactFourStageNativeOriginals");
            string[] stages=["AfterEffect","AfterRestart","AfterRestore","AfterLookup"];
            var sources=new List<JsonObject>();var states=new List<JsonObject>();
            for(int index=0;index<4;index++)
            {
                var snapshot=(JsonObject)snapshots[index]!;
                var proof=snapshot["sourceProof"]!.Deserialize<QualificationObservedOriginal>(CustodyBindingContract.Json)!;
                if(!Verify(proof,requestId) || snapshot["stage"]?.GetValue<string>()!=stages[index]
                    || !JsonNode.DeepEquals(proof.Source,snapshot["source"]) || proof.Source["state"] is not JsonObject state
                    || state["requestId"]?.GetValue<string>()!=requestId || state["receiptId"]?.GetValue<string>()!=proof.OriginalReceiptSha256
                    || !JsonNode.DeepEquals(state["tenantId"],original["enrolledScope"]!["tenantId"])
                    || !JsonNode.DeepEquals(state["logicalId"],original["caseTargetId"]) || !JsonNode.DeepEquals(state["targetIds"],original["enrolledScope"]!["targetIds"])
                    || !JsonNode.DeepEquals(state["providerCopyIds"],original["enrolledScope"]!["providerCopyIds"]) || observations["originalResultSha256"]?.GetValue<string>()!=proof.OriginalReceiptSha256)
                    return Block("IndependentSignedExactNativeCaseState");
                sources.Add(proof.Source);states.Add(state);
            }
            foreach(JsonNode? node in controls)
            {
                var control=node!.Deserialize<QualificationRecoveryControlOriginal>(CustodyBindingContract.Json)!;
                var expected=(JsonObject)original.DeepClone();expected["originalResultSha256"]=observations["originalResultSha256"]!.DeepClone();
                if(!controlVerifier.Verify(control,expected) || control.RequestId!=requestId || control.Operation!=(controls.IndexOf(node)==0?"restart":"restore")
                    || control.TargetCommit!=SecretsProof.Text(original,"targetCommit") || control.IsolatedTargetId!=SecretsProof.Text(original,"isolatedTargetId")
                    || control.OriginalReceiptSha256!=observations["originalResultSha256"]?.GetValue<string>() || control.BeforeIncarnation==control.AfterIncarnation)
                    return Block("IndependentNativeRecoveryControlOriginal");
            }
            if(states.Skip(1).Any(state=>!JsonNode.DeepEquals(states[0],state))) return Block("NativeOriginalChangedAcrossRecovery");
            var stateOriginal=states[0];string outcome=stateOriginal["outcome"]!.GetValue<string>();string receipt=stateOriginal["receiptId"]!.GetValue<string>();
            if(role=="executor") return new JsonObject { ["outcome"]=outcome,["resultSha256"]=receipt };
            var last=sources[^1];
            if(last["receipts"] is not JsonArray receipts || receipts.Count is <4 or >100) return Block("CompleteIndependentlyPersistedReceiptTrace");
            var metrics=new JsonObject();
            foreach(string metric in QualificationProtocolSchema.MetricNames(id))
            {
                long? measured=Measure(metric,states,sources,receipts,original);
                if(measured is null) missing.Add("IndependentNativeMeasurement."+id+"."+metric);else metrics[metric]=measured.Value;
            }
            if(missing.Count!=0) { MissingReadInputs=missing;return null; }
            var projectedSnapshots=new JsonArray();var trace=new JsonArray();var retainedReceipts=new JsonArray();var proofReferences=new JsonArray();
            var proofIds=new HashSet<string>(StringComparer.Ordinal);
            foreach(JsonObject snapshot in snapshots.OfType<JsonObject>())
            {
                string hash=QualificationProtocolSchema.Digest(snapshot["sourceProof"]!);
                if(proofIds.Add(hash)) proofReferences.Add(hash);
            }
            string stateHash=QualificationProtocolSchema.Digest(stateOriginal);
            for(int index=0;index<4;index++) projectedSnapshots.Add(new JsonObject { ["stage"]=stages[index],["originalRequestId"]=requestId,
                ["originalResultSha256"]=receipt,["stateSha256"]=stateHash,["authorityRevision"]=snapshots[index]!["authorityRevision"]!.DeepClone(),["state"]=states[index].DeepClone() });
            long previous=-1;
            foreach(JsonObject sourceReceipt in receipts.OfType<JsonObject>())
            {
                if(sourceReceipt["position"]?.GetValue<long>() is not { } position || position<=previous || sourceReceipt["receiptId"]?.GetValue<string>() is not { Length:64 } originalReceipt)
                    return Block("ExactMonotonicNativeReceiptPositions");
                previous=position;
                var item=(JsonObject)sourceReceipt.DeepClone();item["requestId"]=requestId;item["tenantId"]=stateOriginal["tenantId"]!.DeepClone();item["batchId"]=stateOriginal["batchId"]?.DeepClone();
                item["stateSha256"]=stateHash;item["originalReceiptId"]=receipt;
                if(sourceReceipt["kind"]?.GetValue<string>() is "OriginalPersisted" or "Restart" or "Restore" or "Lookup" && originalReceipt!=receipt)
                    return Block("RecoveryReceiptDoesNotIdentifyNativeOriginal");
                trace.Add(item);retainedReceipts.Add(sourceReceipt.DeepClone());if(proofIds.Add(originalReceipt)) proofReferences.Add(originalReceipt);
            }
            MissingReadInputs=[];
            return new JsonObject { ["outcome"]=outcome,["resultSha256"]=receipt,["metrics"]=metrics,["snapshots"]=projectedSnapshots,
                ["linearization"]=last["linearization"]?.DeepClone()??new JsonObject(),["proofReferences"]=proofReferences,["trace"]=trace,["retainedReceipts"]=retainedReceipts };
        }
        catch(Exception exception) when(exception is JsonException or InvalidOperationException or ArgumentException or CryptographicException or KeyNotFoundException)
        { return Block("ClosedActualNativeObservationSchema"); }
    }
    private JsonObject? Block(string name) { MissingReadInputs=[name];return null; }
    private bool Verify(QualificationObservedOriginal proof,string requestId)
    {
        if(proof.RequestId!=requestId || proof.ObservationIdentity!=independentlyEnrolledObserver.Identity || proof.ObservationVersion!=independentlyEnrolledObserver.Version
            || proof.Signature is not { Length:64 } || proof.ObservedAt<=0 || proof.ObservedAt>(clock??TimeProvider.System).GetUtcNow().ToUnixTimeSeconds()
            || proof.ValidUntil<=proof.ObservedAt || proof.ValidUntil>proof.ObservedAt+30) return false;
        using var key=independentlyEnrolledObserver.OpenPublicKey();
        byte[] bytes=QualificationProofVerifier.CanonicalBytes(JsonSerializer.SerializeToNode(proof with { Signature=[] },CustodyBindingContract.Json)!);
        return key.VerifyData(bytes,proof.Signature,HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }
    private static long? Measure(string metric,IReadOnlyList<JsonObject> states,IReadOnlyList<JsonObject> sources,JsonArray receipts,JsonObject original)
    {
        var state=states[0];var last=sources[^1];
        long? ArrayCount(string name)=>state[name] is JsonArray values?values.Count:null;
        long? ReceiptCount(string kind)=>receipts.OfType<JsonObject>().LongCount(value=>value["kind"]?.GetValue<string>()==kind);
        switch(metric)
        {
            case "immutableIdentity":return states.All(value=>value["requestId"]?.ToJsonString()==state["requestId"]?.ToJsonString())?1:0;
            case "immutableOutcome":return states.All(value=>value["outcome"]?.ToJsonString()==state["outcome"]?.ToJsonString() && value["receiptId"]?.ToJsonString()==state["receiptId"]?.ToJsonString())?1:0;
            case "persistedProofCount":return sources.Count;
            case "activeCredentials":return ArrayCount("activeCredentialIds");case "destroyedTargets":return ArrayCount("destroyedTargetIds");
            case "signingAttempts":return ArrayCount("attempts");
            case "originalEffects":return ReceiptCount("Effect");case "activations":return ReceiptCount("Activate");
            case "forbiddenEffects":
                return last["targetMutations"] is JsonArray mutations?mutations.OfType<JsonObject>().LongCount(value=>value["authorizationReceiptId"] is null):null;
            case "foreignEffects":
                return last["targetMutations"] is JsonArray writes?writes.OfType<JsonObject>().LongCount(value=>!JsonNode.DeepEquals(value["targetTenantId"],original["enrolledScope"]!["tenantId"])):null;
            case "leakCount":
                if(last["surfaceScans"] is not JsonArray scans || !new[]{"Events","Projections","Responses","Logs","Traces","Evidence"}.All(name=>scans.OfType<JsonObject>().Any(value=>value["surface"]?.GetValue<string>()==name))) return null;
                return scans.OfType<JsonObject>().Sum(value=>value["matchCount"]!.GetValue<long>());
            case "allCopyRevival":return last["copyCensus"] is JsonArray copies?copies.OfType<JsonObject>().LongCount(value=>value["priorDestructionReceiptId"] is not null && value["readability"]?.GetValue<string>()=="Readable"):null;
            case "atomicAllOrNone":return state["targetIds"] is JsonArray targets && state["destroyedTargetIds"] is JsonArray destroyed?destroyed.Count==0 || JsonNode.DeepEquals(targets,destroyed)?1:0:null;
            case "exactIndependentDecision":return last["independentDecisionVerification"]?["verified"] is JsonValue decision && decision.TryGetValue<bool>(out bool valid)?valid?1:0:null;
            case "currentActorRoleEvidence":return last["independentDecisionVerification"]?["currentRoleVerificationCount"]?.GetValue<long>();
            case "exactReferenceCount":return last["inventoryState"]?["Versions"] is JsonArray versions && last["inventoryKey"] is JsonObject exact
                ?versions.OfType<JsonObject>().LongCount(value=>JsonNode.DeepEquals(value["Key"],exact)):last["independentResolution"]?["exactResolvedReferenceCount"]?.GetValue<long>();
            case "independentlyCurrentInventory":return last["inventoryState"]?["Revision"] is JsonValue revision && revision.TryGetValue<long>(out long actualRevision)
                && last["inventoryAuthorityRevision"]?.GetValue<long>() is { } authorityRevision?actualRevision>0 && actualRevision==authorityRevision?1:0:null;
            case "authenticatedWrap":return ExportBoolean("AuthenticatedWrap");
            case "ownedPlaintextCleared":return ExportBoolean("OwnedPlaintextCleared");
            case "detachedEs256":return ExportBoolean("DetachedEs256");
            case "publicAnchorRetained":return ExportBoolean("PublicAnchorRetained");
            default:return null;
        }
        long? ExportBoolean(string name)=>last["independentExportVerification"]?[name] is JsonValue value && value.TryGetValue<bool>(out bool actual)?actual?1:0:null;
    }
}
