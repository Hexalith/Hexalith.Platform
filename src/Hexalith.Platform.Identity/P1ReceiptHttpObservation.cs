namespace Hexalith.Platform.Identity;

/// <summary>Copied response frames and subject bound to one verified evidence snapshot; grants no authority or custody proof.</summary>
public sealed class P1ReceiptHttpObservation
{
    private readonly byte[] _receipt;
    private readonly byte[] _status;
    private readonly byte[] _subject;

    internal P1ReceiptHttpObservation(byte[] receipt, byte[] status, byte[] subject, P1VerifiedReceiptEvidence evidence)
    {
        _receipt = receipt.ToArray();
        _status = status.ToArray();
        _subject = subject.ToArray();
        Evidence = evidence;
    }

    /// <summary>The exact complete framed receipt response, copied on access.</summary>
    public byte[] ReceiptFrame => _receipt.ToArray();

    /// <summary>The exact complete framed status response, copied on access.</summary>
    public byte[] StatusFrame => _status.ToArray();

    /// <summary>The exact verified subject, copied on access.</summary>
    public byte[] SubjectBytes => _subject.ToArray();

    /// <summary>Immutable digests from the same verification snapshot.</summary>
    public P1VerifiedReceiptEvidence Evidence { get; }
}
