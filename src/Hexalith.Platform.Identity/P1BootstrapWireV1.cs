using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace Hexalith.Platform.Identity;

/// <summary>Canonical length-prefixed UTF-8 encoding for a root-signed P1 bootstrap.</summary>
public static class P1BootstrapWireV1
{
    private const string Magic = "HX-P1B/1\n";
    private const string TimeFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>Encodes the only accepted signed bootstrap payload representation.</summary>
    public static byte[] Encode(P1BootstrapClaims claims)
    {
        ArgumentNullException.ThrowIfNull(claims);
        if (claims.ReceiptPublicKeySpki is not { Length: > 0 and <= 512 }
            || claims.StatusPublicKeySpki is not { Length: > 0 and <= 512 })
        {
            throw new ArgumentException("P1 bootstrap SPKI length is invalid.", nameof(claims));
        }

        string[] fields = [
            claims.Revision, claims.Issuer, claims.Audience, claims.RetrievalOrigin, claims.StatusOrigin,
            claims.PrincipalMappingRevision, Convert.ToHexStringLower(claims.ReceiptPublicKeySpki),
            claims.ReceiptKeyFingerprint, Convert.ToHexStringLower(claims.StatusPublicKeySpki),
            claims.StatusKeyFingerprint, Time(claims.EffectiveAtUtc), Time(claims.ExpiresAtUtc),
        ];
        using var stream = new MemoryStream();
        stream.Write(Encoding.ASCII.GetBytes(Magic));
        Span<byte> length = stackalloc byte[4];
        foreach (string field in fields)
        {
            int byteCount = StrictUtf8.GetByteCount(field);
            if (stream.Length + 4L + byteCount > 1024 * 1024)
            {
                throw new ArgumentException("P1 bootstrap exceeds one MiB.");
            }

            byte[] bytes = StrictUtf8.GetBytes(field);
            BinaryPrimitives.WriteInt32BigEndian(length, byteCount);
            stream.Write(length);
            stream.Write(bytes);
        }

        return stream.ToArray();
    }

    /// <summary>Decodes only the canonical bootstrap payload; malformed input refuses.</summary>
    public static bool TryDecode(ReadOnlySpan<byte> payload, out P1BootstrapClaims? claims)
    {
        claims = null;
        if (payload.Length > 1024 * 1024 || !payload.StartsWith(Encoding.ASCII.GetBytes(Magic)))
        {
            return false;
        }

        ReadOnlySpan<byte> remaining = payload[Magic.Length..];
        var fields = new string[12];
        try
        {
            for (int i = 0; i < fields.Length; i++)
            {
                if (remaining.Length < 4)
                {
                    return false;
                }

                int length = BinaryPrimitives.ReadInt32BigEndian(remaining);
                remaining = remaining[4..];
                if (length < 0 || length > remaining.Length)
                {
                    return false;
                }

                fields[i] = StrictUtf8.GetString(remaining[..length]);
                remaining = remaining[length..];
            }

            if (!remaining.IsEmpty || !CanonicalHex(fields[6]) || !CanonicalHex(fields[8])
                || !TryTime(fields[10], out DateTimeOffset effective)
                || !TryTime(fields[11], out DateTimeOffset expires))
            {
                return false;
            }

            claims = new(fields[0], fields[1], fields[2], fields[3], fields[4], fields[5],
                Convert.FromHexString(fields[6]), fields[7], Convert.FromHexString(fields[8]), fields[9],
                effective, expires);
            return Encode(claims).AsSpan().SequenceEqual(payload);
        }
        catch (Exception exception) when (exception is ArgumentException or DecoderFallbackException)
        {
            return false;
        }
    }

    private static bool CanonicalHex(string value)
        => value.Length is > 0 and <= 1024 && value.Length % 2 == 0
            && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string Time(DateTimeOffset instant)
    {
        if (instant.Offset != TimeSpan.Zero || instant.Ticks % TimeSpan.TicksPerMillisecond != 0)
        {
            throw new ArgumentException("P1 bootstrap time must be UTC and millisecond-aligned.", nameof(instant));
        }

        return instant.ToString(TimeFormat, CultureInfo.InvariantCulture);
    }

    private static bool TryTime(string value, out DateTimeOffset instant)
        => DateTimeOffset.TryParseExact(value, TimeFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out instant)
            && instant.Offset == TimeSpan.Zero && Time(instant) == value;
}
