using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace Hexalith.Platform.Protection;

/// <summary>Canonical UTF-8 JSON used by the Python owner-role protocol, preserving Unicode and using exact integer spellings.</summary>
internal static class ProtectionQualificationCanonical
{
    internal static byte[] Bytes(JsonNode node)
    {
        var text = new StringBuilder(); Write(text, node);
        return new UTF8Encoding(false, true).GetBytes(text.ToString());
    }
    private static void Write(StringBuilder text, JsonNode? node)
    {
        if (node is null) { text.Append("null"); return; }
        if (node is JsonObject value)
        {
            text.Append('{'); bool first = true;
            foreach (var member in value.OrderBy(item => item.Key, Comparer<string>.Create(Compare)))
            { if (!first) { text.Append(','); } first = false; String(text, member.Key); text.Append(':'); Write(text, member.Value); }
            text.Append('}'); return;
        }
        if (node is JsonArray array)
        { text.Append('['); for (int index = 0; index < array.Count; index++) { if (index > 0) { text.Append(','); } Write(text, array[index]); } text.Append(']'); return; }
        var scalar = (JsonValue)node;
        if (scalar.TryGetValue<string>(out var raw)) { String(text, raw); return; }
        if (scalar.TryGetValue<bool>(out bool flag)) { text.Append(flag ? "true" : "false"); return; }
        if (scalar.TryGetValue<long>(out long number)) { text.Append(number.ToString(CultureInfo.InvariantCulture)); return; }
        if (scalar.TryGetValue<ulong>(out ulong unsigned)) { text.Append(unsigned.ToString(CultureInfo.InvariantCulture)); return; }
        if (scalar.TryGetValue<int>(out int integer)) { text.Append(integer.ToString(CultureInfo.InvariantCulture)); return; }
        if (scalar.TryGetValue<uint>(out uint positive)) { text.Append(positive.ToString(CultureInfo.InvariantCulture)); return; }
        if (scalar.TryGetValue<short>(out short small)) { text.Append(small.ToString(CultureInfo.InvariantCulture)); return; }
        if (scalar.TryGetValue<ushort>(out ushort smallPositive)) { text.Append(smallPositive.ToString(CultureInfo.InvariantCulture)); return; }
        if (scalar.TryGetValue<byte>(out byte octet)) { text.Append(octet.ToString(CultureInfo.InvariantCulture)); return; }
        if (scalar.TryGetValue<sbyte>(out sbyte signedOctet)) { text.Append(signedOctet.ToString(CultureInfo.InvariantCulture)); return; }
        throw new InvalidOperationException("Exact integer JSON required by role protocol.");
    }
    private static int Compare(string left, string right)
    {
        var a = left.EnumerateRunes().GetEnumerator(); var b = right.EnumerateRunes().GetEnumerator();
        while (true)
        {
            bool nextA = a.MoveNext(), nextB = b.MoveNext();
            if (!nextA || !nextB) { return nextA.CompareTo(nextB); }
            int result = a.Current.Value.CompareTo(b.Current.Value); if (result != 0) { return result; }
        }
    }
    private static void String(StringBuilder text, string value)
    {
        _ = new UTF8Encoding(false, true).GetByteCount(value); text.Append('"');
        foreach (char character in value)
        {
            switch (character)
            {
                case '"': text.Append("\\\""); break;
                case '\\': text.Append("\\\\"); break;
                case '\b': text.Append("\\b"); break;
                case '\t': text.Append("\\t"); break;
                case '\n': text.Append("\\n"); break;
                case '\f': text.Append("\\f"); break;
                case '\r': text.Append("\\r"); break;
                default: if (character < 32) { text.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture)); } else { text.Append(character); } break;
            }
        }
        text.Append('"');
    }
}
