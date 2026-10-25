using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Closed phase command implementation over shipped case workflows and independent native publication. Unknown originals use lookup only; complete phase readiness requires every exact mapped workflow/read protocol.</summary>
public sealed class JointPhaseCommand(IQualificationOriginalStore originals,IReadOnlyDictionary<string,IReadOnlyList<(QualificationSourceWorkflow Workflow,JsonObject Original)>> mappedCases,
    JointPhaseNativeSourcePublisher independentPublisher,string evidenceDirectory,ECDsa ownerSigner)
{
    /// <summary>Runs only an explicitly mapped complete phase partition. Caller-supplied executable/artifact paths and predicate flags are never executable inputs.</summary>
    public async Task<JsonObject?> ExecuteAsync(JsonObject request,CancellationToken token)
    {
        if(request["requestId"]?.GetValue<string>() is not { Length:64 } requestId || !requestId.All(char.IsAsciiHexDigit) || request["cases"] is not JsonArray cases || cases.Count==0
            || !Path.IsPathFullyQualified(evidenceDirectory)) return null;
        var work=new List<(string CaseId,IReadOnlyList<(QualificationSourceWorkflow Workflow,JsonObject Original)> Operations)>();
        foreach(var node in cases)
        {
            string id=node!.GetValue<string>();if(!mappedCases.TryGetValue(id,out var operations) || operations.Count==0) return null;
            foreach(var (workflow,original) in operations)
                if(original["runId"]?.GetValue<string>()!=request["attemptId"]?.GetValue<string>() || original["targetCommit"] is not JsonValue
                    || original["caseId"]?.GetValue<string>()!=workflow.CaseId || (await workflow.Preflight(original,token).ConfigureAwait(false)).Count!=0) return null;
            work.Add((id,operations));
        }
        // Admission occurs only after the complete closed partition passes preflight. A lost process/result retains this identity outside the restore cut.
        bool first=await originals.BeginAsync("joint-phase-command",requestId,request,token).ConfigureAwait(false);
        if(await originals.LookupResultAsync("joint-phase-command",requestId,token).ConfigureAwait(false) is { } retained) return retained;
        var observations=new JsonArray();
        foreach(var (caseId,operations) in work)
        {
            foreach(var (workflow,original) in operations)
            {
                string step=requestId+":"+workflow.CaseId;bool fresh=first && await originals.BeginAsync("joint-phase-command",step,original,token).ConfigureAwait(false);
                var actual=fresh?await workflow.Execute(original,token).ConfigureAwait(false):await workflow.Lookup(original,token).ConfigureAwait(false);
                if(actual is null) return null;
            }
            var evidence=await independentPublisher.PublishAsync(caseId,request,token).ConfigureAwait(false);if(evidence is null) return null;
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
