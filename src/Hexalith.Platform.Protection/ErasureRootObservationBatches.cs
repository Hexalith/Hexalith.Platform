using System.Text.Json.Nodes;

namespace Hexalith.Platform.Protection;

/// <summary>Exact batch identities and original-batch receipt closure from the authenticated selected native calls.</summary>
internal static class ErasureRootObservationBatches
{
    internal static HashSet<string> FromCalls(IEnumerable<ProtectionQualificationNativeOriginal> calls)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var call in calls) { Visit(call.Call.Payload); Visit(call.Result); }
        return result;
        void Visit(JsonNode? value)
        {
            if (value is JsonObject obj)
            {
                foreach (var member in obj)
                {
                    if (member.Key is "BatchId" or "batchId" or "OriginalBatchId" && member.Value is JsonValue scalar && scalar.TryGetValue<string>(out var id) && !string.IsNullOrWhiteSpace(id))
                    { result.Add(id); }
                    else { Visit(member.Value); }
                }
            }
            else if (value is JsonArray array) { foreach (var item in array) { Visit(item); } }
        }
    }
}
