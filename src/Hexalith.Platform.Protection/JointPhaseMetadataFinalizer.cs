using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Metadata-only recovery. Reads exact immutable case files and the admitted owner request; never constructs a workflow, material provider or native effect port.</summary>
public sealed class JointPhaseMetadataFinalizer(JointPhaseCommandScope accepted,IQualificationOriginalStore originals,string directory,ECDsa signer)
{
    /// <summary>Publishes only the missing signed index. Independent persisted observation is still required to establish any result.</summary>
    public async Task<JsonObject?> FinalizeAsync(JsonObject request,CancellationToken token)
    {
        string? id=request["requestId"]?.GetValue<string>();
        if(id is not { Length:64 } || !id.All(char.IsAsciiHexDigitLower) || request["bindingDigest"]?.GetValue<string>()!=accepted.BindingDigest
            || request["phase"]?.GetValue<int>()!=accepted.Phase || request["cases"] is not JsonArray cases
            || !cases.Select(value=>value!.GetValue<string>()).SequenceEqual(accepted.Cases,StringComparer.Ordinal) || !Path.IsPathFullyQualified(directory)
            || await originals.ReadAdmissionAsync("joint-phase-command",id,request,token).ConfigureAwait(false)!=QualificationOriginalAdmissionState.Admitted) return null;
        if(await originals.LookupResultAsync("joint-phase-command",id,token).ConfigureAwait(false) is { } retained) return retained;
        string root=Path.Combine(directory,id), index=Path.Combine(root,"owner-original.json");
        var observations=new JsonArray();
        foreach(string caseId in accepted.Cases)
        {
            string name=Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(caseId)))+".json";
            byte[] bytes=await Read(Path.Combine(root,name),token).ConfigureAwait(false);var value=SecretsProof.Parse(System.Text.Encoding.UTF8.GetString(bytes));
            if(value is null) return null;
            if(caseId.StartsWith("secrets:",StringComparison.Ordinal))
            {
                if(value["cohortRequestId"]?.GetValue<string>()!=id || value["request"] is not JsonObject original
                    || original["runId"]?.GetValue<string>()!=request["attemptId"]?.GetValue<string>() || original["targetCommit"]?.GetValue<string>()!=accepted.PlatformCommit
                    || original["caseId"]?.GetValue<string>()!=caseId[8..]) return null;
            }
            else if(value["requestSha256"]?.GetValue<string>()!=QualificationProtocolSchema.Digest(request) || value["caseId"]?.GetValue<string>()!=caseId) return null;
            observations.Add(new JsonObject { ["caseId"]=caseId,["evidenceSha256"]=Convert.ToHexStringLower(SHA256.HashData(bytes)) });
        }
        JsonObject result;
        if(File.Exists(index))
        {
            result=SecretsProof.Parse(System.Text.Encoding.UTF8.GetString(await Read(index,token).ConfigureAwait(false)))!;
            if(result?["payload"] is not JsonObject old || old["requestSha256"]?.GetValue<string>()!=QualificationProtocolSchema.Digest(request)
                || old["bindingDigest"]?.GetValue<string>()!=accepted.BindingDigest || !JsonNode.DeepEquals(old["observations"],observations)
                || !signer.VerifyData(QualificationProofVerifier.CanonicalBytes(old),Convert.FromBase64String(result["signature"]!.GetValue<string>()),HashAlgorithmName.SHA256,DSASignatureFormat.Rfc3279DerSequence)) return null;
        }
        else
        {
            var payload=new JsonObject { ["requestSha256"]=QualificationProtocolSchema.Digest(request),["bindingDigest"]=accepted.BindingDigest,["observations"]=observations,["completedAt"]=DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
            byte[] signature;lock(signer) signature=signer.SignData(QualificationProofVerifier.CanonicalBytes(payload),HashAlgorithmName.SHA256,DSASignatureFormat.Rfc3279DerSequence);
            result=new JsonObject { ["payload"]=payload,["signature"]=Convert.ToBase64String(signature) };
            await ImmutableFileArchive.PublishAsync(index,QualificationProofVerifier.CanonicalBytes(result),token).ConfigureAwait(false);
        }
        await originals.RetainAsync("joint-phase-command",id,result,token).ConfigureAwait(false);return result;
    }
    private static async Task<byte[]> Read(string path,CancellationToken token)
    {
        var info=new FileInfo(path);if(!info.Exists || info.LinkTarget is not null || info.Length is <=0 or >1024*1024) throw new IOException("ExactBoundedImmutablePhaseEvidence");
        byte[] bytes=await File.ReadAllBytesAsync(path,token).ConfigureAwait(false);CustodyBindingContract.RejectDuplicateProperties(bytes);return bytes;
    }
}
