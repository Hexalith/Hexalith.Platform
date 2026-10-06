using System.Globalization;
using System.Text;

namespace Hexalith.Platform.Custody;

/// <summary>AD-29 component bytes; DTO adapters own flattening and explicit optionality.</summary>
public static class PlatformCanonicalBytes
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    /// <summary>Frames identifier components using shipped UTF-16 lengths and U+001F separators.</summary>
    public static byte[] Identity(IEnumerable<string> components)
    {
        ArgumentNullException.ThrowIfNull(components);
        var text = new StringBuilder();
        foreach (string value in components)
        {
            ArgumentNullException.ThrowIfNull(value);
            text.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value).Append('\u001F');
        }
        return Utf8.GetBytes(text.ToString());
    }
    /// <summary>Frames payload/optional components with raw absent/present bytes; null differs from empty.</summary>
    public static byte[] Components(IEnumerable<string?> components)
    {
        ArgumentNullException.ThrowIfNull(components);
        using var stream = new MemoryStream();
        try
        {
            foreach (string? value in components)
            {
                stream.WriteByte(value is null ? (byte)0 : (byte)1);
                byte[] framed = Identity([value ?? string.Empty]);
                try
                {
                    stream.Write(framed);
                }
                finally
                {
                    System.Security.Cryptography.CryptographicOperations.ZeroMemory(framed);
                }
            }
            return stream.ToArray();
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(stream.GetBuffer());
        }
    }
}
