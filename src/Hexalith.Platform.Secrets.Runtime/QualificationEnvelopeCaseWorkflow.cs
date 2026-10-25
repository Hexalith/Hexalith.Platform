using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Shipped closed envelope substitution and replay workflows using the actual authenticated runtime and private registrar. No expected result, test key or synthetic clock is accepted.</summary>
public sealed class QualificationEnvelopeCaseWorkflow(TrustedEnvelopeAuthenticator envelopes, TrustedEnvelopeReplayVerifier replay,
    IndependentDaprAuthority originals,IReadOnlyDictionary<string,TrustedEnvelopeIdentity> enrolledIdentities,ITrustedEnvelopeReplayRegistrar? lookupRegistrar=null,IQualificationOriginalStore? evaluationOriginals=null)
{
    private readonly IQualificationOriginalStore _evaluations=evaluationOriginals??new InstalledQualificationOriginalStore(originals);
    /// <summary>Admits one actual issuance/evaluation, retaining the complete evaluation body before return. An admitted missing result stays unknown.</summary>
    public async Task<JsonObject?> ExecuteAsync(JsonObject request,CancellationToken token)
    {
        string? id=EvaluationId(request);if(id is null) return null;
        if(!await _evaluations.BeginAsync("executor",id,request,token).ConfigureAwait(false)) return await LookupAsync(request,token).ConfigureAwait(false);
        var result=await RunAsync(request,false,token).ConfigureAwait(false);if(result is null) return null;
        await _evaluations.RetainAsync("executor",id,result,token).ConfigureAwait(false);return (JsonObject)result.DeepClone();
    }
    /// <summary>Recovers only the immutable evaluation original. It never issues, registers replay or calls the clock-dependent verifier again.</summary>
    public async Task<JsonObject?> LookupAsync(JsonObject request,CancellationToken token)
    {
        string? id=EvaluationId(request);
        if(id is null || await _evaluations.ReadAdmissionAsync("executor",id,request,token).ConfigureAwait(false)!=QualificationOriginalAdmissionState.Admitted) return null;
        var result=await _evaluations.LookupResultAsync("executor",id,token).ConfigureAwait(false);
        return result is not null && JsonNode.DeepEquals(result["caseId"],request["caseId"]) && JsonNode.DeepEquals(result["requestId"],request["requestId"])?(JsonObject)result.DeepClone():null;
    }
    private string? EvaluationId(JsonObject request)
    {
        string? id=SecretsProof.Text(request,"caseId"),requestId=SecretsProof.Text(request,"requestId");
        return id is not null && requestId is not null && enrolledIdentities.TryGetValue(id,out var identity)
            && request["enrolledScope"] is JsonObject scope && SecretsProof.Text(scope,"tenantId")==identity.TargetTenantId
            && (id is "S2.canonical-hmac" or "S2.byte-identical-replay" or "S2.replay-reject.changed-fields" or "S2.replay-reject.cross-tenant" or "S2.replay-reject.foreign-issuer" || id.StartsWith("S2.substitution.",StringComparison.Ordinal))
            ?requestId+":envelope-evaluation-result":null;
    }
    /// <summary>Independently verifies retained issued bytes at the actual current time and current authority. This is not historical outcome lookup; missing historical native MAC evidence remains an observation blocker.</summary>
    public Task<JsonObject?> ReadVerifyAsync(JsonObject request,CancellationToken token)=>RunAsync(request,true,token);
    private async Task<JsonObject?> RunAsync(JsonObject request,bool lookupOnly,CancellationToken token)
    {
        string? caseId=SecretsProof.Text(request,"caseId"),requestId=SecretsProof.Text(request,"requestId");
        if(caseId is null || requestId is null || !enrolledIdentities.TryGetValue(caseId,out var identity)
            || request["enrolledScope"] is not JsonObject scope || SecretsProof.Text(scope,"tenantId")!=identity.TargetTenantId) return null;
        if(caseId is not ("S2.canonical-hmac" or "S2.byte-identical-replay") && !caseId.StartsWith("S2.substitution.",StringComparison.Ordinal)
            && caseId is not ("S2.replay-reject.changed-fields" or "S2.replay-reject.cross-tenant" or "S2.replay-reject.foreign-issuer")) return null;
        string originalId=requestId+":envelope";
        var original=await originals.InvokeAsync<IndependentOriginal>("qualification-lookup-original",new { role="executor",requestId=originalId },token).ConfigureAwait(false);
        // A legacy issuance without an evaluation original may already have been evaluated before loss.
        // Only this invocation's fresh issuance permits its first evaluation; never recreate a missing outcome.
        if(!lookupOnly && original is not null) return null;
        var input=JsonSerializer.SerializeToElement(new { requestId,caseId,identity },CustodyBindingContract.Json);
        if(original is null)
        {
            if(lookupOnly) return null;
            var admitted=await originals.InvokeAsync<Admission>("qualification-begin-original",new { role="executor",requestId=originalId,original=input },token).ConfigureAwait(false);
            if(admitted is not { Exact:true,First:true }) return null;
            var issued=await envelopes.IssueAsync(identity,TimeSpan.FromSeconds(300),token).ConfigureAwait(false);
            if(await originals.InvokeAsync<Admission>("qualification-retain-result",new { role="executor",requestId=originalId,result=issued },token).ConfigureAwait(false) is not { Exact:true }) return null;
            original=await originals.InvokeAsync<IndependentOriginal>("qualification-lookup-original",new { role="executor",requestId=originalId },token).ConfigureAwait(false);
        }
        if(original is null || original.Request.GetRawText()!=input.GetRawText() || original.Result is not { } result) return null;
        var issuance=result.Deserialize<TrustedEnvelopeResult>(CustodyBindingContract.Json)!;
        if(issuance.Envelope is not { } envelope) return new JsonObject { ["caseId"]=caseId,["requestId"]=requestId,["issueStatus"]=issuance.Status.ToString() };
        var expected=identity; var supplied=envelope;
        string field=caseId.StartsWith("S2.substitution.",StringComparison.Ordinal) ? caseId["S2.substitution.".Length..] :
            caseId=="S2.replay-reject.cross-tenant" ? "target" : caseId=="S2.replay-reject.foreign-issuer" ? "issuer" : caseId=="S2.replay-reject.changed-fields" ? "fingerprint" : string.Empty;
        expected=field switch {
            "issuer"=>identity with { Issuer=identity.Issuer+"-substituted" },
            "audience"=>identity with { Audience=identity.Audience+"-substituted" },
            "operation"=>identity with { OperationFamily=identity.OperationFamily+"-substituted" },
            "principal"=>identity with { Principal=identity.Principal with { Kind=TrustedPrincipalKind.Workflow } },
            "actor"=>identity with { Principal=identity.Principal with { HumanActorId=(identity.Principal.HumanActorId??"actor")+"-substituted" } },
            "binding"=>identity with { Principal=identity.Principal with { BindingVersion=(identity.Principal.BindingVersion??0)+1 } },
            "roles"=>identity with { Principal=identity.Principal with { RoleBasis=(identity.Principal.RoleBasis??"roles")+"-substituted" } },
            "target"=>identity with { TargetTenantId=identity.TargetTenantId+"-substituted" },
            "schema"=>identity with { SchemaVersion=identity.SchemaVersion+1 },
            "logical-id"=>identity with { LogicalCommandId=identity.LogicalCommandId+"-substituted" },
            "tuple"=>identity with { IdempotencyTuple=identity.IdempotencyTuple.Append("substituted").ToArray() },
            "fingerprint"=>identity with { PayloadFingerprint=new string('0',64) },
            "digest-key-version"=>identity with { DigestKeyVersion=identity.DigestKeyVersion+"-substituted" },
            _=>identity };
        supplied=field switch {
            "nonce"=>envelope with { DeliveryNonce=envelope.DeliveryNonce+"-substituted" },
            "issued-at"=>envelope with { IssuedAt=envelope.IssuedAt.AddSeconds(-1) },
            "expires-at"=>envelope with { ExpiresAt=envelope.ExpiresAt.AddSeconds(1) },
            "key-version"=>envelope with { SigningKeyVersion=envelope.SigningKeyVersion+"-substituted" },
            _=>envelope };
        bool usesReplay=caseId.Contains("replay",StringComparison.Ordinal);
        var first=usesReplay && !lookupOnly ? await replay.VerifyAsync(supplied,expected,token).ConfigureAwait(false) : await envelopes.VerifyAsync(supplied,expected,token).ConfigureAwait(false);
        var exact=usesReplay && !lookupOnly ? await replay.VerifyAsync(supplied,expected,token).ConfigureAwait(false) : await envelopes.VerifyAsync(supplied,expected,token).ConfigureAwait(false);
        TrustedEnvelopeReplayReceipt? replayOriginal=null;
        if(usesReplay && first.Status==CustodyStatus.Succeeded)
        {
            if(lookupRegistrar is null) return null;
            byte[] canonical=TrustedEnvelopeCodec.Delivery(supplied),tag=Convert.FromHexString(supplied.Tag),complete=new byte[canonical.Length+tag.Length];
            try
            {
                canonical.CopyTo(complete,0);tag.CopyTo(complete,canonical.Length);
                var intent=new TrustedEnvelopeReplayIntent(supplied.Identity.Issuer,supplied.DeliveryNonce,supplied.Identity.TargetTenantId,supplied.Identity.LogicalCommandId,supplied.SigningKeyVersion,Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(complete)));
                replayOriginal=await lookupRegistrar.LookupAsync(intent,token).ConfigureAwait(false);
                if(replayOriginal?.Intent!=intent || replayOriginal.SourceRevision!=1) return null;
            }
            finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(canonical);System.Security.Cryptography.CryptographicOperations.ZeroMemory(tag);System.Security.Cryptography.CryptographicOperations.ZeroMemory(complete); }
        }
        return new JsonObject { ["caseId"]=caseId,["requestId"]=requestId,["originalEnvelopeSha256"]=CustodyBindingContract.Hash(JsonSerializer.SerializeToUtf8Bytes(envelope)),
            ["firstStatus"]=first.Status.ToString(),["exactRetryStatus"]=exact.Status.ToString(),["replayOriginal"]=JsonSerializer.SerializeToNode(replayOriginal,CustodyBindingContract.Json) };
    }
    private sealed record Admission(bool Exact,bool First);
}
