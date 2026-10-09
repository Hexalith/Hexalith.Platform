using System.Buffers.Binary;

namespace Hexalith.Platform.Identity;

/// <summary>Exact version-one length, payload, and detached-signature response framing.</summary>
public static class P1SignedEnvelopeV1
{
    /// <summary>Maximum number of bytes in one complete response body.</summary>
    public const int MaxResponseLength = 1024 * 1024;

    /// <summary>Maximum payload length after the length prefix and detached signature.</summary>
    public const int MaxTransportPayloadLength = MaxResponseLength - 4 - 64;

    /// <summary>Encodes one bounded signed document as an exact response body.</summary>
    public static byte[] Encode(P1SignedDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Payload is not { Length: > 0 and <= MaxTransportPayloadLength }
            || document.Signature is not { Length: 64 })
        {
            throw new ArgumentException("Invalid P1 transport document.", nameof(document));
        }

        byte[] response = new byte[4 + document.Payload.Length + 64];
        BinaryPrimitives.WriteUInt32BigEndian(response, (uint)document.Payload.Length);
        document.Payload.CopyTo(response, 4);
        document.Signature.CopyTo(response, 4 + document.Payload.Length);
        return response;
    }

    /// <summary>Decodes one complete bounded body and copies the payload and signature.</summary>
    public static bool TryDecode(ReadOnlySpan<byte> response, out P1SignedDocument? document)
    {
        document = null;
        if (response.Length is < 69 or > MaxResponseLength)
        {
            return false;
        }

        uint payloadLength = BinaryPrimitives.ReadUInt32BigEndian(response);
        if (payloadLength is 0 or > MaxTransportPayloadLength || response.Length != 4L + payloadLength + 64)
        {
            return false;
        }

        int length = (int)payloadLength;
        document = new(response.Slice(4, length).ToArray(), response.Slice(4 + length, 64).ToArray());
        return true;
    }
}
