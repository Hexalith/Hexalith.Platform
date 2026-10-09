namespace Hexalith.Platform.Identity;

/// <summary>Offline media-type and canonical-payload decoding for P1 response bodies.</summary>
public static class P1ReceiptTransportV1
{
    /// <summary>Exact receipt response media type.</summary>
    public const string ReceiptMediaType = "application/vnd.hexalith.p1-receipt.v1";

    /// <summary>Exact status response media type.</summary>
    public const string StatusMediaType = "application/vnd.hexalith.p1-status.v1";

    /// <summary>Decodes one complete receipt response with its exact media type.</summary>
    public static bool TryDecodeReceipt(string? mediaType, ReadOnlySpan<byte> response, out P1SignedDocument? document)
    {
        document = null;
        if (!string.Equals(mediaType, ReceiptMediaType, StringComparison.Ordinal)
            || !P1SignedEnvelopeV1.TryDecode(response, out P1SignedDocument? decoded)
            || decoded is null || !P1ReceiptWireV1.TryDecodeReceipt(decoded.Payload, out _))
        {
            return false;
        }

        document = decoded;
        return true;
    }

    /// <summary>Decodes one complete status response with its exact media type.</summary>
    public static bool TryDecodeStatus(string? mediaType, ReadOnlySpan<byte> response, out P1SignedDocument? document)
    {
        document = null;
        if (!string.Equals(mediaType, StatusMediaType, StringComparison.Ordinal)
            || !P1SignedEnvelopeV1.TryDecode(response, out P1SignedDocument? decoded)
            || decoded is null || !P1ReceiptWireV1.TryDecodeStatus(decoded.Payload, out _))
        {
            return false;
        }

        document = decoded;
        return true;
    }
}
