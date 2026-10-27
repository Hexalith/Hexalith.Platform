using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Receiving implementation for all installed executor/observer/enrollment methods. Missing concrete drivers or measured projections deny before effects.</summary>
public sealed class QualificationOwnerOperationHandler(QualificationOwnerRole role,ECDsa signer,IQualificationOriginalStore originals,
    IReadOnlyDictionary<string,IQualificationOwnerCaseDriver> drivers,TimeProvider clock,IQualificationObservedOriginalReader? independentSourceReader=null,IQualificationAttemptAuthority? attemptAuthority=null) : IIndependentOwnerOperationHandler
{
    /// <inheritdoc />
    public IReadOnlySet<string> Methods { get; }=new HashSet<string>(["qualification-executor-execute","qualification-executor-lookup","qualification-observer-observe","qualification-enrollment-enroll","qualification-observer-read-source","qualification-observer-source-preflight","qualification-observer-lookup-source"],StringComparer.Ordinal);
    /// <inheritdoc />
    public bool RequiresCurrentResultAdmission(string method)=>Methods.Contains(method);
    /// <inheritdoc />
    public string? ResultPolicy(string method,JsonElement payload,IndependentAuthorityState current)
    {
        var request=JsonNode.Parse(payload.GetRawText()) as JsonObject;
        if(request?["original"] is JsonObject nested) request=nested;
        object? cases=role.Role=="enrollment"?current.QualificationCases:
            request?["caseId"]?.GetValue<string>() is { } id && current.QualificationCases?.TryGetValue(id,out var enrolled)==true?enrolled:null;
        return QualificationProtocolSchema.Digest(JsonSerializer.SerializeToNode(new { current.QualificationBindings,cases },CustodyBindingContract.Json)!);
    }
    /// <inheritdoc />
    public bool IsReadOnly(string method)=>method is "qualification-executor-lookup" or "qualification-observer-read-source" or "qualification-observer-source-preflight" or "qualification-observer-lookup-source";
    /// <inheritdoc />
    public Task<object?> RecoverAsync(string method,JsonElement payload,IndependentCallerGrant caller,IndependentAuthorityState current,CancellationToken token)
        =>HandleAsync(method=="qualification-executor-execute"?"qualification-executor-lookup":method,payload,caller,current,token);
    /// <inheritdoc />
    public async Task<object?> HandleAsync(string method,JsonElement payload,IndependentCallerGrant caller,IndependentAuthorityState current,CancellationToken cancellationToken)
    {
        string expectedMethod=role.Role switch { "executor"=>"qualification-executor-","observer"=>"qualification-observer-","enrollment"=>"qualification-enrollment-",_=>throw new InvalidOperationException("Enrolled role required.") };
        if(!method.StartsWith(expectedMethod,StringComparison.Ordinal) || caller.Role!=(method is "qualification-observer-read-source" or "qualification-observer-source-preflight" or "qualification-observer-lookup-source"?"observer-reader":role.Role) || caller.Revoked || caller.ValidUntil<=clock.GetUtcNow()
            || current.ValidUntil<=clock.GetUtcNow() || current.QualificationBindings is null || current.QualificationCases is null)
            throw new InvalidOperationException("Current independently enrolled receiving role required.");
        using var enrolledKey=ECDsa.Create();enrolledKey.ImportFromPem(role.PublicKeyPem);
        if(signer.ExportParameters(false).Curve.Oid.Value!="1.2.840.10045.3.1.7" || !signer.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(enrolledKey.ExportSubjectPublicKeyInfo()))
            throw new InvalidOperationException("Independently enrolled ES256 role signer required before effects.");
        var request=JsonNode.Parse(payload.GetRawText()) as JsonObject;
        QualificationObservationStageRequest? stage=null;
        if((method is "qualification-observer-read-source" or "qualification-observer-lookup-source") && request?["original"] is JsonObject nested)
        {
            stage=request["observationContext"]?.Deserialize<QualificationObservationStageRequest>(CustodyBindingContract.Json);
            if(request.Count!=2 || stage is null) throw new InvalidOperationException("Exact versioned observation boundary required.");
            request=(JsonObject)nested.DeepClone();
        }
        if(request is null || !QualificationProtocolSchema.ValidOwnerRequest(role.Role,request)
            || !new[]{"targetCommit","isolatedTargetId","planSha256"}.All(name=>current.QualificationBindings.TryGetValue(name,out string? value) && request[name]?.ToJsonString()==value))
            throw new InvalidOperationException("Exact current target, cohort attempt, configuration and plan required.");
        if(current.QualificationBindings.TryGetValue("runId",out var pinnedRun)?request["runId"]?.ToJsonString()!=pinnedRun:
            attemptAuthority is null || !await attemptAuthority.ValidateAsync(request,cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Current independently retained cohort attempt required.");
        if(role.Role!="enrollment" && (!current.QualificationCases.TryGetValue(request["caseId"]!.GetValue<string>(),out var pinnedCase)
            || !JsonNode.DeepEquals(JsonNode.Parse(pinnedCase.GetProperty("enrolledScope").GetRawText()),request["enrolledScope"])
            || !JsonNode.DeepEquals(JsonNode.Parse(pinnedCase.GetProperty("caseTargetId").GetRawText()),request["caseTargetId"])))
            throw new InvalidOperationException("Current independently enrolled exact recipe scope required.");
        string requestId=SecretsProof.Text(request,"requestId")!;
        if(method is "qualification-observer-read-source" or "qualification-observer-source-preflight" or "qualification-observer-lookup-source")
        {
            string sourceCase=SecretsProof.Text(request,"caseId")!;
            if(role.Role!="observer" || independentSourceReader is null || !current.QualificationCases.TryGetValue(sourceCase,out var enrolledSource)
                || !JsonNode.DeepEquals(JsonNode.Parse(enrolledSource.GetProperty("enrolledScope").GetRawText()),request["enrolledScope"]))
                throw new InvalidOperationException("Independent typed source reader and exact enrolled scope required.");
            if(method=="qualification-observer-source-preflight") return (await originals.PreflightObservationAsync(cancellationToken).ConfigureAwait(false)).Concat(await independentSourceReader.PreflightStagesAsync(request,cancellationToken).ConfigureAwait(false)).Distinct(StringComparer.Ordinal).ToArray();
            if(stage is null || stage.Version!="EXT-SECRETS-1.native-observation.v2" || stage.CaseRequestSha256!=QualificationProtocolSchema.Digest(request)
                || !QualificationObservationStage.Stages.Contains(stage.Stage,StringComparer.Ordinal))
                throw new InvalidOperationException("External.ExactSignedNativeStageContextV2");
            int stageIndex=Array.IndexOf(QualificationObservationStage.Stages,stage.Stage);
            QualificationObservedOriginal? predecessor=null;
            if(stageIndex>0)
            {
                var prior=await originals.LookupResultAsync("observer",QualificationObservationStage.Locator(requestId,QualificationObservationStage.Stages[stageIndex-1]),cancellationToken).ConfigureAwait(false);
                predecessor=prior?.Deserialize<QualificationObservedOriginal>(CustodyBindingContract.Json);
                if(predecessor?.StageEvidence is null || QualificationProtocolSchema.Digest(prior!)!=stage.PredecessorSha256)
                    throw new InvalidOperationException("Exact independently retained native observation predecessor required.");
            }
            else if(stage.PredecessorSha256!=stage.CaseRequestSha256) throw new InvalidOperationException("Exact original observation predecessor required.");
            string stageId=QualificationObservationStage.Locator(requestId,stage.Stage);
            var stageRequest=new JsonObject { ["original"]=request.DeepClone(),["observationContext"]=JsonSerializer.SerializeToNode(stage,CustodyBindingContract.Json) };
            if(await originals.LookupResultAsync("observer",stageId,cancellationToken).ConfigureAwait(false) is { } retainedStage)
            {
                var retainedObservation=retainedStage.Deserialize<QualificationObservedOriginal>(CustodyBindingContract.Json)!;
                if(retainedObservation.StageEvidence?.Boundary!=stage) throw new InvalidOperationException("Immutable native stage context changed.");
                return retainedObservation;
            }
            if(method=="qualification-observer-lookup-source") return null;
            bool firstStage=await originals.BeginAsync("observer",stageId,stageRequest,cancellationToken).ConfigureAwait(false);
            if(!firstStage) throw new InvalidOperationException("External.ExactHistoricalSignedNativeStageOriginalProtocol:"+stage.Stage);
            var raw=await independentSourceReader.ReadAsync(request,cancellationToken).ConfigureAwait(false)
                ??throw new InvalidOperationException("Exact native original read unavailable.");
            var nativeBoundary=QualificationObservationStage.RequireNative(raw,stage);
            if(predecessor is not null && (predecessor.OriginalReceiptSha256!=raw.OriginalReceiptSha256
                || stage.Stage=="AfterLookup" && predecessor.StageEvidence!.Native.SourceIncarnation!=nativeBoundary.SourceIncarnation))
                throw new InvalidOperationException("Native original or lookup incarnation changed.");
            if(raw.RequestId!=requestId) throw new InvalidOperationException("Native original request substitution.");
            var observedSource=await QualificationObservationWitnesses.BindAsync(originals,request,raw,stage,predecessor,clock,cancellationToken).ConfigureAwait(false);
            raw=raw with { Source=observedSource };
            long observed=clock.GetUtcNow().ToUnixTimeSeconds();
            var bound=raw with { ObservationIdentity=role.Identity,ObservationVersion=role.Version,ObservedAt=observed,ValidUntil=Math.Min(observed+30,current.ValidUntil.ToUnixTimeSeconds()),Signature=[],StageEvidence=new(stage,nativeBoundary) };
            byte[] bytes=QualificationProofVerifier.CanonicalBytes(JsonSerializer.SerializeToNode(bound,CustodyBindingContract.Json)!);
            lock(signer) bound=bound with { Signature=signer.SignData(bytes,HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation) };
            await originals.RetainAsync("observer",stageId,(JsonObject)JsonSerializer.SerializeToNode(bound,CustodyBindingContract.Json)!,cancellationToken).ConfigureAwait(false);
            return bound;
        }
        string proofId=requestId+":signed-proof";
        bool renewingEnrollment=false;
        if(await originals.LookupResultAsync(role.Role,proofId,cancellationToken).ConfigureAwait(false) is { } oldProof)
        {
            // Current receiving role/scope/attempt admission above is fresh; immutable signature/time remain original.
            if(role.Role=="enrollment" && (current.QualificationCases.Count!=QualificationProtocolSchema.CaseIds.Length
                || current.QualificationCases.Any(pair=>!JsonNode.DeepEquals(oldProof["payload"]?["caseScopes"]?[pair.Key],JsonNode.Parse(pair.Value.GetProperty("enrolledScope").GetRawText()))
                    || !JsonNode.DeepEquals(oldProof["payload"]?["caseTargetIds"]?[pair.Key],JsonNode.Parse(pair.Value.GetProperty("caseTargetId").GetRawText())))
                || new[]{"executorIdentity","executorVersion","observerIdentity","observerVersion","qualifiedReferencesSha256","currentProfileSha256"}
                    .Any(name=>!current.QualificationBindings.TryGetValue(name,out var currentValue) || oldProof["payload"]?[name]?.ToJsonString()!=currentValue)))
                throw new InvalidOperationException("Current independently enrolled case/profile bindings changed.");
            byte[] anchor=System.Text.Encoding.ASCII.GetBytes(role.PublicKeyPem);
            var verifier=new QualificationProofVerifier(new Dictionary<string,QualificationProofBinding>{{role.Role,new(role.Identity,role.Version,anchor,Convert.ToHexStringLower(SHA256.HashData(anchor)))}},clock);
            long at=oldProof["payload"]?["observedAt"]?.GetValue<long>()??-1;
            if(!verifier.VerifyOriginal(role.Role,request,oldProof,at,cancellationToken)) throw new InvalidOperationException("Exact immutable receiving proof required.");
            if(role.Role!="enrollment" || verifier.Verify(role.Role,request,oldProof,cancellationToken)) return oldProof;
            renewingEnrollment=true;
            // Enrollment is current authorization, whereas execution/observation proofs are immutable historical facts.
            // Retain each renewable frame separately. The original proof above remains the scope/profile anchor.
            long expiry=Math.Min(current.ValidUntil.ToUnixTimeSeconds(),caller.ValidUntil.ToUnixTimeSeconds());
            proofId=requestId+":signed-proof:enrollment-current-v2:"+(clock.GetUtcNow().ToUnixTimeSeconds()/15)+":"+expiry;
            if(await originals.LookupResultAsync(role.Role,proofId,cancellationToken).ConfigureAwait(false) is { } refreshed)
            {
                if(!verifier.Verify(role.Role,request,refreshed,cancellationToken)) throw new InvalidOperationException("Current retained enrollment frame required.");
                return refreshed;
            }
        }
        JsonObject result;
        if(role.Role=="enrollment")
        {
            // Receiving admission is required even when the public host was bypassed.
            if(!renewingEnrollment) await originals.BeginAsync(role.Role,requestId,request,cancellationToken).ConfigureAwait(false);
            string[] complete=QualificationProtocolSchema.CaseIds;
            if(!complete.ToHashSet(StringComparer.Ordinal).SetEquals(drivers.Keys) || !complete.ToHashSet(StringComparer.Ordinal).SetEquals(current.QualificationCases.Keys))
                throw new InvalidOperationException("Complete shipped and independently enrolled case driver set required.");
            var scopes=new JsonObject();var targets=new JsonObject();
            foreach(string id in complete)
            {
                var enrolled=JsonNode.Parse(current.QualificationCases[id].GetRawText()) as JsonObject??throw new InvalidOperationException("Closed enrolled case required.");
                var check=QualificationProtocolSchema.CaseRequest(request,id,enrolled);
                if(drivers[id].CaseId!=id || (await drivers[id].PreflightAsync(check,cancellationToken).ConfigureAwait(false)).Count!=0)
                    throw new InvalidOperationException("Case source or independently installed read/control protocol missing: "+id);
                scopes[id]=enrolled["enrolledScope"]?.DeepClone();targets[id]=enrolled["caseTargetId"]?.DeepClone();
            }
            result=Common(request);
            foreach(string name in new[]{"executorIdentity","executorVersion","observerIdentity","observerVersion","qualifiedReferencesSha256","currentProfileSha256"})
                result[name]=JsonNode.Parse(current.QualificationBindings[name]);
            result["targetClass"]="IsolatedQualification";result["productionMutationAllowed"]=false;result["currentProfileStatus"]="Current";
            result["completeCaseIds"]=new JsonArray(complete.Select(value=>(JsonNode?)JsonValue.Create(value)).ToArray());result["caseTargetIds"]=targets;result["caseScopes"]=scopes;
        }
        else
        {
            string id=SecretsProof.Text(request,"caseId")!;
            if(!drivers.TryGetValue(id,out var driver) || driver.CaseId!=id || !current.QualificationCases.TryGetValue(id,out var enrolled)
                || !JsonNode.DeepEquals(JsonNode.Parse(enrolled.GetProperty("enrolledScope").GetRawText()),request["enrolledScope"])
                || !JsonNode.DeepEquals(JsonNode.Parse(enrolled.GetProperty("caseTargetId").GetRawText()),request["caseTargetId"])
                || (await driver.PreflightAsync(request,cancellationToken).ConfigureAwait(false)).Count!=0)
                throw new InvalidOperationException("Exact installed concrete case workflow and independent observation inputs required.");
            // Absent lookups must never consume execute admission or create a signed execution original.
            bool lookup=role.Role=="executor" && method.EndsWith("lookup",StringComparison.Ordinal);
            if(lookup && await originals.ReadAdmissionAsync(role.Role,requestId,request,cancellationToken).ConfigureAwait(false)!=QualificationOriginalAdmissionState.Admitted) return null;
            bool first=!lookup && await originals.BeginAsync(role.Role,requestId,request,cancellationToken).ConfigureAwait(false);
            JsonObject? actual=role.Role=="observer"?await driver.ObserveAsync(request,cancellationToken).ConfigureAwait(false)
                : first && method.EndsWith("execute",StringComparison.Ordinal)?await driver.ExecuteAsync(request,cancellationToken).ConfigureAwait(false)
                :await driver.LookupAsync(request,cancellationToken).ConfigureAwait(false);
            if(actual is null) throw new InvalidOperationException("Exact original lookup or measured invariant projection pending.");
            result=(JsonObject)actual.DeepClone();
            foreach(var value in Common(request)) result[value.Key]=value.Value?.DeepClone();
            result["caseId"]=request["caseId"]!.DeepClone();result["caseTargetId"]=request["caseTargetId"]!.DeepClone();
        }
        result["identity"]=role.Identity;result["version"]=role.Version;
        long now=clock.GetUtcNow().ToUnixTimeSeconds();result["observedAt"]=now;result["validUntil"]=Math.Min(now+30,Math.Min(current.ValidUntil.ToUnixTimeSeconds(),caller.ValidUntil.ToUnixTimeSeconds()));
        if(result["validUntil"]!.GetValue<long>()<=now) throw new InvalidOperationException("Current caller and authority must remain valid before signed proof publication.");
        // Normalize numeric nodes before validating the closed public protocol.
        result=(JsonObject)JsonNode.Parse(result.ToJsonString())!;
        if(!QualificationProtocolSchema.Valid(role.Role,result,request)) throw new InvalidOperationException("Complete measured public case proof required.");
        var retained=(JsonObject)result.DeepClone();retained.Remove("observedAt");retained.Remove("validUntil");
        await originals.RetainAsync(role.Role,requestId,retained,cancellationToken).ConfigureAwait(false);
        using var publicKey=ECDsa.Create();publicKey.ImportFromPem(role.PublicKeyPem);
        if(!signer.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(publicKey.ExportSubjectPublicKeyInfo())) throw new InvalidOperationException("Independently enrolled role signer mismatch.");
        byte[] signature;
        lock(signer) signature=signer.SignData(QualificationProofVerifier.CanonicalBytes(result),HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        var proof=new JsonObject { ["payload"]=result,["signature"]=Convert.ToBase64String(signature) };
        await originals.BeginAsync(role.Role,proofId,request,cancellationToken).ConfigureAwait(false);
        try { await originals.RetainAsync(role.Role,proofId,proof,cancellationToken).ConfigureAwait(false); }
        catch(InvalidOperationException)
        {
            // A concurrent exact caller may have retained the unique proof first. Never replace its original time/signature.
            var prior=await originals.LookupResultAsync(role.Role,proofId,cancellationToken).ConfigureAwait(false);if(prior is null) throw;return prior;
        }
        return await originals.LookupResultAsync(role.Role,proofId,cancellationToken).ConfigureAwait(false)??throw new InvalidOperationException("Exact receiving proof acknowledgement pending.");
    }
    private static JsonObject Common(JsonObject original)
    {
        var value=new JsonObject();
        foreach(string field in new[]{"protocol","runId","targetCommit","isolatedTargetId","planSha256","requestId"}) value[field]=original[field]!.DeepClone();
        return value;
    }
}
