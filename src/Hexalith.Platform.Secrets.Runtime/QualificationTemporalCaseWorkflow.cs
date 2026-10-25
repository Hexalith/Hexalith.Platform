using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Closed temporal and emergency workflows over the actual authenticator and current key resolver. Exact boundary observations require the selected native evaluator to call at that instant.</summary>
public sealed class QualificationTemporalCaseWorkflow(TrustedEnvelopeAuthenticator authenticator,IPlatformHmacKeyProvider keys,
    IPlatformSigningProfileProvider profiles,TimeProvider clock,IQualificationOriginalStore originals,IReadOnlyDictionary<string,QualificationTemporalCaseInput> inputs)
{
    /// <summary>Gets publicly executable closed registrations.</summary>
    public IReadOnlyList<QualificationSourceWorkflow> Registrations()=>inputs.Keys.Where(Supported).Select(id=>QualificationRuntimeWorkflowCatalog.Retained(id,originals,ExecuteAsync)
        with { Preflight=PreflightAsync }).ToArray();
    /// <summary>Validates real current temporal conditions; supplied clocks and expected outcome flags are not accepted.</summary>
    public Task<IReadOnlyList<string>> PreflightAsync(JsonObject request,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();var input=Input(request);var profile=profiles.GetCurrent();
        if(input is null || profile is null || !profile.IsValid(clock.GetUtcNow())) return Task.FromResult<IReadOnlyList<string>>(["ExactTemporalOriginalAndCurrentProfile"]);
        string id=request["caseId"]!.GetValue<string>();var now=clock.GetUtcNow();var envelope=input.Envelope;
        bool condition=id switch {
            "S2.expiry.before"=>now<envelope.ExpiresAt && now>=envelope.ExpiresAt.AddSeconds(-1),
            "S2.expiry.at"=>now==envelope.ExpiresAt,
            "S2.expiry.after"=>now>envelope.ExpiresAt,
            "S2.future-skew"=>envelope.IssuedAt>now+profile.ClockSkew,
            "S2.maximum-lifetime"=>envelope.ExpiresAt-envelope.IssuedAt>profile.MaximumLifetime,
            _=>true };
        return Task.FromResult<IReadOnlyList<string>>(condition?[]:["External.NativeTemporalEvaluatorAtExactBoundary."+id]);
    }
    /// <summary>Executes only the actual temporal check, or emergency reissuance after independently established current revocation.</summary>
    public async Task<JsonObject?> ExecuteAsync(JsonObject request,CancellationToken token)
    {
        if((await PreflightAsync(request,token).ConfigureAwait(false)).Count!=0) return null;
        return await EvaluateAsync(request,true,token).ConfigureAwait(false);
    }
    /// <summary>Repeats current verification only. Emergency issued bytes are retrieved from their exact durable original, never reissued.</summary>
    public Task<JsonObject?> ObserveAsync(JsonObject request,CancellationToken token)=>EvaluateAsync(request,false,token);
    private QualificationTemporalCaseInput? Input(JsonObject request)
    {
        string? id=SecretsProof.Text(request,"caseId");
        return id is not null && Supported(id) && inputs.TryGetValue(id,out var input) && request["enrolledScope"] is JsonObject scope
            && QualificationProtocolSchema.Digest(scope)==input.EnrolledScopeSha256 && scope["tenantId"]?.GetValue<string>()==input.Identity.TargetTenantId
            && TrustedEnvelopeCodec.Identity(input.Envelope.Identity).AsSpan().SequenceEqual(TrustedEnvelopeCodec.Identity(input.Identity))?input:null;
    }
    private async Task<JsonObject?> EvaluateAsync(JsonObject request,bool execute,CancellationToken token)
    {
        var input=Input(request);var profile=profiles.GetCurrent();if(input is null || profile is null || !profile.IsValid(clock.GetUtcNow())) return null;
        string id=request["caseId"]!.GetValue<string>();string requestId=request["requestId"]!.GetValue<string>();
        var envelope=input.Envelope;var scope=new PlatformHmacScope(input.Identity.TargetTenantId,PlatformHmacPurpose.TrustedEnvelope);
        DateTimeOffset checkedAt=clock.GetUtcNow();CustodyStatus status;PlatformHmacKeyMetadata? metadata=null;TrustedEnvelope? replacement=null;
        if(id.StartsWith("S2.overlap-",StringComparison.Ordinal))
        {
            // L=300 and O=600: a delivery cannot remain live until this key boundary. Measure key eligibility separately, without relaxing either limit.
            var resolution=await PlatformKeyResolution.ReadAsync(keys,scope,envelope.SigningKeyVersion,token).ConfigureAwait(false);using var key=resolution.Key;
            metadata=key?.Metadata;if(metadata?.State!=PlatformHmacKeyState.Retained || metadata.RetiredAt is null) return null;
            var edge=metadata.RetiredAt.Value+profile.RotationOverlap;checkedAt=clock.GetUtcNow();
            if(id=="S2.overlap-before" && !(checkedAt<edge && checkedAt>=edge.AddSeconds(-1)) || id=="S2.overlap-at" && checkedAt!=edge || id=="S2.overlap-after" && checkedAt<=edge) return null;
            status=PlatformKeyResolution.Check(resolution,scope,envelope.SigningKeyVersion,checkedAt,false,profile.RotationOverlap);
        }
        else
        {
            status=(await authenticator.VerifyAsync(envelope,input.Identity,token).ConfigureAwait(false)).Status;
            if(id=="S2.emergency-redispatch")
            {
                if(status!=CustodyStatus.RevokedKey) return null;
                string issuanceId=requestId+":emergency-delivery";
                if(execute && await originals.BeginAsync("executor",issuanceId,request,token).ConfigureAwait(false))
                {
                    var issued=await authenticator.IssueAsync(input.Identity,profile.MaximumLifetime,token).ConfigureAwait(false);
                    if(issued.Envelope is null) return null;
                    await originals.RetainAsync("executor",issuanceId,(JsonObject)JsonSerializer.SerializeToNode(issued,CustodyBindingContract.Json)!,token).ConfigureAwait(false);
                }
                var retained=await originals.LookupResultAsync("executor",issuanceId,token).ConfigureAwait(false);
                replacement=retained?.Deserialize<TrustedEnvelopeResult>(CustodyBindingContract.Json)?.Envelope;if(replacement is null) return null;
                status=(await authenticator.VerifyAsync(replacement,input.Identity,token).ConfigureAwait(false)).Status;
            }
        }
        return new JsonObject { ["caseId"]=id,["requestId"]=requestId,["status"]=status.ToString(),["outcome"]=status==CustodyStatus.Succeeded?"Verified":status==CustodyStatus.Expired?"Expired":"Denied",
            ["checkedAt"]=checkedAt.ToUnixTimeMilliseconds(),["originalEnvelope"]=JsonSerializer.SerializeToNode(envelope,CustodyBindingContract.Json),
            ["replacementEnvelope"]=JsonSerializer.SerializeToNode(replacement,CustodyBindingContract.Json),["keyMetadata"]=JsonSerializer.SerializeToNode(metadata,CustodyBindingContract.Json),
            ["rotationOverlapMilliseconds"]=(long)profile.RotationOverlap.TotalMilliseconds };
    }
    /// <summary>Gets the exact implemented case group.</summary>
    public static bool Supported(string id)=>id.StartsWith("S2.expiry.",StringComparison.Ordinal) || id is "S2.future-skew" or "S2.maximum-lifetime" or "S2.overlap-before" or "S2.overlap-at" or "S2.overlap-after" or "S2.emergency-redispatch";
}
