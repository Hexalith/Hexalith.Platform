using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Closed private replay ACL/retention/recovery sequencing using actual registrar append and exact source lookup. It never grants a target command.</summary>
public sealed class QualificationReplayCaseWorkflow(ITrustedEnvelopeReplayRegistrar registrar,IQualificationOriginalStore originals,
    IReadOnlyDictionary<string,QualificationReplayCaseInput> inputs,TimeProvider clock)
{
    /// <summary>Registers the concrete closed workflows and their distinct read-only recovery behavior.</summary>
    public IReadOnlyList<QualificationSourceWorkflow> Registrations()=>inputs.Keys.Where(Supported).Select(id=>new QualificationSourceWorkflow(id,ExecuteAsync,LookupAsync,PreflightAsync)).ToArray();
    /// <summary>Requires exact enrollment and current first evaluation for a new append. No production time substitution exists.</summary>
    public Task<IReadOnlyList<string>> PreflightAsync(JsonObject request,CancellationToken token) { token.ThrowIfCancellationRequested();return Task.FromResult<IReadOnlyList<string>>(Input(request) is null?["ExactEnrolledReplayOriginal"]:[]); }
    /// <summary>Retains the original before one restricted append; a loss uses only lookup and cannot renew nonce/time.</summary>
    public async Task<JsonObject?> ExecuteAsync(JsonObject request,CancellationToken token)
    {
        var input=Input(request);if(input is null) return null;string requestId=request["requestId"]!.GetValue<string>();
        bool first=await originals.BeginAsync("executor",requestId+":case-result",request,token).ConfigureAwait(false);
        if(!first) return await LookupAsync(request,token).ConfigureAwait(false);
        if(input.FirstSeenAt>clock.GetUtcNow() || input.FirstSeenAt<clock.GetUtcNow().AddSeconds(-30)) return null;
        _=await registrar.RegisterAsync(input.Intent,input.FirstSeenAt,TimeSpan.FromSeconds(604800),token).ConfigureAwait(false);
        var result=await ReadOriginalAsync(request,token).ConfigureAwait(false);
        if(result is not null) await originals.RetainAsync("executor",requestId+":case-result",result,token).ConfigureAwait(false);return result;
    }
    /// <summary>Queries the original issuer/nonce only. Unknown original source remains unknown without a new registration.</summary>
    public async Task<JsonObject?> LookupAsync(JsonObject request,CancellationToken token)
    {
        var result=await ReadOriginalAsync(request,token).ConfigureAwait(false);
        if(result is not null) await originals.RetainAsync("executor",request["requestId"]!.GetValue<string>()+":case-result",result,token).ConfigureAwait(false);return result;
    }
    /// <summary>Independent read only; this cannot publish any executor result or first-seen original.</summary>
    public async Task<JsonObject?> ReadOriginalAsync(JsonObject request,CancellationToken token)
    {
        var input=Input(request);if(input is null) return null;string requestId=request["requestId"]!.GetValue<string>();
        var actual=await registrar.LookupAsync(input.Intent,token).ConfigureAwait(false);
        if(actual?.Intent!=input.Intent || actual.FirstSeenAt!=input.FirstSeenAt || actual.RetainUntil!=input.FirstSeenAt.AddSeconds(604800) || actual.SourceRevision!=1) return null;
        var result=new JsonObject { ["caseId"]=request["caseId"]!.DeepClone(),["requestId"]=requestId,["replayOriginal"]=JsonSerializer.SerializeToNode(actual,CustodyBindingContract.Json) };
        // Lookup invoked independently by an observer never publishes executor state.
        return result;
    }
    private QualificationReplayCaseInput? Input(JsonObject request)
    {
        string? id=SecretsProof.Text(request,"caseId");return id is not null && Supported(id) && inputs.TryGetValue(id,out var input) && request["enrolledScope"] is JsonObject scope
            && QualificationProtocolSchema.Digest(scope)==input.EnrolledScopeSha256 && scope["tenantId"]?.GetValue<string>()==input.Intent.TargetTenantId?input:null;
    }
    /// <summary>Gets the implemented replay source group; selected fault/scheduler controls remain distinct inputs.</summary>
    public static bool Supported(string id)=>id is "S2.replay-exact-stream-acl" or "S2.replay-retention-recovery" || id.StartsWith("Recovery.replay.",StringComparison.Ordinal);
}
