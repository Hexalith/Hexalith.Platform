using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Serialization;

namespace Hexalith.EventStore.DomainService;

/// <summary>Complete typed payload fingerprint for owner-approved local intent policies.</summary>
public static class CanonicalIntentPayload
{
    /// <summary>Hashes the entire serialized typed payload, ordering object members recursively and retaining array order, nulls and values.</summary>
    public static string Sha256<T>(T payload)
    {
        JsonElement element = JsonSerializer.SerializeToElement(payload, EventStorePayloadSerialization.Options);
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output)) Write(writer, element);
        return Convert.ToHexStringLower(SHA256.HashData(output.ToArray()));
    }

    private static void Write(Utf8JsonWriter writer, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
            { writer.WritePropertyName(property.Name); Write(writer, property.Value); }
            writer.WriteEndObject();
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in value.EnumerateArray()) Write(writer, item);
            writer.WriteEndArray();
        }
        else value.WriteTo(writer);
    }
}
