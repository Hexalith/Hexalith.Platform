using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Parses one public proof. Duplicate names and secret-bearing property names never become a current proof.</summary>
internal static class SecretsProof
{
    internal const int MaximumBytes = 1024 * 1024;
    private static readonly HashSet<string> SecretNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "secret", "password", "token", "privateKey", "private_key", "apiKey", "api_key",
        "accessToken", "refreshToken", "keyMaterial", "plaintext", "credentialValue", "secretValue",
    };

    /// <summary>Parses a proof object. A duplicate or secret-bearing name returns null and is not cached by the caller.</summary>
    public static JsonObject? Parse(string json)
    {
        if (json.Length > MaximumBytes || Encoding.UTF8.GetByteCount(json) > MaximumBytes) { return null; }
        try
        {
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json), new JsonReaderOptions { CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 32 });
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) { return null; }
            JsonObject value = ReadObject(ref reader, rejectSecretNames: true);
            return reader.Read() ? null : value;
        }
        catch (JsonException) { return null; }
        catch (SecretsRejectedException) { return null; }
    }

    /// <summary>Parses a configuration document. Unknown names, including unmapped secret names, are retained only so the loader can ignore them.</summary>
    public static JsonObject? ParseDocument(string json)
    {
        if (json.Length > MaximumBytes || Encoding.UTF8.GetByteCount(json) > MaximumBytes) { return null; }
        try
        {
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json), new JsonReaderOptions { CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 32 });
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) { return null; }
            JsonObject value = ReadObject(ref reader, rejectSecretNames: false);
            return reader.Read() ? null : value;
        }
        catch (JsonException) { return null; }
        catch (SecretsRejectedException) { return null; }
    }

    public static string? Text(JsonObject? proof, string name)
        => proof?[name] is JsonValue value && value.TryGetValue<string>(out string? text) ? text : null;

    public static long? Integer(JsonObject? proof, string name)
        => proof?[name] is JsonValue value && value.TryGetValue<long>(out long number) ? number : null;

    public static bool? Flag(JsonObject? proof, string name)
        => proof?[name] is JsonValue value && value.TryGetValue<bool>(out bool flag) ? flag : null;

    public static bool SameText(JsonObject? proof, string name, string? expected) => expected is not null && Text(proof, name) == expected;

    public static bool SameInteger(JsonObject? proof, string name, long expected) => Integer(proof, name) == expected;

    public static bool Fresh(JsonObject? proof, DateTimeOffset now)
        => Instant(proof, "observedAt") is { } observed
            && Instant(proof, proof?.ContainsKey("proofValidUntil") == true ? "proofValidUntil" : "validUntil") is { } until
            && observed <= now && now < until && until - observed <= TimeSpan.FromSeconds(30);

    public static bool Echoes(JsonObject? proof, JsonObject request)
        => proof is not null && request.All(field => proof.ContainsKey(field.Key) && JsonNode.DeepEquals(proof[field.Key], field.Value));

    public static bool Authorized(JsonObject? proof, JsonObject request, DateTimeOffset now)
        => Flag(proof, "authorized") is true && Fresh(proof, now)
            && (proof?.ContainsKey("status") != true || Text(proof, "status") is "authorized" or "present" or "recorded" or "absent")
            && SecretsProvisioning.IsReference(Text(proof, "authorityRevision")) && Echoes(proof, request);

    public static bool Absent(JsonObject? proof, JsonObject request, string? authority, string queryMethod, DateTimeOffset now)
        => Authorized(proof, request, now) && SameText(proof, "status", "absent") && SameText(proof, "proofType", "absence")
            && SameText(proof, "authorityReference", authority) && SameText(proof, "queryMethod", queryMethod);

    public static DateTimeOffset? Instant(JsonObject? proof, string name)
    {
        string? text = Text(proof, name);
        if (text is null || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset instant)) { return null; }
        return instant.Offset == TimeSpan.Zero ? instant : null;
    }

    public static byte[]? SubjectPublicKey(JsonObject? proof)
    {
        string? encoded = Text(proof, "subjectPublicKeyInfo");
        if (string.IsNullOrEmpty(encoded)) { return null; }
        try
        {
            byte[] decoded = Convert.FromBase64String(encoded);
            return decoded.Length is > 0 and <= 512 ? decoded : null;
        }
        catch (FormatException) { return null; }
    }

    public static bool TryLifetime(long seconds, out TimeSpan lifetime)
    {
        lifetime = default;
        if (seconds <= 0 || seconds > TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerSecond) { return false; }
        try
        {
            lifetime = TimeSpan.FromSeconds(seconds);
            return lifetime > TimeSpan.Zero;
        }
        catch (OverflowException) { return false; }
    }

    public static string[]? Texts(JsonObject? proof, string name)
    {
        if (proof?[name] is not JsonArray array) { return null; }
        var values = new string[array.Count];
        for (int i = 0; i < array.Count; i++)
        {
            if (array[i] is not JsonValue value || !value.TryGetValue<string>(out string? text) || string.IsNullOrWhiteSpace(text)) { return null; }
            values[i] = text;
        }
        return values;
    }

    private static JsonObject ReadObject(ref Utf8JsonReader reader, bool rejectSecretNames)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var result = new JsonObject();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName) { throw new SecretsRejectedException(); }
            string name = reader.GetString() ?? throw new SecretsRejectedException();
            if (!names.Add(name) || rejectSecretNames && SecretNames.Contains(name)) { throw new SecretsRejectedException(); }
            if (!reader.Read()) { throw new SecretsRejectedException(); }
            result[name] = ReadValue(ref reader, rejectSecretNames);
        }
        if (reader.TokenType != JsonTokenType.EndObject) { throw new SecretsRejectedException(); }
        return result;
    }

    private static JsonNode? ReadValue(ref Utf8JsonReader reader, bool rejectSecretNames) => reader.TokenType switch
    {
        JsonTokenType.StartObject => ReadObject(ref reader, rejectSecretNames),
        JsonTokenType.StartArray => ReadArray(ref reader, rejectSecretNames),
        JsonTokenType.String => JsonValue.Create(reader.GetString()),
        JsonTokenType.True => JsonValue.Create(true),
        JsonTokenType.False => JsonValue.Create(false),
        JsonTokenType.Null => null,
        JsonTokenType.Number => ReadInteger(ref reader),
        _ => throw new SecretsRejectedException(),
    };

    private static JsonArray ReadArray(ref Utf8JsonReader reader, bool rejectSecretNames)
    {
        var array = new JsonArray();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            array.Add(ReadValue(ref reader, rejectSecretNames));
        }
        if (reader.TokenType != JsonTokenType.EndArray) { throw new SecretsRejectedException(); }
        return array;
    }

    private static JsonValue ReadInteger(ref Utf8JsonReader reader)
    {
        if (!reader.TryGetInt64(out long number)) { throw new SecretsRejectedException(); }
        return JsonValue.Create(number);
    }

    private sealed class SecretsRejectedException : Exception;
}
