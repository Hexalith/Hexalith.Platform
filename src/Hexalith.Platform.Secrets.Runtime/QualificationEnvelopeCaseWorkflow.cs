using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Shipped closed envelope substitution and replay workflows using the actual authenticated runtime and private registrar. No expected result, test key or synthetic clock is accepted.</summary>
public sealed class QualificationEnvelopeCaseWorkflow(TrustedEnvelopeAuthenticator envelopes, TrustedEnvelopeReplayVerifier replay,
    IndependentDaprAuthority originals,IReadOnlyDictionary<string,TrustedEnvelopeIdentity> enrolledIdentities)
{
    /// <summary>Retains one original issuance before verification; after ambiguous issuance only exact signed-original lookup is allowed.</summary>
    public Task<JsonObject?> ExecuteAsync(JsonObject request,CancellationToken token)=>RunAsync(request,false,token);
    /// <summary>Independently verifies the exact retained issued bytes using current runtime authority. It never issues a missing original.</summary>
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
        var first=usesReplay ? await replay.VerifyAsync(supplied,expected,token).ConfigureAwait(false) : await envelopes.VerifyAsync(supplied,expected,token).ConfigureAwait(false);
        var exact=usesReplay ? await replay.VerifyAsync(supplied,expected,token).ConfigureAwait(false) : await envelopes.VerifyAsync(supplied,expected,token).ConfigureAwait(false);
        return new JsonObject { ["caseId"]=caseId,["requestId"]=requestId,["originalEnvelopeSha256"]=CustodyBindingContract.Hash(JsonSerializer.SerializeToUtf8Bytes(envelope)),
            ["firstStatus"]=first.Status.ToString(),["exactRetryStatus"]=exact.Status.ToString() };
    }
    private sealed record Admission(bool Exact,bool First);
}
