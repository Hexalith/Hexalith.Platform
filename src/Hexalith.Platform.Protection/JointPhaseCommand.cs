using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Closed phase command implementation over shipped case workflows and independent native publication. Unknown originals use lookup only; complete phase readiness requires every exact mapped workflow/read protocol.</summary>
public sealed class JointPhaseCommand(JointPhaseCommandScope accepted,IQualificationOriginalStore originals,IReadOnlyDictionary<string,IReadOnlyList<(QualificationSourceWorkflow Workflow,JsonObject Original)>> mappedCases,
    IJointPhaseNativeSourcePublisher? independentPublisher,string evidenceDirectory,ECDsa ownerSigner,IJointSecretsCaseExecutor? secrets=null,IQualificationOriginalStore? executorOriginals=null)
{
    /// <summary>Gets precise missing full-partition readiness inputs; native uncertainty after dispatch remains a retained unresolved original.</summary>
    public IReadOnlyList<string> MissingInputs { get; private set; }=[];
    /// <summary>Runs only an explicitly mapped complete phase partition. Caller-supplied executable/artifact paths and predicate flags are never executable inputs.</summary>
    public async Task<JsonObject?> ExecuteAsync(JsonObject request,CancellationToken token)
    {
        if(request["requestId"]?.GetValue<string>() is not { Length:64 } requestId || !requestId.All(char.IsAsciiHexDigit) || request["cases"] is not JsonArray cases || cases.Count==0
            || !Path.IsPathFullyQualified(evidenceDirectory) || request["bindingDigest"]?.GetValue<string>()!=accepted.BindingDigest
            || request["phase"]?.GetValue<int>()!=accepted.Phase || !cases.Select(value=>value!.GetValue<string>()).SequenceEqual(accepted.Cases,StringComparer.Ordinal)) return null;
        var work=new List<(string CaseId,IReadOnlyList<(QualificationSourceWorkflow Workflow,JsonObject Original)> Operations)>();
        foreach(var node in cases)
        {
            string id=node!.GetValue<string>();
            if(id.StartsWith("secrets:",StringComparison.Ordinal)) { if(secrets is null || (await secrets.PreflightAsync(id,request,token).ConfigureAwait(false)).Count!=0) return null;work.Add((id,[]));continue; }
            if(!mappedCases.TryGetValue(id,out var operations) || operations.Count==0) return null;
            foreach(var (workflow,original) in operations)
                if(original["runId"]?.GetValue<string>()!=request["attemptId"]?.GetValue<string>() || original["targetCommit"]?.GetValue<string>()!=accepted.PlatformCommit
                    || original["caseId"]?.GetValue<string>()!=workflow.CaseId || (await workflow.Preflight(original,token).ConfigureAwait(false)).Count!=0) return null;
            if(independentPublisher is null) { MissingInputs=["IndependentNativeSourceReadinessProtocol"];return null; }
            var missing=await independentPublisher.PreflightAsync(id,request,token).ConfigureAwait(false);if(missing.Count!=0) { MissingInputs=missing;return null; }
            work.Add((id,operations));
        }
        // Admission occurs only after the complete closed partition passes preflight. A lost process/result retains this identity outside the restore cut.
        _=await originals.BeginAsync("joint-phase-command",requestId,request,token).ConfigureAwait(false);
        if(await originals.LookupResultAsync("joint-phase-command",requestId,token).ConfigureAwait(false) is { } retained) return retained;
        string originalPath=Path.Combine(evidenceDirectory,requestId,"owner-original.json");
        if(File.Exists(originalPath))
        {
            var info=new FileInfo(originalPath);if(info.LinkTarget is not null || info.Length is <=0 or >1024*1024) return null;
            var recovered=JsonNode.Parse(await File.ReadAllBytesAsync(originalPath,token).ConfigureAwait(false))!.AsObject();var old=recovered["payload"]!.AsObject();
            if(old["requestSha256"]?.GetValue<string>()!=QualificationProtocolSchema.Digest(request) || old["bindingDigest"]?.GetValue<string>()!=accepted.BindingDigest
                || old["observations"] is not JsonArray items || !items.Select(value=>value!["caseId"]!.GetValue<string>()).SequenceEqual(accepted.Cases,StringComparer.Ordinal)
                || !ownerSigner.VerifyData(QualificationProofVerifier.CanonicalBytes(old),Convert.FromBase64String(recovered["signature"]!.GetValue<string>()),HashAlgorithmName.SHA256,DSASignatureFormat.Rfc3279DerSequence)) return null;
            foreach(var item in items)
            {
                string file=Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(item!["caseId"]!.GetValue<string>())))+".json";
                byte[] actual=await File.ReadAllBytesAsync(Path.Combine(evidenceDirectory,requestId,file),token).ConfigureAwait(false);
                if(Convert.ToHexStringLower(SHA256.HashData(actual))!=item["evidenceSha256"]!.GetValue<string>()) return null;
            }
            await originals.RetainAsync("joint-phase-command",requestId,recovered,token).ConfigureAwait(false);return recovered;
        }
        var observations=new JsonArray();
        foreach(var (caseId,operations) in work)
        {
            foreach(var (workflow,original) in operations)
            {
                var admission=await (executorOriginals??originals).ReadAdmissionAsync("executor",original["requestId"]!.GetValue<string>()+":case-result",original,token).ConfigureAwait(false);
                var actual=admission==QualificationOriginalAdmissionState.Absent?await workflow.Execute(original,token).ConfigureAwait(false):
                    admission==QualificationOriginalAdmissionState.Admitted && workflow.Resume is { } resume?await resume(original,token).ConfigureAwait(false):await workflow.Lookup(original,token).ConfigureAwait(false);
                if(actual is null) return null;
            }
            var evidence=caseId.StartsWith("secrets:",StringComparison.Ordinal)?await secrets!.ExecuteAsync(caseId,request,token).ConfigureAwait(false):
                await independentPublisher!.PublishAsync(caseId,request,token).ConfigureAwait(false);if(evidence is null) return null;
            byte[] bytes=QualificationProofVerifier.CanonicalBytes(evidence);
            string name=Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(caseId)))+".json";
            await ImmutableFileArchive.PublishAsync(Path.Combine(evidenceDirectory,requestId,name),bytes,token).ConfigureAwait(false);
            observations.Add(new JsonObject { ["caseId"]=caseId,["evidenceSha256"]=Convert.ToHexStringLower(SHA256.HashData(bytes)) });
        }
        var payload=new JsonObject { ["requestSha256"]=QualificationProtocolSchema.Digest(request),["bindingDigest"]=request["bindingDigest"]!.DeepClone(),["observations"]=observations,["completedAt"]=DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
        byte[] signed;
        lock(ownerSigner) signed=ownerSigner.SignData(QualificationProofVerifier.CanonicalBytes(payload),HashAlgorithmName.SHA256,DSASignatureFormat.Rfc3279DerSequence);
        var result=new JsonObject { ["payload"]=payload,["signature"]=Convert.ToBase64String(signed) };
        await ImmutableFileArchive.PublishAsync(Path.Combine(evidenceDirectory,requestId,"owner-original.json"),QualificationProofVerifier.CanonicalBytes(result),token).ConfigureAwait(false);
        await originals.RetainAsync("joint-phase-command",requestId,result,token).ConfigureAwait(false);return result;
    }
}
