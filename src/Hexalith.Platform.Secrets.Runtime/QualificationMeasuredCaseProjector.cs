using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Concrete projection of independently signed exact native state/receipt reads. Missing actual facts are explicit blockers; no expected metric values are accepted.</summary>
public sealed class QualificationMeasuredCaseProjector(QualificationProofBinding independentlyEnrolledObserver,IQualificationRecoveryController controlVerifier,
    IReadOnlyDictionary<string,IReadOnlyList<string>> missingInstalledReaders,TimeProvider? clock=null,QualificationMutationTrust? mutationTrust=null):IQualificationMeasuredCaseProjector
{
    private static readonly HashSet<string> SupportedMetrics=new(["immutableIdentity","immutableOutcome","persistedProofCount","activeCredentials","destroyedTargets","signingAttempts","originalEffects","activations","forbiddenEffects","foreignEffects","leakCount","allCopyRevival","atomicAllOrNone","exactIndependentDecision","currentActorRoleEvidence","exactReferenceCount","independentlyCurrentInventory","authenticatedWrap","ownedPlaintextCleared","detachedEs256","publicAnchorRetained","retainedEnvelopeVerifier","newKeyAndNonce","immutableLogicalId","revokedEffects","exactReceipts","currentLifecycleReservation","retainedPins","staleSafetyVerdicts","retainedDigestVerifiers","obsoleteUnissuedAttempts","stableSealBatchManifest","capabilityExpiryFields","canonicalFieldCount","actualIssueRevisionSeparate","globalKeyBlock","exactNoIssueProof","nonceRecords","directPrincipalRelease","immutableDeliveredAt","renewedExpiry","replayRetentionSeconds","renewedFirstSeen","reservedSystemNamespace","successfulRevisionInSignedBytes","raceWinners","immutableReservation","completeAffectedBatchEnumeration","capabilityCompromiseBlockProof","successorDispatchProof","exactNewKeyBlockReceipt","immutableOriginalOutcome","immutableOriginalRequest","restoredIdentityUnchanged","exactOriginalReceipt","replacementKeyEffects","openProductDispositionEffects","legacyEpochWrites","nextAttestationOrdinal","exactReplayStreamWrites"],StringComparer.Ordinal);
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
            var sources=new List<JsonObject>();var states=new List<JsonObject>();var proofs=new List<QualificationObservedOriginal>();
            for(int index=0;index<4;index++)
            {
                var snapshot=(JsonObject)snapshots[index]!;
                var proof=snapshot["sourceProof"]!.Deserialize<QualificationObservedOriginal>(CustodyBindingContract.Json)!;
                if(!Verify(proof,requestId) || proof.StageEvidence is not { } signedStage
                    || signedStage.Boundary.Version!="EXT-SECRETS-1.native-observation.v2" || signedStage.Boundary.CaseRequestSha256!=QualificationProtocolSchema.Digest(original)
                    || signedStage.Boundary.Stage!=stages[index] || signedStage.Native!=QualificationObservationStage.RequireNative(proof,signedStage.Boundary)
                    || signedStage.Boundary.PredecessorSha256!=(index==0?QualificationProtocolSchema.Digest(original):QualificationProtocolSchema.Digest(snapshots[index-1]!["sourceProof"]!))
                    || snapshot["stage"]?.GetValue<string>()!=stages[index] || snapshot["originalRequestId"]?.GetValue<string>()!=requestId
                    || snapshot["originalResultSha256"]?.GetValue<string>()!=proof.OriginalReceiptSha256 || snapshot["stateSha256"]?.GetValue<string>()!=QualificationProtocolSchema.Digest(proof.Source)
                    || snapshot["authorityRevision"]?.GetValue<string>()!=proof.AuthorityRevision
                    || !JsonNode.DeepEquals(proof.Source,snapshot["source"]) || proof.Source["state"] is not JsonObject state
                    || state["requestId"]?.GetValue<string>()!=requestId || state["receiptId"]?.GetValue<string>()!=proof.OriginalReceiptSha256
                    || !JsonNode.DeepEquals(state["tenantId"],original["enrolledScope"]!["tenantId"])
                    || !JsonNode.DeepEquals(state["logicalId"],original["caseTargetId"]) || !JsonNode.DeepEquals(state["targetIds"],original["enrolledScope"]!["targetIds"])
                    || !JsonNode.DeepEquals(state["providerCopyIds"],original["enrolledScope"]!["providerCopyIds"]) || observations["originalResultSha256"]?.GetValue<string>()!=proof.OriginalReceiptSha256)
                    return Block("IndependentSignedExactNativeCaseState");
                if(QualificationTemporalCaseWorkflow.Supported(id) && (proof.Source["independentTemporalVerification"] is not JsonObject temporal || temporal["temporalEvidenceVersion"]?.GetValue<int>()!=2
                    || state["temporalEvaluation"]?["version"]?.GetValue<int>()!=2 || !JsonNode.DeepEquals(state["temporalEvaluation"]?["evaluatedAt"],temporal["checkedAt"])
                    || !JsonNode.DeepEquals(state["timing"]?["checkedAt"],temporal["checkedAt"]) || state["temporalEvaluation"]?["originalResultSha256"]?.GetValue<string>()!=proof.OriginalReceiptSha256
                    || !QualificationTemporalOriginalReader.ValidPrecision(id,temporal,state,proof.Source["nativeTemporalPrecision"] as JsonObject))) return Block("ExactOwnedTemporalDecisionV2");
                if(!ValidCensus(proof.Source)) return Block("CompleteExactSurfaceAndTargetMutationCensusSchema");
                foreach(var mutation in proof.Source["targetMutations"]!.AsArray().Cast<JsonObject>())
                {
                    if(mutationTrust is null) return Block("InstalledExactNativeMutationAuthorizationAuthorityAndTargetMappingsV2");
                    var evidence=mutation["authorizationEvidence"]?.Deserialize<QualificationNativeMutationEvidence>(CustodyBindingContract.Json);
                    if(evidence is null || !evidence.Verify(original,mutationTrust.Authority,proof.ObservedAt) || proof.Source["nativeMutationSources"] is not JsonArray nativeSources || !evidence.MatchesNative(nativeSources)
                        || mutation["nativeMutationId"]?.GetValue<string>()!=evidence.MutationId || mutation["authorizationReceiptId"]?.GetValue<string>()!=evidence.AuthorizationId
                        || mutation["targetTenantId"]?.GetValue<string>()!=evidence.Source.TenantId || mutation["targetDomain"]?.GetValue<string>()!=evidence.Source.Domain
                        || mutation["targetObjectId"]?.GetValue<string>()!=evidence.Source.AggregateId || mutation["targetId"]?.GetValue<string>()!=evidence.TargetId
                        || !mutationTrust.TargetMappings.TryGetValue(QualificationMutationTrust.TargetKey(evidence.Source.TenantId,evidence.Source.Domain,evidence.Source.AggregateId),out var mapped) || mapped!=evidence.TargetId)
                        return Block("AuthenticatedExactNativeMutationDispatchOriginalTargetMethodPrincipalAndBoundaryV2");
                }
                if(!ValidHistory(proof.Source,original,proof.OriginalReceiptSha256,index==0?null:sources[^1])) return Block("CompleteExactCorrelatedNativeReceiptHistory");
                sources.Add(proof.Source);states.Add(state);proofs.Add(proof);
            }
            int controlIndex=0;
            foreach(JsonNode? node in controls)
            {
                var control=node!.Deserialize<QualificationRecoveryControlOriginal>(CustodyBindingContract.Json)!;
                var expected=(JsonObject)original.DeepClone();expected["originalResultSha256"]=observations["originalResultSha256"]!.DeepClone();
                if(!controlVerifier.Verify(control,expected) || control.RequestId!=requestId || control.Operation!=(controls.IndexOf(node)==0?"restart":"restore")
                    || control.TargetCommit!=SecretsProof.Text(original,"targetCommit") || control.IsolatedTargetId!=SecretsProof.Text(original,"isolatedTargetId")
                    || control.OriginalReceiptSha256!=observations["originalResultSha256"]?.GetValue<string>() || control.BeforeIncarnation==control.AfterIncarnation)
                    return Block("IndependentNativeRecoveryControlOriginal");
                var before=proofs[controlIndex].StageEvidence!;var after=proofs[controlIndex+1].StageEvidence!;
                if(before.Native.SourceIncarnation!=control.BeforeIncarnation || after.Native.SourceIncarnation!=control.AfterIncarnation
                    || after.Boundary.ControlOperationId!=control.OperationId || after.Boundary.ControlOriginalSha256!=QualificationProtocolSchema.Digest(node!)
                    || after.Native.ControlOriginalSha256!=QualificationProtocolSchema.Digest(node!)
                    || !sources[controlIndex+1]["receipts"]!.AsArray().Any(value=>value?["kind"]?.GetValue<string>()==(controlIndex==0?"Restart":"Restore") && QualificationProtocolSchema.Digest(value!)==control.ControlReceiptSha256))
                    return Block("ExactSignedStageControlAndNativeIncarnationChain");
                controlIndex++;
            }
            if(!ValidReadonlyLookup(proofs[3],proofs[2],original)) return Block("ExactIndependentlyRetainedReadonlyLookupWitness");
            var mutationsById=new Dictionary<string,JsonObject>(StringComparer.Ordinal);
            foreach(var mutation in sources.SelectMany(source=>source["targetMutations"]!.AsArray()).Cast<JsonObject>())
            { string mutationId=mutation["nativeMutationId"]!.GetValue<string>();if(mutationsById.TryGetValue(mutationId,out var prior) && !JsonNode.DeepEquals(StableMutation(prior),StableMutation(mutation))) return Block("ConflictingExactNativeMutationHistory");mutationsById[mutationId]=mutation; }
            if(states.Skip(1).Any(state=>!JsonNode.DeepEquals(states[0],state))) return Block("NativeOriginalChangedAcrossRecovery");
            var stateOriginal=states[0];string outcome=stateOriginal["outcome"]!.GetValue<string>();string receipt=stateOriginal["receiptId"]!.GetValue<string>();
            if(role=="executor") return new JsonObject { ["outcome"]=outcome,["resultSha256"]=receipt };
            var last=sources[^1];
            if(last["receipts"] is not JsonArray receipts || receipts.Count is <1 or >99) return Block("CompleteIndependentlyPersistedReceiptTrace");
            var metrics=new JsonObject();
            foreach(string metric in QualificationProtocolSchema.MetricNames(id))
            {
                long? measured=Measure(metric,states,sources,receipts,original);
                if(measured is null) missing.Add("IndependentNativeMeasurement."+id+"."+metric);else metrics[metric]=measured.Value;
            }
            foreach(string unsafeMetric in new[]{"forbiddenEffects","foreignEffects","revokedEffects","openProductDispositionEffects","legacyEpochWrites","leakCount","allCopyRevival"})
                if(!metrics.ContainsKey(unsafeMetric) && Measure(unsafeMetric,states,sources,receipts,original) is >0) return Block("ObservedUnsafeNativeHistoryOutsideClosedMetricSet:"+unsafeMetric);
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
            var publicReceipts=new JsonArray();
            foreach(JsonObject nativeReceipt in receipts.Cast<JsonObject>())
            {
                var receiptBody=QualificationProtocolSchema.PublicReceipt(nativeReceipt);
                receiptBody["position"]=last["independentReceiptPositions"]![QualificationProtocolSchema.Digest(nativeReceipt)]!.DeepClone();publicReceipts.Add(receiptBody);
            }
            var lookup=last["readonlyLookupWitness"]!.AsObject();var lookupBody=lookup["body"]!.AsObject();
            var lookupReceipt=QualificationProtocolSchema.PublicReceipt(receipts[^1]!.AsObject());
            lookupReceipt["kind"]="Lookup";lookupReceipt["receiptId"]=receipt;lookupReceipt["position"]=lookup["retainedRevision"]!.DeepClone();lookupReceipt["occurredAt"]=lookupBody["occurredAt"]!.DeepClone();publicReceipts.Add(lookupReceipt);
            long previous=-1;
            foreach(JsonObject sourceReceipt in publicReceipts.Cast<JsonObject>())
            {
                if(sourceReceipt["position"]?.GetValue<long>() is not { } position || position<=previous || sourceReceipt["receiptId"]?.GetValue<string>() is not { Length:64 } originalReceipt)
                    return Block("ExactMonotonicNativeReceiptPositions");
                previous=position;
                var publicReceipt=QualificationProtocolSchema.PublicReceipt(sourceReceipt);
                var item=(JsonObject)publicReceipt.DeepClone();item["requestId"]=requestId;item["tenantId"]=stateOriginal["tenantId"]!.DeepClone();item["batchId"]=stateOriginal["batchId"]?.DeepClone();
                item["stateSha256"]=stateHash;item["originalReceiptId"]=receipt;
                if(sourceReceipt["kind"]?.GetValue<string>() is "OriginalPersisted" or "Restart" or "Restore" or "Lookup" && originalReceipt!=receipt)
                    return Block("RecoveryReceiptDoesNotIdentifyNativeOriginal");
                trace.Add(item);retainedReceipts.Add(publicReceipt);if(proofIds.Add(originalReceipt)) proofReferences.Add(originalReceipt);
            }
            MissingReadInputs=[];
            return new JsonObject { ["outcome"]=outcome,["resultSha256"]=receipt,["metrics"]=metrics,["snapshots"]=projectedSnapshots,
                ["linearization"]=ProjectLinearization(last),["proofReferences"]=proofReferences,["trace"]=trace,["retainedReceipts"]=retainedReceipts };
        }
        catch(Exception exception) when(exception is JsonException or InvalidOperationException or ArgumentException or CryptographicException or KeyNotFoundException or OverflowException)
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
            case "immutableOriginalRequest":case "restoredIdentityUnchanged":case "immutableIdentity":return states.All(value=>value["requestId"]?.ToJsonString()==state["requestId"]?.ToJsonString())?1:0;
            case "immutableOriginalOutcome":case "exactOriginalReceipt":case "immutableOutcome":return states.All(value=>value["outcome"]?.ToJsonString()==state["outcome"]?.ToJsonString() && value["receiptId"]?.ToJsonString()==state["receiptId"]?.ToJsonString())?1:0;
            case "persistedProofCount":return sources.Count;
            case "activeCredentials":return ArrayCount("activeCredentialIds");case "destroyedTargets":return ArrayCount("destroyedTargetIds");
            case "signingAttempts":return ArrayCount("attempts");
            case "originalEffects":return ReceiptCount("Effect");case "activations":return ReceiptCount("Activate");
            case "replacementKeyEffects":return MutationCount("keyVersion",original["enrolledScope"]?["successorKeyVersion"]?.GetValue<string>(),equal:true);
            case "openProductDispositionEffects":return MutationCount("productDisposition","Open",equal:true);
            case "legacyEpochWrites":return sources.All(source=>source["targetMutations"]!.AsArray().Count==0)?0:sources.Any(source=>source["guardState"]?["EpochId"] is not JsonValue)?null:
                sources.SelectMany(source=>source["targetMutations"]!.AsArray().OfType<JsonObject>().Where(value=>!JsonNode.DeepEquals(value["epochId"],source["guardState"]!["EpochId"]))).DistinctBy(value=>value["nativeMutationId"]!.GetValue<string>()).LongCount();
            case "forbiddenEffects":
                return AllMutations().LongCount(value=>value["authorizationReceiptId"] is null);
            case "foreignEffects":
                return AllMutations().LongCount(value=>!JsonNode.DeepEquals(value["targetTenantId"],original["enrolledScope"]!["tenantId"])
                    || !original["enrolledScope"]!["targetIds"]!.AsArray().Any(target=>JsonNode.DeepEquals(target,value["targetId"])));
            case "leakCount":
                // Maximum per measured surface detects every retained violation without claiming distinct sightings across repeated scans.
                return new[]{"Events","Projections","Responses","Logs","Traces","Evidence"}.Select(surface=>sources.Max(source=>source["surfaceScans"]!.AsArray().Single(value=>value!["surface"]!.GetValue<string>()==surface)!["matchCount"]!.GetValue<long>())).Aggregate(0L,(total,count)=>checked(total+count));
            case "allCopyRevival":
                var revivals=sources.Select(source=>CompleteCopyRevival(source,original)).ToArray();return revivals.Any(value=>value is null)?null:revivals.Max();
            case "atomicAllOrNone":return state["targetIds"] is JsonArray targets && state["destroyedTargetIds"] is JsonArray destroyed?destroyed.Count==0 || JsonNode.DeepEquals(targets,destroyed)?1:0:null;
            case "exactIndependentDecision":return last["independentDecisionVerification"]?["verified"] is JsonValue decision && decision.TryGetValue<bool>(out bool valid)?valid?1:0:null;
            case "currentActorRoleEvidence":return last["independentDecisionVerification"]?["currentRoleVerificationCount"]?.GetValue<long>();
            case "exactReferenceCount":return last["inventoryState"]?["Versions"] is JsonArray versions && last["inventoryKey"] is JsonObject exact
                ?versions.OfType<JsonObject>().LongCount(value=>JsonNode.DeepEquals(value["Key"],exact)):last["independentResolution"]?["exactResolvedReferenceCount"]?.GetValue<long>();
            case "independentlyCurrentInventory":return last["inventoryState"]?["Revision"] is JsonValue revision && revision.TryGetValue<long>(out long actualRevision)
                && last["inventoryAuthorityRevision"]?.GetValue<long>() is { } authorityRevision?actualRevision>0 && actualRevision==authorityRevision?1:0:null;
            case "retainedEnvelopeVerifier":return last["independentTemporalVerification"]?["keyMetadata"]?["State"]?.GetValue<string>()=="Retained" && last["independentTemporalVerification"]?["status"]?.GetValue<string>()=="Succeeded"?1:null;
            case "newKeyAndNonce":
                var old=last["independentTemporalVerification"]?["originalEnvelope"];var fresh=last["independentTemporalVerification"]?["replacementEnvelope"];
                return old is not null && fresh is not null?old["SigningKeyVersion"]?.GetValue<string>()!=fresh["SigningKeyVersion"]?.GetValue<string>() && old["DeliveryNonce"]?.GetValue<string>()!=fresh["DeliveryNonce"]?.GetValue<string>()?1:0:null;
            case "immutableLogicalId":return last["independentTemporalVerification"]?["originalEnvelope"]?["Identity"] is { } oldIdentity && last["independentTemporalVerification"]?["replacementEnvelope"]?["Identity"] is { } freshIdentity?JsonNode.DeepEquals(oldIdentity,freshIdentity)?1:0:null;
            case "revokedEffects":return AllMutations().LongCount(value=>value["keyState"]?.GetValue<string>()=="Revoked");
            case "constantTimeVerification":return null; // Successful calls do not establish implementation assurance or timing behavior.
            case "exactReplayStreamWrites":return last["independentReplayOriginal"]?["SourceRevision"]?.GetValue<long>();
            case "nonceRecords":return last["independentEnvelopeVerification"]?["replayOriginal"]?["SourceRevision"]?.GetValue<long>() is { } sourceRevision?sourceRevision:null;
            case "directPrincipalRelease":return last["independentDeliveryOriginal"]?["State"]?.GetValue<string>()=="Delivered" && last["independentDeliveryOriginal"]?["ObservedAt"] is not null?1:0;
            case "immutableDeliveredAt":return states.All(value=>JsonNode.DeepEquals(value["deliveredAt"],state["deliveredAt"]))?1:0;
            case "renewedExpiry":return states.Skip(1).LongCount(value=>!JsonNode.DeepEquals(value["exclusiveExpiry"],state["exclusiveExpiry"]));
            case "replayRetentionSeconds":return state["firstSeenAt"]?.GetValue<long>() is { } firstSeen && state["retainUntil"]?.GetValue<long>() is { } retain?(retain-firstSeen)/1000:null;
            case "renewedFirstSeen":return state["firstSeenAt"] is not null?states.Skip(1).LongCount(value=>!JsonNode.DeepEquals(value["firstSeenAt"],state["firstSeenAt"])):null;
            case "reservedSystemNamespace":return state["replayNamespace"]?.GetValue<string>() is { } replayNamespace?replayNamespace=="system/trusted-envelope-replay"?1:0:null;
            case "exactReceipts":return last["independentLifecycleVerification"]?["ExactReceiptCount"]?.GetValue<long>();
            case "currentLifecycleReservation":return last["independentLifecycleVerification"]?["CurrentReservationCount"]?.GetValue<long>();
            case "retainedPins":return last["independentLifecycleVerification"]?["RetainedPinCount"]?.GetValue<long>();
            case "authenticatedWrap":return ExportBoolean("AuthenticatedWrap");
            case "ownedPlaintextCleared":return ExportBoolean("OwnedPlaintextCleared");
            case "detachedEs256":return last["independentCapabilityVerification"]?["verified"] is JsonValue capability && capability.TryGetValue<bool>(out bool signatureValid)?signatureValid?1:0:ExportBoolean("DetachedEs256");
            case "publicAnchorRetained":return ExportBoolean("PublicAnchorRetained");
            case "staleSafetyVerdicts":return last["rotationVerdicts"] is JsonArray verdicts && last["rotationInventory"]?["Revision"]?.GetValue<long>() is { } currentRevision?verdicts.OfType<JsonObject>().LongCount(value=>value["BasisInventoryRevision"]?.GetValue<long>()<currentRevision && value["Status"]?.GetValue<string>()=="Current"):null;
            case "retainedDigestVerifiers":return last["retainedDigestCurrent"] is JsonObject currentDigest && last["retainedDigestOriginal"] is JsonObject oldDigest?currentDigest["Status"]?.GetValue<string>()=="Succeeded" && JsonNode.DeepEquals(currentDigest,oldDigest)?1:0:null;
            case "successfulRevisionInSignedBytes":return last["independentCapabilityVerification"]?["payload"] is JsonObject signedPayload?signedPayload.ContainsKey("CommittedIssuedGuardRevision")?1:0:null;
            case "canonicalFieldCount" when last["independentCapabilityVerification"] is JsonObject canonical:return canonical["canonicalFieldCount"]?.GetValue<long>();
            case "capabilityExpiryFields" when (last["independentCapabilityVerification"]?["payload"] is JsonObject capPayload):return capPayload.Count(value=>value.Key.Contains("Expir",StringComparison.OrdinalIgnoreCase));
            case "nextAttestationOrdinal":case "raceWinners":case "immutableReservation":case "completeAffectedBatchEnumeration":case "capabilityCompromiseBlockProof":case "successorDispatchProof":case "exactNewKeyBlockReceipt":
                return last["independentOwnerInvariants"]?["Metrics"]?[metric]?.GetValue<long>();
            case "obsoleteUnissuedAttempts":case "stableSealBatchManifest":case "capabilityExpiryFields":case "canonicalFieldCount":case "actualIssueRevisionSeparate":case "globalKeyBlock":case "exactNoIssueProof":
                return last["independentOwnerInvariants"]?["Metrics"]?[metric]?.GetValue<long>();
            default:return null;
        }
        IEnumerable<JsonObject> AllMutations()=>sources.SelectMany(source=>source["targetMutations"]!.AsArray().Cast<JsonObject>()).DistinctBy(value=>value["nativeMutationId"]!.GetValue<string>());
        long? MutationCount(string field,string? expected,bool equal)
        {
            if(expected is null || AllMutations().Any(value=>value[field] is not JsonValue)) return null;
            return AllMutations().LongCount(value=>(value[field]!.GetValue<string>()==expected)==equal);
        }
        long? ExportBoolean(string name)=>last["independentExportVerification"]?[name] is JsonValue value && value.TryGetValue<bool>(out bool actual)?actual?1:0:null;
    }
    private static JsonObject ProjectLinearization(JsonObject source)
    {
        var projected=source["linearization"]?.DeepClone() as JsonObject??new JsonObject();
        foreach(string field in new[]{"triggerPosition","effectPosition"})
        {
            if(projected[field] is null) continue;
            long native=projected[field]!.GetValue<long>();var receipt=source["receipts"]!.AsArray().Single(value=>value!["position"]!.GetValue<long>()==native)!;
            projected[field]=source["independentReceiptPositions"]![QualificationProtocolSchema.Digest(receipt)]!.DeepClone();
        }
        return projected;
    }
    private static bool ValidReadonlyLookup(QualificationObservedOriginal proof,QualificationObservedOriginal previous,JsonObject original)
    {
        var stage=proof.StageEvidence!;var prior=previous.StageEvidence!;
        if(stage.Boundary.ControlOperationId!=proof.RequestId+":lookup-stage" || stage.Native.SourceIncarnation!=prior.Native.SourceIncarnation
            || proof.Source["readonlyLookupWitness"] is not JsonObject witness || witness["body"] is not JsonObject body
            || witness["retainedRevision"]?.GetValue<long>() is not { } revision || revision<=proof.Source["independentReceiptPositions"]!.AsObject().Max(pair=>pair.Value!.GetValue<long>())
            || body["protocol"]?.GetValue<string>()!="EXT-SECRETS-1.observer-native-lookup.v1" || body["requestId"]?.GetValue<string>()!=proof.RequestId
            || body["caseRequestSha256"]?.GetValue<string>()!=QualificationProtocolSchema.Digest(original) || body["lookupOperationId"]?.GetValue<string>()!=proof.RequestId+":lookup-stage"
            || body["stage"]?.GetValue<string>()!="AfterLookup" || body["originalReceiptSha256"]?.GetValue<string>()!=proof.OriginalReceiptSha256 || body["authorityRevision"]?.GetValue<string>()!=proof.AuthorityRevision
            || body["sourceIncarnation"]?.GetValue<string>()!=stage.Native.SourceIncarnation || body["sourceRevision"]?.GetValue<string>()!=stage.Native.SourceRevision
            || body["precedingObservationSha256"]?.GetValue<string>()!=stage.Boundary.PredecessorSha256
            || body["precedingControlOperationId"]?.GetValue<string>()!=prior.Boundary.ControlOperationId || body["precedingControlSha256"]?.GetValue<string>()!=prior.Boundary.ControlOriginalSha256
            || body["occurredAt"]?.GetValue<long>() is not { } at || at<=0 || at>checked(proof.ObservedAt*1000+999)) return false;
        var returned=(JsonObject)proof.Source.DeepClone();returned.Remove("readonlyLookupWitness");returned.Remove("independentReceiptPositions");
        return body["returnedSourceSha256"]?.GetValue<string>()==QualificationProtocolSchema.Digest(returned);
    }
    private static bool ValidHistory(JsonObject source,JsonObject original,string receipt,JsonObject? previous)
    {
        if(source["receipts"] is not JsonArray history || history.Count is <1 or >100) return false;
        if(source["independentReceiptPositions"] is not JsonObject recordedPositions || recordedPositions.Count!=history.Count) return false;
        long position=-1,observedPosition=-1;int originals=0;
        foreach(var entry in history)
        {
            if(entry is not JsonObject row || !QualificationProtocolSchema.ValidNativeReceipt(row) || row["position"]!.GetValue<long>()<=position
                || !JsonNode.DeepEquals(row["requestId"],original["requestId"]) || !JsonNode.DeepEquals(row["tenantId"],original["enrolledScope"]!["tenantId"])
                || row["originalReceiptId"]?.GetValue<string>()!=receipt) return false;
            position=row["position"]!.GetValue<long>();
            string hash=QualificationProtocolSchema.Digest(row);
            if(recordedPositions[hash]?.GetValue<long>() is not { } observed || observed<=observedPosition || observed>9007199254740991) return false;
            if(previous?["independentReceiptPositions"] is JsonObject priorPositions && priorPositions[hash] is { } priorObserved && priorObserved.GetValue<long>()!=observed) return false;
            observedPosition=observed;
            if(row["kind"]!.GetValue<string>()=="OriginalPersisted") { originals++;if(row["receiptId"]!.GetValue<string>()!=receipt) return false; }
        }
        if(originals!=1) return false;
        if(previous?["receipts"] is JsonArray prior && (history.Count<prior.Count || prior.Where((value,index)=>!JsonNode.DeepEquals(value,history[index])).Any())) return false;
        return true;
    }
    private static bool ValidCensus(JsonObject source)
    {
        string[] surfaces=["Events","Projections","Responses","Logs","Traces","Evidence"];
        if(source["surfaceScans"] is not JsonArray scans || scans.Count!=surfaces.Length || source["targetMutations"] is not JsonArray mutations) return false;
        var seen=new HashSet<string>(StringComparer.Ordinal);long total=0;
        foreach(var node in scans)
        {
            if(node is not JsonObject scan || scan["surface"] is not JsonValue name || !name.TryGetValue<string>(out var surface)
                || !surfaces.Contains(surface,StringComparer.Ordinal) || !seen.Add(surface) || scan["matchCount"] is not JsonValue count
                || !count.TryGetValue<long>(out var matches) || matches<0) return false;
            total=checked(total+matches);
        }
        var mutationIds=new HashSet<string>(StringComparer.Ordinal);
        foreach(var node in mutations)
        {
            if(node is not JsonObject mutation || mutation["nativeMutationId"] is not JsonValue mutationId || !mutationId.TryGetValue<string>(out var mutationIdentity) || string.IsNullOrWhiteSpace(mutationIdentity) || !mutationIds.Add(mutationIdentity) || !mutation.ContainsKey("authorizationReceiptId")
                || mutation["authorizationReceiptId"] is not null && (mutation["authorizationReceiptId"] is not JsonValue authorization || !authorization.TryGetValue<string>(out var receipt) || string.IsNullOrWhiteSpace(receipt))) return false;
            foreach(string field in new[]{"targetTenantId","keyVersion","productDisposition","epochId","keyState"})
                if(mutation[field] is not JsonValue value || !value.TryGetValue<string>(out var text) || string.IsNullOrWhiteSpace(text)) return false;
        }
        return seen.Count==surfaces.Length;
    }
    private static JsonObject StableMutation(JsonObject mutation)
    {
        var value=(JsonObject)mutation.DeepClone();var evidence=value["authorizationEvidence"]?.Deserialize<QualificationNativeMutationEvidence>(CustodyBindingContract.Json);
        value.Remove("authorizationEvidence");
        if(evidence is not null) { value["sourceIncarnation"]=evidence.Source.SourceIncarnation;value["sourceRevision"]=evidence.SourceRevision;value["nativeEventDigest"]=evidence.NativeEventDigest; }
        return value;
    }
    private static long? CompleteCopyRevival(JsonObject source,JsonObject original)
    {
        if(original["enrolledScope"]?["providerCopyIds"] is not JsonArray enrolled
            || source["copyCensus"] is not JsonArray census || census.Count!=enrolled.Count) return null;
        // An explicitly enrolled empty set is meaningful only for cases with no physical copies in scope.
        if(enrolled.Count==0) return 0;
        if(source["destructionOriginals"] is not JsonArray destructions || destructions.Count!=enrolled.Count || destructions.Any(value=>value is not JsonObject)) return null;
        var expected=enrolled.Select(value=>value?.GetValue<string>()).ToArray();
        if(expected.Any(string.IsNullOrWhiteSpace) || expected.Distinct(StringComparer.Ordinal).Count()!=expected.Length) return null;
        var seen=new HashSet<string>(StringComparer.Ordinal);long revived=0;
        foreach(var value in census)
        {
            if(value is not JsonObject copy || copy["copyId"]?.GetValue<string>() is not { } id || !expected.Contains(id,StringComparer.Ordinal) || !seen.Add(id)
                || copy["priorDestructionReceiptId"]?.GetValue<string>() is not { Length:64 } receipt
                || copy["readability"]?.GetValue<string>() is not ("Readable" or "Erased")
                || !JsonNode.DeepEquals(copy["tenantId"],original["enrolledScope"]!["tenantId"])
                || !JsonNode.DeepEquals(copy["requestId"],original["requestId"]) || copy["nativeReadProofSha256"]?.GetValue<string>() is not { Length:64 }
                || destructions.OfType<JsonObject>().Count(item=>item["receiptId"]?.GetValue<string>()==receipt
                    && item["copyId"]?.GetValue<string>()==id && item["outcome"]?.GetValue<string>()=="Destroyed"
                    && JsonNode.DeepEquals(item["tenantId"],copy["tenantId"]) && JsonNode.DeepEquals(item["requestId"],copy["requestId"]))!=1) return null;
            if(copy["readability"]!.GetValue<string>()=="Readable") revived++;
        }
        return seen.Count==expected.Length?revived:null;
    }
}
