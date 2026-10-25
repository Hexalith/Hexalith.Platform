using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Atomic original publication at an independently authenticated nonrollback service, never process dictionaries.</summary>
public sealed class InstalledQualificationOriginalStore(IndependentDaprAuthority originals) : IQualificationOriginalStore
{
    /// <inheritdoc />
    public async Task<bool> BeginAsync(string role, string requestId, JsonObject original, CancellationToken cancellationToken)
    {
        var result = await originals.InvokeAsync<OriginalAdmission>("qualification-begin-original", new { role, requestId, original }, cancellationToken).ConfigureAwait(false);
        if (result is null || !result.Exact) throw new InvalidOperationException("Durable original publication is unknown or conflicting.");
        return result.First;
    }
    /// <inheritdoc />
    public async Task RetainAsync(string role, string requestId, JsonObject result, CancellationToken cancellationToken)
    {
        if (await originals.InvokeAsync<OriginalAdmission>("qualification-retain-result", new { role, requestId, result }, cancellationToken).ConfigureAwait(false) is not { Exact: true })
            throw new InvalidOperationException("Durable original result publication is unknown or conflicting.");
    }
    /// <inheritdoc />
    public async Task<JsonObject?> LookupResultAsync(string role,string requestId,CancellationToken cancellationToken)
    {
        var original=await originals.InvokeAsync<IndependentOriginal>("qualification-lookup-original",new { role,requestId },cancellationToken).ConfigureAwait(false);
        return original?.Result is { ValueKind:JsonValueKind.Object } result ? JsonNode.Parse(result.GetRawText()) as JsonObject : null;
    }
    /// <inheritdoc />
    public async Task<JsonObject?> LookupRequestAsync(string role,string requestId,CancellationToken cancellationToken)
    {
        var original=await originals.InvokeAsync<IndependentOriginal>("qualification-lookup-original",new { role,requestId },cancellationToken).ConfigureAwait(false);
        return original?.Request is { ValueKind:JsonValueKind.Object } request?JsonNode.Parse(request.GetRawText()) as JsonObject:null;
    }
    /// <inheritdoc />
    public async Task<QualificationOriginalAdmissionState> ReadAdmissionAsync(string role,string requestId,JsonObject exactOriginal,CancellationToken token)
    {
        var read=await originals.InvokeAsync<JsonElement>("qualification-lookup-original",new { role,requestId },token).ConfigureAwait(false);
        if(read.ValueKind==JsonValueKind.Null) return QualificationOriginalAdmissionState.Absent;
        if(read.ValueKind!=JsonValueKind.Object) return QualificationOriginalAdmissionState.Unknown;
        var original=read.Deserialize<IndependentOriginal>(CustodyBindingContract.Json);
        return original is not null && JsonNode.DeepEquals(JsonNode.Parse(original.Request.GetRawText()),exactOriginal)?QualificationOriginalAdmissionState.Admitted:QualificationOriginalAdmissionState.Unknown;
    }
    private sealed record OriginalAdmission(bool Exact, bool First);
}
