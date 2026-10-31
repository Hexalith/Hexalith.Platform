using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexalith.Platform.Protection;

/// <summary>Reads and writes one string-keyed sorted dictionary with <see cref="StringComparer.Ordinal"/>.</summary>
internal sealed class OrdinalStringSortedDictionaryConverter<TValue> : JsonConverter<SortedDictionary<string, TValue>>
{
    public override SortedDictionary<string, TValue> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject) { throw new JsonException("ERC head dictionary required."); }
        var result = new SortedDictionary<string, TValue>(StringComparer.Ordinal);
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) { return result; }
            if (reader.TokenType != JsonTokenType.PropertyName) { throw new JsonException("ERC head dictionary required."); }
            string key = reader.GetString() ?? throw new JsonException("ERC head dictionary required.");
            if (!reader.Read()) { throw new JsonException("ERC head dictionary required."); }
            TValue? value = JsonSerializer.Deserialize<TValue>(ref reader, options);
            result.Add(key, value!);
        }
        throw new JsonException("ERC head dictionary required.");
    }

    public override void Write(Utf8JsonWriter writer, SortedDictionary<string, TValue> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var entry in value)
        {
            writer.WritePropertyName(entry.Key);
            JsonSerializer.Serialize(writer, entry.Value, options);
        }
        writer.WriteEndObject();
    }
}
