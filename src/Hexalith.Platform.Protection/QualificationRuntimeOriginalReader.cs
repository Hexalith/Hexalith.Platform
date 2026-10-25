using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Independently current read of the actual durable runtime cryptographic original and exact native replay/denial source streams.</summary>
public sealed class QualificationRuntimeOriginalReader(IndependentDaprAuthority originals,QualificationNativeSourceReader native,
    IReadOnlyDictionary<string,IReadOnlyList<AggregateIdentity>> enrolledStreams) : IQualificationObservedOriginalReader
{
    /// <inheritdoc />
    public async Task<QualificationObservedOriginal?> ReadAsync(JsonObject request,CancellationToken cancellationToken)
    {
        string? requestId=SecretsProof.Text(request,"requestId"),caseId=SecretsProof.Text(request,"caseId");
        if(requestId is null || caseId is null || !enrolledStreams.TryGetValue(caseId,out var streamScope)) return null;
        var original=await originals.InvokeAsync<IndependentOriginal>("qualification-lookup-original",new { role="executor",requestId=requestId+":envelope" },cancellationToken).ConfigureAwait(false);
        if(original?.Result is not { } result || original.ResultRetainedRevision<=0 || original.Request.GetProperty("requestId").GetString()!=requestId
            || original.Request.GetProperty("caseId").GetString()!=caseId) return null;
        var streams=streamScope.Count==0 ? [] : await native.ReadAsync(streamScope,cancellationToken).ConfigureAwait(false);
        if(streams is null) return null;
        var source=(JsonObject)JsonSerializer.SerializeToNode(new { original=result,streams },CustodyBindingContract.Json)!;
        string receipt=Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(result,CustodyBindingContract.Json)));
        return new(requestId,receipt,"runtime-original:"+original.ResultRetainedRevision,source);
    }
}
