using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Retains one exact inventory/digest read result before its caller can receive it. An admitted read whose result was not durably retained stays unknown; recovery never rereads mutable state or recomputes a digest.</summary>
public sealed class QualificationRetainedRuntimeRead(IQualificationOriginalStore originals,QualificationRuntimeSourceDriver source)
{
    /// <summary>Admits one exact source read, then retains its complete original body under an independent inner identity.</summary>
    public async Task<JsonObject?> ExecuteAsync(JsonObject original,CancellationToken token)
    {
        string? id=OriginalId(original);if(id is null) return null;
        if(!await originals.BeginAsync("executor",id,original,token).ConfigureAwait(false)) return await LookupAsync(original,token).ConfigureAwait(false);
        var result=await source.ExecuteAsync(original,token).ConfigureAwait(false);
        if(result is null || !ExactResult(original,result)) return null;
        await originals.RetainAsync("executor",id,result,token).ConfigureAwait(false);
        return (JsonObject)result.DeepClone();
    }
    /// <summary>Reads only the independently retained inner original. A lost or missing immutable result is never replaced by a later current read.</summary>
    public async Task<JsonObject?> LookupAsync(JsonObject original,CancellationToken token)
    {
        string? id=OriginalId(original);
        if(id is null || await originals.ReadAdmissionAsync("executor",id,original,token).ConfigureAwait(false)!=QualificationOriginalAdmissionState.Admitted) return null;
        var result=await originals.LookupResultAsync("executor",id,token).ConfigureAwait(false);
        return result is not null && ExactResult(original,result)?(JsonObject)result.DeepClone():null;
    }
    private static string? OriginalId(JsonObject original)
        =>SecretsProof.Text(original,"caseId") is { } caseId && (caseId.StartsWith("S1.inventory.",StringComparison.Ordinal) || caseId=="S2.retained-digest")
            && SecretsProof.Text(original,"requestId") is { } requestId?requestId+":runtime-read-result":null;
    private static bool ExactResult(JsonObject original,JsonObject result)
        =>JsonNode.DeepEquals(result["caseId"],original["caseId"]) && result["requestSha256"]?.GetValue<string>()==QualificationProtocolSchema.Digest(original);
}
