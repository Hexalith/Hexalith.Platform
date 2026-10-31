using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexalith.Platform.Protection;

/// <summary>Selects ordinal string-key dictionaries so deserialized ERC heads do not follow the process culture.</summary>
internal sealed class OrdinalStringSortedDictionaryConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(SortedDictionary<,>) && typeToConvert.GetGenericArguments()[0] == typeof(string);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        => (JsonConverter)Activator.CreateInstance(typeof(OrdinalStringSortedDictionaryConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()[1]))!;
}
