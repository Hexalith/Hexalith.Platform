using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Local concrete case workflow and independent projection. Implementations return only actual owner-derived public facts.</summary>
public interface IQualificationOwnerCaseDriver
{
    /// <summary>Gets the exact compiled case identity.</summary>
    string CaseId { get; }
    /// <summary>Names source/protocol/resource inputs before any effect; no expected outcome is an input.</summary>
    Task<IReadOnlyList<string>> PreflightAsync(JsonObject original,CancellationToken cancellationToken);
    /// <summary>Runs one admitted original operation and observation sequence.</summary>
    Task<JsonObject?> ExecuteAsync(JsonObject original,CancellationToken cancellationToken);
    /// <summary>Looks up only the exact retained original after uncertainty.</summary>
    Task<JsonObject?> LookupAsync(JsonObject original,CancellationToken cancellationToken);
    /// <summary>Reads and projects independently persisted owner originals.</summary>
    Task<JsonObject?> ObserveAsync(JsonObject original,CancellationToken cancellationToken);
}
/// <summary>Current independently enrolled role signer and exact public identity; construction does not enroll it.</summary>
public sealed record QualificationOwnerRole(string Role,string Identity,string Version,string PublicKeyPem);
/// <summary>Receiving implementation for all installed executor/observer/enrollment methods. Missing concrete drivers or measured projections deny before effects.</summary>
public sealed class QualificationOwnerOperationHandler(QualificationOwnerRole role,ECDsa signer,IQualificationOriginalStore originals,
    IReadOnlyDictionary<string,IQualificationOwnerCaseDriver> drivers,TimeProvider clock) : IIndependentOwnerOperationHandler
{
    /// <inheritdoc />
    public IReadOnlySet<string> Methods { get; }=new HashSet<string>(["qualification-executor-execute","qualification-executor-lookup","qualification-observer-observe","qualification-enrollment-enroll"],StringComparer.Ordinal);
    /// <inheritdoc />
    public async Task<object?> HandleAsync(string method,JsonElement payload,IndependentCallerGrant caller,IndependentAuthorityState current,CancellationToken cancellationToken)
    {
        string expectedMethod=role.Role switch { "executor"=>"qualification-executor-","observer"=>"qualification-observer-","enrollment"=>"qualification-enrollment-",_=>throw new InvalidOperationException("Enrolled role required.") };
        if(!method.StartsWith(expectedMethod,StringComparison.Ordinal) || caller.Role!=role.Role || caller.Revoked || caller.ValidUntil<=clock.GetUtcNow()
            || current.ValidUntil<=clock.GetUtcNow() || current.QualificationBindings is null || current.QualificationCases is null)
            throw new InvalidOperationException("Current independently enrolled receiving role required.");
        using var enrolledKey=ECDsa.Create();enrolledKey.ImportFromPem(role.PublicKeyPem);
        if(signer.ExportParameters(false).Curve.Oid.Value!="1.2.840.10045.3.1.7" || !signer.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(enrolledKey.ExportSubjectPublicKeyInfo()))
            throw new InvalidOperationException("Independently enrolled ES256 role signer required before effects.");
        var request=JsonNode.Parse(payload.GetRawText()) as JsonObject;
        if(request is null || !QualificationProtocolSchema.ValidOwnerRequest(role.Role,request)
            || !new[]{"runId","targetCommit","isolatedTargetId","planSha256"}.All(name=>current.QualificationBindings.TryGetValue(name,out string? value) && request[name]?.ToJsonString()==value))
            throw new InvalidOperationException("Exact current target, cohort attempt, configuration and plan required.");
        string requestId=SecretsProof.Text(request,"requestId")!;
        JsonObject result;
        if(role.Role=="enrollment")
        {
            string[] complete=QualificationProtocolSchema.CaseIds;
            if(!complete.ToHashSet(StringComparer.Ordinal).SetEquals(drivers.Keys) || !complete.ToHashSet(StringComparer.Ordinal).SetEquals(current.QualificationCases.Keys))
                throw new InvalidOperationException("Complete shipped and independently enrolled case driver set required.");
            var scopes=new JsonObject();var targets=new JsonObject();
            foreach(string id in complete)
            {
                var enrolled=JsonNode.Parse(current.QualificationCases[id].GetRawText()) as JsonObject??throw new InvalidOperationException("Closed enrolled case required.");
                var check=(JsonObject)request.DeepClone();check.Remove("operation");check["caseId"]=id;check["caseTargetId"]=enrolled["caseTargetId"]?.DeepClone();check["enrolledScope"]=enrolled["enrolledScope"]?.DeepClone();
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
            bool first=await originals.BeginAsync(role.Role,requestId,request,cancellationToken).ConfigureAwait(false);
            JsonObject? actual=role.Role=="observer"?await driver.ObserveAsync(request,cancellationToken).ConfigureAwait(false)
                : first && method.EndsWith("execute",StringComparison.Ordinal)?await driver.ExecuteAsync(request,cancellationToken).ConfigureAwait(false)
                :await driver.LookupAsync(request,cancellationToken).ConfigureAwait(false);
            if(actual is null) throw new InvalidOperationException("Exact original lookup or measured invariant projection pending.");
            result=(JsonObject)actual.DeepClone();
            foreach(var value in Common(request)) result[value.Key]=value.Value?.DeepClone();
            result["caseId"]=request["caseId"]!.DeepClone();result["caseTargetId"]=request["caseTargetId"]!.DeepClone();
        }
        result["identity"]=role.Identity;result["version"]=role.Version;
        long now=clock.GetUtcNow().ToUnixTimeSeconds();result["observedAt"]=now;result["validUntil"]=Math.Min(now+30,current.ValidUntil.ToUnixTimeSeconds());
        // Normalize numeric nodes before validating the closed public protocol.
        result=(JsonObject)JsonNode.Parse(result.ToJsonString())!;
        if(!QualificationProtocolSchema.Valid(role.Role,result,request)) throw new InvalidOperationException("Complete measured public case proof required.");
        var retained=(JsonObject)result.DeepClone();retained.Remove("observedAt");retained.Remove("validUntil");
        await originals.RetainAsync(role.Role,requestId,retained,cancellationToken).ConfigureAwait(false);
        using var publicKey=ECDsa.Create();publicKey.ImportFromPem(role.PublicKeyPem);
        if(!signer.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(publicKey.ExportSubjectPublicKeyInfo())) throw new InvalidOperationException("Independently enrolled role signer mismatch.");
        byte[] signature;
        lock(signer) signature=signer.SignData(QualificationProofVerifier.CanonicalBytes(result),HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return new JsonObject { ["payload"]=result,["signature"]=Convert.ToBase64String(signature) };
    }
    private static JsonObject Common(JsonObject original)
    {
        var value=new JsonObject();
        foreach(string field in new[]{"protocol","runId","targetCommit","isolatedTargetId","planSha256","requestId"}) value[field]=original[field]!.DeepClone();
        return value;
    }
}
