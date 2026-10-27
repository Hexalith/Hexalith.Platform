using System.Text.Json.Nodes;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Concrete three-role phase adapter using existing independently authenticated qualification providers and verifier. Unknown executor effects stay exact lookup only.</summary>
public sealed class JointSecretsCaseExecutor(JointPhaseCommandScope accepted,IQualificationExecutorProvider executor,IQualificationObserverProvider observer,
    IQualificationEnrollmentProvider enrollment,IQualificationPublicProofVerifier verifier,IQualificationOriginalStore originals,QualificationCallerContext caller,
    IReadOnlyDictionary<string,JsonObject> enrolledOriginals):IJointSecretsCaseExecutor
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> PreflightAsync(string caseId,JsonObject phaseRequest,CancellationToken token)
    {
        if(!enrolledOriginals.TryGetValue(caseId,out var recipe) || JointPhaseRequestRecipe.Bind(recipe,phaseRequest,accepted) is not { } request || !QualificationProtocolSchema.ValidRequest("executor","execute",request)
            || request["runId"]?.GetValue<string>()!=phaseRequest["attemptId"]?.GetValue<string>() || request["targetCommit"]?.GetValue<string>()!=accepted.PlatformCommit) return ["ExactSecretsPhaseOriginal"];
        var admission=new JsonObject();foreach(string name in new[]{"protocol","runId","targetCommit","isolatedTargetId","planSha256"}) admission[name]=request[name]!.DeepClone();
        admission["operation"]="enroll";admission["requestId"]=QualificationProtocolSchema.Digest(admission);
        if(!await executor.AuthorizeAsync(request,caller,token).ConfigureAwait(false) || !await observer.AuthorizeAsync(request,caller,token).ConfigureAwait(false)
            || !await enrollment.AuthorizeAsync(admission,caller,token).ConfigureAwait(false)) return ["CurrentAuthenticatedSecretsRoleScopes"];
        var evidence=await enrollment.InvokeAsync("enroll",admission,token).ConfigureAwait(false);
        return evidence is not null && Original("enrollment",admission,evidence,token)?[]:["CompleteSecretsSourceMetricReaderAndRecoveryReadiness"];
    }
    /// <inheritdoc />
    public async Task<JsonObject?> ExecuteAsync(string caseId,JsonObject phaseRequest,CancellationToken token)
    {
        if(!caseId.StartsWith("secrets:",StringComparison.Ordinal) || !enrolledOriginals.TryGetValue(caseId,out var recipe) || JointPhaseRequestRecipe.Bind(recipe,phaseRequest,accepted) is not { } full || full["caseId"]?.GetValue<string>()!=caseId[8..]
            || full["runId"]?.GetValue<string>()!=phaseRequest["attemptId"]?.GetValue<string>() || full["targetCommit"]?.GetValue<string>()!=accepted.PlatformCommit
            || phaseRequest["bindingDigest"]?.GetValue<string>()!=accepted.BindingDigest || !QualificationProtocolSchema.ValidRequest("executor","execute",full)) return null;
        var original=(JsonObject)full.DeepClone();string id=full["requestId"]!.GetValue<string>();
        var admission=new JsonObject();foreach(string name in new[]{"protocol","runId","targetCommit","isolatedTargetId","planSha256"}) admission[name]=original[name]!.DeepClone();
        admission["operation"]="enroll";admission["requestId"]=QualificationProtocolSchema.Digest(admission);
        if(!await enrollment.AuthorizeAsync(admission,caller,token).ConfigureAwait(false)) return null;
        var enrolled=await enrollment.InvokeAsync("enroll",admission,token).ConfigureAwait(false);
        if(enrolled is null || !Original("enrollment",admission,enrolled,token) || !JsonNode.DeepEquals(enrolled["payload"]?["caseScopes"]?[caseId[8..]],original["enrolledScope"])
            || !JsonNode.DeepEquals(enrolled["payload"]?["caseTargetIds"]?[caseId[8..]],original["caseTargetId"])) return null;
        if(!await executor.AuthorizeAsync(full,caller,token).ConfigureAwait(false)) return null;
        bool first=await originals.BeginAsync("joint-phase-secret",id,original,token).ConfigureAwait(false);
        var execution=await executor.InvokeAsync(first?"execute":"lookup",original,token).ConfigureAwait(false);
        if(execution is null || !Original("executor",full,execution,token) || !await observer.AuthorizeAsync(full,caller,token).ConfigureAwait(false)) return null;
        var observation=await observer.InvokeAsync("observe",original,token).ConfigureAwait(false);
        if(observation is null || !Original("observer",full,observation,token)) return null;
        return new JsonObject { ["request"]=full.DeepClone(),["execution"]=execution,["observation"]=observation,["enrollment"]=enrolled,["cohortRequestId"]=phaseRequest["requestId"]!.DeepClone() };
    }
    private bool Original(string role,JsonObject request,JsonObject evidence,CancellationToken token)
        =>evidence["payload"]?["observedAt"] is JsonValue at && at.TryGetValue<long>(out long time) && verifier.VerifyOriginal(role,request,evidence,time,token);
}
