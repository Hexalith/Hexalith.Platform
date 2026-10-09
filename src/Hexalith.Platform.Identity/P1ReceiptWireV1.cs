using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace Hexalith.Platform.Identity;

/// <summary>Version-one length-prefixed UTF-8 encoding; no JSON normalization or alternate signed representation.</summary>
public static class P1ReceiptWireV1
{
    private const string ReceiptMagic = "HX-P1R/1\n";
    private const string StatusMagic = "HX-P1S/1\n";
    private const string TimeFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>Produces the only valid signed receipt payload representation.</summary>
    public static byte[] Encode(P1ReceiptClaims receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        return Pack(ReceiptMagic, [
            receipt.ReceiptId, receipt.Issuer, receipt.KeyFingerprint, receipt.Audience, receipt.Kind,
            receipt.Action, receipt.Principal, receipt.SubjectRef, receipt.SubjectSha256,
            receipt.SubjectLength.ToString(CultureInfo.InvariantCulture), receipt.ProfileSha256,
            receipt.WorkloadSha256, receipt.TenantId, receipt.ClusterId, receipt.NamespaceUid,
            receipt.TargetSha256, receipt.SessionId, receipt.SourceRevision, receipt.RegistryRevision,
            receipt.PolicyRevision, receipt.GrantId, receipt.Decision, receipt.Reasons,
            Time(receipt.IssuedAtUtc), Time(receipt.ExpiresAtUtc)]);
    }

    /// <summary>Produces the only valid signed status payload representation.</summary>
    public static byte[] Encode(P1StatusClaims status)
    {
        ArgumentNullException.ThrowIfNull(status);
        return Pack(StatusMagic, [
            status.Issuer, status.TrustRevision, status.ReceiptId, status.KeyFingerprint,
            status.GrantId, status.SessionId, status.PolicyRevision, status.ReceiptState, status.KeyState,
            status.GrantState, status.PolicyState, status.SessionState, status.Nonce,
            Time(status.ObservedAtUtc), Time(status.ExpiresAtUtc)]);
    }

    /// <summary>Parses one canonical receipt payload; malformed or alternate encodings refuse.</summary>
    public static bool TryDecodeReceipt(ReadOnlySpan<byte> payload, out P1ReceiptClaims? receipt)
    {
        receipt = null;
        if (!TryUnpack(payload, ReceiptMagic, 25, out string[]? f)
            || !long.TryParse(f[9], NumberStyles.None, CultureInfo.InvariantCulture, out long length)
            || length < 0 || f[9] != length.ToString(CultureInfo.InvariantCulture)
            || !TryTime(f[23], out DateTimeOffset issued) || !TryTime(f[24], out DateTimeOffset expires))
        {
            return false;
        }

        receipt = new(f[0], f[1], f[2], f[3], f[4], f[5], f[6], f[7], f[8], length,
            f[10], f[11], f[12], f[13], f[14], f[15], f[16], f[17], f[18], f[19],
            f[20], f[21], f[22], issued, expires);
        return Encode(receipt).AsSpan().SequenceEqual(payload);
    }

    /// <summary>Parses one canonical status payload; malformed or alternate encodings refuse.</summary>
    public static bool TryDecodeStatus(ReadOnlySpan<byte> payload, out P1StatusClaims? status)
    {
        status = null;
        if (!TryUnpack(payload, StatusMagic, 15, out string[]? f)
            || !TryTime(f[13], out DateTimeOffset observed) || !TryTime(f[14], out DateTimeOffset expires))
        {
            return false;
        }

        status = new(f[0], f[1], f[2], f[3], f[4], f[5], f[6], f[7], f[8], f[9],
            f[10], f[11], f[12], observed, expires);
        return Encode(status).AsSpan().SequenceEqual(payload);
    }

    private static byte[] Pack(string magic, string[] fields)
    {
        using var stream = new MemoryStream();
        stream.Write(Encoding.ASCII.GetBytes(magic));
        Span<byte> length = stackalloc byte[4];
        foreach (string field in fields)
        {
            byte[] data = StrictUtf8.GetBytes(field);
            if (stream.Length + 4L + data.Length > 1024 * 1024)
            {
                throw new ArgumentException("P1 payload exceeds one MiB.");
            }
            BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
            stream.Write(length);
            stream.Write(data);
        }

        return stream.ToArray();
    }

    private static bool TryUnpack(ReadOnlySpan<byte> payload, string magic, int count, out string[] fields)
    {
        fields = [];
        if (payload.Length > 1024 * 1024 || !payload.StartsWith(Encoding.ASCII.GetBytes(magic)))
        {
            return false;
        }

        payload = payload[magic.Length..];
        var result = new string[count];
        try
        {
            for (int i = 0; i < count; i++)
            {
                if (payload.Length < 4)
                {
                    return false;
                }

                int length = BinaryPrimitives.ReadInt32BigEndian(payload);
                payload = payload[4..];
                if (length < 0 || length > payload.Length)
                {
                    return false;
                }

                result[i] = StrictUtf8.GetString(payload[..length]);
                payload = payload[length..];
            }
        }
        catch (DecoderFallbackException)
        {
            return false;
        }

        if (!payload.IsEmpty)
        {
            return false;
        }

        fields = result;
        return true;
    }

    private static string Time(DateTimeOffset instant) => instant.ToUniversalTime().ToString(TimeFormat, CultureInfo.InvariantCulture);

    private static bool TryTime(string value, out DateTimeOffset instant)
        => DateTimeOffset.TryParseExact(value, TimeFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out instant)
            && Time(instant) == value;
}
