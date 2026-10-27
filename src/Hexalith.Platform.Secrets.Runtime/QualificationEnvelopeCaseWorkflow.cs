using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Shipped closed envelope substitution and replay workflows using the actual authenticated runtime and private registrar. No expected result, test key or synthetic clock is accepted.</summary>
public sealed class QualificationEnvelopeCaseWorkflow(TrustedEnvelopeAuthenticator envelopes, TrustedEnvelopeReplayVerifier replay,
    IndependentDaprAuthority originals,IReadOnlyDictionary<string,TrustedEnvelopeIdentity> enrolledIdentities,ITrustedEnvelopeReplayRegistrar? lookupRegistrar=null,IQualificationOriginalStore? evaluationOriginals=null,IReadOnlyDictionary<string,QualificationReplayChallengeInput>? replayChallenges=null,IReadOnlyDictionary<string,TrustedEnvelopeAuthenticator>? foreignAuthenticators=null)
{
    private readonly IQualificationOriginalStore _evaluations=evaluationOriginals??new InstalledQualificationOriginalStore(originals);
    /// <summary>Admits one actual issuance/evaluation, retaining the complete evaluation body before return. An admitted missing result stays unknown.</summary>
    public async Task<JsonObject?> ExecuteAsync(JsonObject request,CancellationToken token)
    {
        string? id=EvaluationId(request);if(id is null) return null;
        if(await LookupAsync(request,token).ConfigureAwait(false) is { } retained) return retained;
        if((await PreflightAsync(request,token).ConfigureAwait(false)).Count!=0) return null;
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
    /// <summary>Checks all authenticated challenge prerequisites before any replay admission; no missing input is a passing negative.</summary>
    public async Task<IReadOnlyList<string>> PreflightAsync(JsonObject request,CancellationToken token)
    {
        string? id=SecretsProof.Text(request,"caseId");if(id is null || !id.StartsWith("S2.replay-reject.",StringComparison.Ordinal)) return [];
        if(replayChallenges is null || !replayChallenges.TryGetValue(id,out var input) || lookupRegistrar is null) return ["External.AuthenticNonceBoundReplayChallengeAndPrivateRegistrar."+id];
        if(!enrolledIdentities.TryGetValue(id,out var expected)) return ["ExactEnrolledReplayChallengeIdentity."+id];
        var changedVerifier=id.EndsWith("foreign-issuer",StringComparison.Ordinal)?foreignAuthenticators?.GetValueOrDefault(id):envelopes;
        if(changedVerifier is null) return ["External.CurrentForeignIssuerRuntimeConfiguration."+id];
        if(input.OriginalDelivery is { } baseline && input.ChangedDelivery is { } changed)
        {
            if(!SameIdentity(baseline.Identity,expected) || !IntendedChallenge(id,input)) return ["ExactSemanticallyChangedReplayChallenge."+id];
            var first=await envelopes.VerifyAsync(baseline,expected,token).ConfigureAwait(false);var second=await changedVerifier.VerifyAsync(changed,changed.Identity,token).ConfigureAwait(false);
            return first.Status==CustodyStatus.Succeeded && second.Status==CustodyStatus.Succeeded?[]:["CurrentCryptographicallyValidReplayChallenge."+id];
        }
        if(input.OriginalDelivery is not null || input.ChangedDelivery is not null || input.ChangedIdentity is not { } changedIdentity || !IntendedIdentities(id,expected,changedIdentity))
            return ["External.ExactCurrentScopedReplayChallengeIssuanceRecipe."+id];
        return await envelopes.CanIssueChallengeAsync(expected,token).ConfigureAwait(false) && await changedVerifier.CanIssueChallengeAsync(changedIdentity,token).ConfigureAwait(false)
            ?[]:["External.CurrentScopedReplayChallengeSigningAuthority."+id];
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
        if(caseId.StartsWith("S2.replay-reject.",StringComparison.Ordinal)) return lookupOnly?(await PreflightAsync(request,token).ConfigureAwait(false)).Count==0?await LookupAsync(request,token).ConfigureAwait(false):null:await RunChallengeAsync(request,token).ConfigureAwait(false);
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
            var issued=await envelopes.IssueAsync(identity,TimeSpan.FromSeconds(caseId is "S2.substitution.issued-at" or "S2.substitution.expires-at"?298:300),token).ConfigureAwait(false);
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
            "principal"=>identity with { Principal=DifferentPrincipal(identity.Principal,identity.TargetTenantId) },
            "actor"=>identity with { Principal=identity.Principal with { HumanActorId=(identity.Principal.HumanActorId??"actor")+"-substituted" } },
            "binding"=>identity with { Principal=identity.Principal with { BindingVersion=(identity.Principal.BindingVersion??0)+1 } },
            "roles"=>identity with { Principal=identity.Principal with { RoleBasis=(identity.Principal.RoleBasis??"roles")+"-substituted" } },
            "target"=>identity with { TargetTenantId=identity.TargetTenantId+"-substituted" },
            "schema"=>identity with { SchemaVersion=identity.SchemaVersion+1 },
            "logical-id"=>identity with { LogicalCommandId=identity.LogicalCommandId+"-substituted" },
            "tuple"=>identity with { IdempotencyTuple=identity.IdempotencyTuple.Append("substituted").ToArray() },
            "fingerprint"=>identity with { PayloadFingerprint=identity.PayloadFingerprint==new string('0',64)?new string('1',64):new string('0',64) },
            "digest-key-version"=>identity with { DigestKeyVersion=identity.DigestKeyVersion+"-substituted" },
            _=>identity };
        if(field=="principal" && (expected.Principal.Kind==identity.Principal.Kind || !expected.Principal.IsValid(identity.TargetTenantId)) || field=="fingerprint" && expected.PayloadFingerprint==identity.PayloadFingerprint) return null;
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
            ["expectedIdentity"]=JsonSerializer.SerializeToNode(expected,CustodyBindingContract.Json),["suppliedEnvelope"]=JsonSerializer.SerializeToNode(supplied,CustodyBindingContract.Json),
            ["firstStatus"]=first.Status.ToString(),["exactRetryStatus"]=exact.Status.ToString(),["originalAuthenticationStatus"]=(await envelopes.VerifyAsync(envelope,identity,token).ConfigureAwait(false)).Status.ToString(),["replayOriginal"]=JsonSerializer.SerializeToNode(replayOriginal,CustodyBindingContract.Json) };
    }
    private static bool SameIdentity(TrustedEnvelopeIdentity a,TrustedEnvelopeIdentity b)=>TrustedEnvelopeCodec.Identity(a).AsSpan().SequenceEqual(TrustedEnvelopeCodec.Identity(b));
    private static bool IntendedChallenge(string id,QualificationReplayChallengeInput input)
        =>input.OriginalDelivery is { } a && input.ChangedDelivery is { } b && a.DeliveryNonce==b.DeliveryNonce && a!=b && IntendedIdentities(id,a.Identity,b.Identity);
    private static bool IntendedIdentities(string id,TrustedEnvelopeIdentity a,TrustedEnvelopeIdentity b)
    {
        if(id.EndsWith("foreign-issuer",StringComparison.Ordinal)) return a.Issuer!=b.Issuer && SameIdentity(a with { Issuer=b.Issuer,ProfileVersion=b.ProfileVersion,Audience=b.Audience },b);
        if(a.Issuer!=b.Issuer) return false;
        if(id.EndsWith("cross-tenant",StringComparison.Ordinal)) return a.TargetTenantId!=b.TargetTenantId && b.Principal.IsValid(b.TargetTenantId)
            && SameIdentity(a with { TargetTenantId=b.TargetTenantId,Principal=a.Principal.Kind is TrustedPrincipalKind.User or TrustedPrincipalKind.Administrator?a.Principal with { ActorTenantId=b.TargetTenantId }:a.Principal },b);
        return a.PayloadFingerprint!=b.PayloadFingerprint && SameIdentity(a with { PayloadFingerprint=b.PayloadFingerprint },b);
    }
    private static TrustedEnvelopeReplayIntent Intent(TrustedEnvelope envelope)
    {
        byte[] canonical=TrustedEnvelopeCodec.Delivery(envelope),tag=Convert.FromHexString(envelope.Tag);
        try { return new(envelope.Identity.Issuer,envelope.DeliveryNonce,envelope.Identity.TargetTenantId,envelope.Identity.LogicalCommandId,envelope.SigningKeyVersion,Convert.ToHexString(System.Security.Cryptography.SHA256.HashData([..canonical,..tag]))); }
        finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(canonical);System.Security.Cryptography.CryptographicOperations.ZeroMemory(tag); }
    }
    private async Task<JsonObject?> RunChallengeAsync(JsonObject request,CancellationToken token)
    {
        if((await PreflightAsync(request,token).ConfigureAwait(false)).Count!=0) return null;
        string id=request["caseId"]!.GetValue<string>(),requestId=request["requestId"]!.GetValue<string>();var input=replayChallenges![id];
        if(input.OriginalDelivery is null && input.ChangedDelivery is null && input.ChangedIdentity is { } changedIdentity)
        {
            string issuanceId=requestId+":replay-challenge-issuance-v2";
            var issuanceInput=new JsonObject { ["request"]=request.DeepClone(),["recipe"]=JsonSerializer.SerializeToNode(input,CustodyBindingContract.Json),["originalIdentity"]=JsonSerializer.SerializeToNode(enrolledIdentities[id],CustodyBindingContract.Json) };
            if(!await _evaluations.BeginAsync("executor",issuanceId,issuanceInput,token).ConfigureAwait(false)) return null; // Never reissue an admitted unknown signing result.
            var baseline=await envelopes.IssueAsync(enrolledIdentities[id],TimeSpan.FromSeconds(300),token).ConfigureAwait(false);if(baseline.Envelope is null) return null;
            var changedVerifier=id.EndsWith("foreign-issuer",StringComparison.Ordinal)?foreignAuthenticators![id]:envelopes;
            var changed=await changedVerifier.IssueNonceBoundChallengeAsync(changedIdentity,TimeSpan.FromSeconds(300),baseline.Envelope.DeliveryNonce,token).ConfigureAwait(false);if(changed.Envelope is null) return null;
            input=input with { OriginalDelivery=baseline.Envelope,ChangedDelivery=changed.Envelope };
            await _evaluations.RetainAsync("executor",issuanceId,(JsonObject)JsonSerializer.SerializeToNode(input,CustodyBindingContract.Json)!,token).ConfigureAwait(false);
        }
        if(input.OriginalDelivery is not { } originalDelivery || input.ChangedDelivery is not { } changedDelivery || !IntendedChallenge(id,input)) return null;
        var authentication=id.EndsWith("foreign-issuer",StringComparison.Ordinal)?foreignAuthenticators![id]:envelopes;
        if((await envelopes.VerifyAsync(originalDelivery,originalDelivery.Identity,token).ConfigureAwait(false)).Status!=CustodyStatus.Succeeded
            || (await authentication.VerifyAsync(changedDelivery,changedDelivery.Identity,token).ConfigureAwait(false)).Status!=CustodyStatus.Succeeded) return null;
        string retainedInputId=requestId+":authenticated-replay-challenge-v2";
        var exactInput=new JsonObject { ["request"]=request.DeepClone(),["deliveries"]=JsonSerializer.SerializeToNode(input,CustodyBindingContract.Json) };
        if(!await _evaluations.BeginAsync("executor",retainedInputId,exactInput,token).ConfigureAwait(false)) return null; // Unknown first-seen/challenge acknowledgements are lookup-only.
        var seed=await replay.VerifyAsync(originalDelivery,originalDelivery.Identity,token).ConfigureAwait(false);
        if(seed.Status!=CustodyStatus.Succeeded) return null;
        var firstSeen=await lookupRegistrar!.LookupAsync(Intent(originalDelivery),token).ConfigureAwait(false);
        if(firstSeen?.Intent!=Intent(originalDelivery) || firstSeen.SourceRevision!=1) return null;
        await _evaluations.RetainAsync("executor",retainedInputId,new JsonObject { ["firstSeen"]=JsonSerializer.SerializeToNode(firstSeen,CustodyBindingContract.Json),["deliveries"]=JsonSerializer.SerializeToNode(input,CustodyBindingContract.Json) },token).ConfigureAwait(false);
        bool foreign=id.EndsWith("foreign-issuer",StringComparison.Ordinal);
        // Foreign issuers have a distinct replay key. Challenge the actual receiving profile/issuer boundary instead of inventing a collision across issuers.
        var rejection=await replay.VerifyAsync(changedDelivery,foreign?originalDelivery.Identity:changedDelivery.Identity,token).ConfigureAwait(false);
        if(rejection.Status is CustodyStatus.Unavailable) return null;
        var retained=await lookupRegistrar.LookupAsync(Intent(originalDelivery),token).ConfigureAwait(false);
        if(retained!=firstSeen) return null;
        var retry=await replay.VerifyAsync(changedDelivery,foreign?originalDelivery.Identity:changedDelivery.Identity,token).ConfigureAwait(false);
        if(retry.Status is CustodyStatus.Unavailable) return null;
        var afterRetry=await lookupRegistrar.LookupAsync(Intent(originalDelivery),token).ConfigureAwait(false);
        if(afterRetry!=firstSeen) return null;
        return new JsonObject { ["caseId"]=id,["requestId"]=requestId,["challengeVersion"]=2,["originalAdmissionStatus"]=seed.Status.ToString(),["changedAuthenticationStatus"]="Succeeded",
            ["firstStatus"]=rejection.Status.ToString(),["exactRetryStatus"]=retry.Status.ToString(),["replayOriginal"]=JsonSerializer.SerializeToNode(firstSeen,CustodyBindingContract.Json),
            ["originalDelivery"]=JsonSerializer.SerializeToNode(originalDelivery,CustodyBindingContract.Json),["changedDelivery"]=JsonSerializer.SerializeToNode(changedDelivery,CustodyBindingContract.Json) };
    }
    private static TrustedPrincipal DifferentPrincipal(TrustedPrincipal original,string tenant)=>original.Kind switch {
        TrustedPrincipalKind.User=>original with { Kind=TrustedPrincipalKind.Administrator,PartyId=null,BindingVersion=null },
        TrustedPrincipalKind.Administrator=>original with { Kind=TrustedPrincipalKind.User,PartyId="qualification-substituted-party",BindingVersion=1 },
        TrustedPrincipalKind.Platform=>original with { Kind=TrustedPrincipalKind.Administrator,ActorTenantId=tenant },
        _=>new(TrustedPrincipalKind.Administrator,tenant,"qualification-substituted-actor",null,null,"qualification-substituted-role",null,null,null,null) };
    private sealed record Admission(bool Exact,bool First);
}
