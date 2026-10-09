namespace Hexalith.Platform.Identity;

/// <summary>Immutable digests of one successful offline receipt and status verification snapshot; grants no operational authority.</summary>
public sealed class P1VerifiedReceiptEvidence
{
    internal P1VerifiedReceiptEvidence(
        string receiptPayloadSha256, string receiptSignatureSha256,
        string statusPayloadSha256, string statusSignatureSha256,
        string subjectSha256, long subjectLength, string bootstrapSha256,
        string retrievalUri, string statusUri, string requestNonce)
    {
        ReceiptPayloadSha256 = receiptPayloadSha256;
        ReceiptSignatureSha256 = receiptSignatureSha256;
        StatusPayloadSha256 = statusPayloadSha256;
        StatusSignatureSha256 = statusSignatureSha256;
        SubjectSha256 = subjectSha256;
        SubjectLength = subjectLength;
        BootstrapSha256 = bootstrapSha256;
        RetrievalUri = retrievalUri;
        StatusUri = statusUri;
        RequestNonce = requestNonce;
    }

    /// <summary>Digest of the exact verified receipt payload.</summary>
    public string ReceiptPayloadSha256 { get; }

    /// <summary>Digest of the exact verified receipt signature.</summary>
    public string ReceiptSignatureSha256 { get; }

    /// <summary>Digest of the exact verified status payload.</summary>
    public string StatusPayloadSha256 { get; }

    /// <summary>Digest of the exact verified status signature.</summary>
    public string StatusSignatureSha256 { get; }

    /// <summary>Digest of the exact verified subject bytes.</summary>
    public string SubjectSha256 { get; }

    /// <summary>Length of the exact verified subject bytes.</summary>
    public long SubjectLength { get; }

    /// <summary>Digest of the separately root-verified bootstrap payload.</summary>
    public string BootstrapSha256 { get; }

    /// <summary>Exact receipt locator supplied for verification.</summary>
    public string RetrievalUri { get; }

    /// <summary>Exact status locator supplied for verification.</summary>
    public string StatusUri { get; }

    /// <summary>Exact challenge supplied for status verification.</summary>
    public string RequestNonce { get; }
}
