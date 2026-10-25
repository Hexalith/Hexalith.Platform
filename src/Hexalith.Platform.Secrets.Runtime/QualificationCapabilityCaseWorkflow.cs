using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Closed canonical capability and substitution workflow using the actual signer and separately published public trust.</summary>
public sealed class QualificationCapabilityCaseWorkflow(IDeletionCapabilitySigningActor signer,IDeletionCapabilitySigningTrustProvider trust,
    IQualificationOriginalStore originals,IReadOnlyDictionary<string,DeletionBatchCapabilityV1> enrolledPayloads)
{
    /// <summary>Uses one deterministic original signing identity and verifies changed public bytes without any destruction, issue, dispatch or approval operation.</summary>
    public Task<JsonObject?> ExecuteAsync(JsonObject request,CancellationToken token)=>RunAsync(request,false,token);
    /// <summary>Re-verifies the actual original signer artifact and independently current public trust. Missing originals cannot sign.</summary>
    public Task<JsonObject?> ReadVerifyAsync(JsonObject request,CancellationToken token)=>RunAsync(request,true,token);
    private async Task<JsonObject?> RunAsync(JsonObject request,bool lookupOnly,CancellationToken token)
    {
        string? id=SecretsProof.Text(request,"caseId"),requestId=SecretsProof.Text(request,"requestId");
        if(id is null || requestId is null || !enrolledPayloads.TryGetValue(id,out var payload) || request["enrolledScope"] is not JsonObject scope
            || scope["tenantId"]?.GetValue<string>()!=payload.TenantId || scope["batchId"]?.GetValue<string>()!=payload.BatchId
            || scope["sealId"]?.GetValue<string>()!=payload.DestructionSealId || scope["manifestDigest"]?.GetValue<string>()!=payload.ManifestDigest
            || id!="S4.capability-canonical-14-fields" && !id.StartsWith("S4.capability-substitution.",StringComparison.Ordinal)) return null;
        string originalId=requestId+":capability";
        var input=(JsonObject)JsonSerializer.SerializeToNode(new { requestId,payload },CustodyBindingContract.Json)!;
        bool first=!lookupOnly && await originals.BeginAsync("executor",originalId,input,token).ConfigureAwait(false);
        var issued=first ? await signer.SignAsync(payload).WaitAsync(token).ConfigureAwait(false) : await signer.LookupAsync(payload).WaitAsync(token).ConfigureAwait(false);
        if(issued.Payload!=payload || issued.State!=DeletionCapabilitySigningState.Signed || issued.DetachedJws is null) return null;
        var published=await trust.ResolveAsync(payload.TenantId,"DeletionBatchCapabilitySigningKey",payload.CapabilityKeyVersion,token).ConfigureAwait(false);
        if(published is null || published.IsRevoked || published.TenantId!=payload.TenantId || published.CapabilityKeyVersion!=payload.CapabilityKeyVersion
            || published.PublicAnchorId!=issued.PublicAnchorId || published.PublicAnchorVersion!=issued.PublicAnchorVersion) return null;
        var profile=new DeletionCapabilityTrustProfile(published.Issuer,published.Audience,published.TenantId,published.CapabilityKeyVersion,published.PublicAnchorId,published.PublicAnchorVersion);
        string field=id.StartsWith("S4.capability-substitution.",StringComparison.Ordinal)?id["S4.capability-substitution.".Length..]:string.Empty;
        var changed=field switch {
            "issuer"=>payload with { Issuer=payload.Issuer+"-substituted" },"audience"=>payload with { Audience=payload.Audience+"-substituted" },
            "tenant"=>payload with { TenantId=payload.TenantId+"-substituted" },"request"=>payload with { DeletionRequestId=payload.DeletionRequestId+"-substituted" },
            "seal"=>payload with { DestructionSealId=payload.DestructionSealId+"-substituted" },"kind"=>payload with { BatchKind=payload.BatchKind+"-substituted" },
            "ordinal"=>payload with { BatchOrdinal=payload.BatchOrdinal+1 },"batch"=>payload with { BatchId=payload.BatchId+"-substituted" },
            "manifest"=>payload with { ManifestDigest=(payload.ManifestDigest[0]=='0'?"1":"0")+payload.ManifestDigest[1..] },
            "guard"=>payload with { GuardStreamId=payload.GuardStreamId+"-substituted" },"intended-revision"=>payload with { IntendedIssuedGuardRevision=payload.IntendedIssuedGuardRevision+1 },
            "attestation"=>payload with { AttestationOrdinal=payload.AttestationOrdinal+1 },"signing-attempt"=>payload with { SigningAttemptOrdinal=payload.SigningAttemptOrdinal+1 },
            "key-version"=>payload with { CapabilityKeyVersion=payload.CapabilityKeyVersion+"-substituted" },_=>payload };
        string artifact=issued.DetachedJws;
        if(field=="header") { int separator=artifact.IndexOf('.'); artifact="eyJhbGciOiJub25lIn0"+artifact[separator..]; }
        using var key=ECDsa.Create(); key.ImportSubjectPublicKeyInfo(published.SubjectPublicKeyInfo,out int read);
        if(read!=published.SubjectPublicKeyInfo.Length) return null;
        bool verified=DeletionBatchCapabilityCodec.Verify(changed,profile,artifact,key);
        byte[] canonical=DeletionBatchCapabilityIdentity.CanonicalPayload(payload);
        using var document=JsonDocument.Parse(canonical);
        var result=new JsonObject { ["caseId"]=id,["requestId"]=requestId,["signingRequestId"]=issued.SigningRequestId,["verified"]=verified,
            ["canonicalFieldCount"]=document.RootElement.EnumerateObject().Count(),["canonicalSha256"]=Convert.ToHexStringLower(SHA256.HashData(canonical)),
            ["detachedArtifactSha256"]=Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(issued.DetachedJws))),
            ["publicAnchorId"]=published.PublicAnchorId,["publicAnchorVersion"]=published.PublicAnchorVersion,["trustRevision"]=published.TrustProfileRevision,
            ["payload"]=JsonSerializer.SerializeToNode(payload,CustodyBindingContract.Json) };
        if(!lookupOnly) await originals.RetainAsync("executor",originalId,result,token).ConfigureAwait(false);
        return result;
    }
}
