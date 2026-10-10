using System.Globalization;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;

namespace Hexalith.Platform.Identity;

/// <summary>Read-only authenticated P1 retrieval and fresh status observation. It grants no authority or custody proof.</summary>
public sealed class P1ReceiptHttpAdapter
{
    private readonly P1AuthenticatedEnrollment _enrollment;
    private readonly P1ReceiptHttpTrust _receiptTrust;
    private readonly P1ReceiptHttpTrust _statusTrust;
    private readonly P1ReceiptHttpHistory _history;
    private readonly P1BootTimeClock _clock;
    internal Action? BeforeFinalCommit { get; set; }

    /// <summary>Creates a two-origin adapter from independently supplied enrollment and production TLS trust.</summary>
    public P1ReceiptHttpAdapter(P1AuthenticatedEnrollment enrollment, P1ReceiptHttpTrust receiptTrust,
        P1ReceiptHttpTrust statusTrust)
        : this(enrollment, receiptTrust, statusTrust, new P1BootTimeClock())
    {
    }

    internal P1ReceiptHttpAdapter(P1AuthenticatedEnrollment enrollment, P1ReceiptHttpTrust receiptTrust,
        P1ReceiptHttpTrust statusTrust, P1BootTimeClock clock, P1ReceiptHttpHistory? history = null)
    {
        ArgumentNullException.ThrowIfNull(enrollment);
        ArgumentNullException.ThrowIfNull(receiptTrust);
        ArgumentNullException.ThrowIfNull(statusTrust);
        ArgumentNullException.ThrowIfNull(clock);
        if (receiptTrust.Origin != enrollment.Enrollment.RetrievalOrigin
            || statusTrust.Origin != enrollment.Enrollment.StatusOrigin)
        {
            throw new ArgumentException("TLS origins must match the independently verified enrollment.");
        }

        _enrollment = enrollment;
        _receiptTrust = receiptTrust;
        _statusTrust = statusTrust;
        _clock = clock;
        _history = history ?? new P1ReceiptHttpHistory();
    }

    /// <summary>Attempts one complete read-only receipt and fresh status chain under caller-supplied scope and authenticated UTC.</summary>
    public async Task<P1ReceiptHttpResult> RetrieveAsync(P1ReceiptClaims? expected, byte[]? subjectBytes,
        DateTimeOffset authenticatedNowUtc, P1ReceiptHttpObservation? priorObservation = null,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Refuse(P1ReceiptHttpFailure.Deadline);
        }

        if (expected is null || subjectBytes is null || authenticatedNowUtc.Offset != TimeSpan.Zero
            || !Hex(expected.ReceiptId) || !_clock.TryRead(out long chainStart))
        {
            return Refuse(P1ReceiptHttpFailure.InvalidInput);
        }

        string receiptUri = _receiptTrust.Origin + "/v1/receipts/" + expected.ReceiptId;
        byte[] subject = subjectBytes.ToArray();
        try
        {
            await using var receiptDeadline = new P1ReceiptHttpDeadline(_clock, chainStart, authenticatedNowUtc, cancellationToken);
            if (!receiptDeadline.Valid)
            {
                return Refuse(P1ReceiptHttpFailure.Deadline);
            }

            byte[]? receiptFrame;
            try
            {
                receiptFrame = await GetAsync(_receiptTrust, "/v1/receipts/" + expected.ReceiptId,
                    P1ReceiptTransportV1.ReceiptMediaType, receiptDeadline).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return Refuse(P1ReceiptHttpFailure.Deadline);
            }
            if (receiptFrame is null)
            {
                return Refuse(receiptDeadline.Valid ? P1ReceiptHttpFailure.Transport : P1ReceiptHttpFailure.Deadline);
            }

            if (!P1ReceiptTransportV1.TryDecodeReceipt(P1ReceiptTransportV1.ReceiptMediaType, receiptFrame,
                    out P1SignedDocument? receipt) || receipt is null
                || !receiptDeadline.TryAuthenticatedUtc(out DateTimeOffset receiptNow)
                || !P1ReceiptVerifier.TryVerifyReceiptOnly(receipt, receiptUri, expected, subject, _enrollment, receiptNow))
            {
                return Refuse(receiptDeadline.Valid ? P1ReceiptHttpFailure.Authority : P1ReceiptHttpFailure.Deadline);
            }

            if (priorObservation is not null &&
                (priorObservation.Evidence.RetrievalUri != receiptUri
                 || !priorObservation.ReceiptFrame.AsSpan().SequenceEqual(receiptFrame)))
            {
                return Refuse(receiptDeadline.Valid ? P1ReceiptHttpFailure.ImmutableIdIncident : P1ReceiptHttpFailure.Deadline);
            }

            if (_history.Compare(expected.ReceiptId, receiptFrame) == P1ReceiptHttpFailure.ImmutableIdIncident)
            {
                return Refuse(receiptDeadline.Valid ? P1ReceiptHttpFailure.ImmutableIdIncident : P1ReceiptHttpFailure.Deadline);
            }

            if (!receiptDeadline.Valid)
            {
                return Refuse(P1ReceiptHttpFailure.Deadline);
            }

            string nonce = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
            string statusPath = "/v1/status/" + expected.ReceiptId + "?nonce=" + nonce;
            string statusUri = _statusTrust.Origin + statusPath;
            await using var statusDeadline = new P1ReceiptHttpDeadline(_clock, chainStart, authenticatedNowUtc, cancellationToken);
            if (!statusDeadline.Valid)
            {
                return Refuse(P1ReceiptHttpFailure.Deadline);
            }

            byte[]? statusFrame;
            try
            {
                statusFrame = await GetAsync(_statusTrust, statusPath, P1ReceiptTransportV1.StatusMediaType,
                    statusDeadline).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return Refuse(P1ReceiptHttpFailure.Deadline);
            }
            if (statusFrame is null)
            {
                return Refuse(statusDeadline.Valid ? P1ReceiptHttpFailure.Transport : P1ReceiptHttpFailure.Deadline);
            }

            if (!P1ReceiptTransportV1.TryDecodeStatus(P1ReceiptTransportV1.StatusMediaType, statusFrame,
                    out P1SignedDocument? status) || status is null
                || !P1ReceiptWireV1.TryDecodeStatus(status.Payload, out P1StatusClaims? statusClaims)
                || statusClaims is null
                || !statusDeadline.TryAuthenticatedUtc(out DateTimeOffset verificationNow)
                || !P1ReceiptVerifier.TryVerify(receipt, receiptUri, status, statusUri, expected, subject,
                    _enrollment, nonce, verificationNow, out P1VerifiedReceiptEvidence? evidence)
                || evidence is null)
            {
                return Refuse(statusDeadline.Valid ? P1ReceiptHttpFailure.Authority : P1ReceiptHttpFailure.Deadline);
            }

            // Prepare all copied result bytes before the final deadline/cancellation and history commit.
            var candidate = new P1ReceiptHttpObservation(receiptFrame, statusFrame, subject, evidence);
            var positive = new P1ReceiptHttpResult(P1ReceiptHttpFailure.None, candidate);
            BeforeFinalCommit?.Invoke();
            P1ReceiptHttpFailure committed = _history.Commit(expected.ReceiptId, receiptFrame,
                () => statusDeadline.TryAuthenticatedUtc(out DateTimeOffset finalNow)
                    && _enrollment.Enrollment.EffectiveAtUtc <= finalNow
                    && finalNow < _enrollment.Enrollment.ExpiresAtUtc
                    && expected.IssuedAtUtc <= finalNow && finalNow < expected.ExpiresAtUtc
                    && statusClaims.ObservedAtUtc <= finalNow && finalNow < statusClaims.ExpiresAtUtc
                    && finalNow - statusClaims.ObservedAtUtc <= TimeSpan.FromSeconds(300));
            return committed == P1ReceiptHttpFailure.None ? positive : Refuse(committed);
        }
        catch (Exception exception) when (exception is IOException or SocketException or AuthenticationException
            or CryptographicException or OperationCanceledException or ObjectDisposedException or ArgumentException
            or InvalidOperationException)
        {
            return Refuse(cancellationToken.IsCancellationRequested ? P1ReceiptHttpFailure.Deadline : P1ReceiptHttpFailure.Transport);
        }
    }

    private static async Task<byte[]?> GetAsync(P1ReceiptHttpTrust trust, string path, string type, P1ReceiptHttpDeadline deadline)
    {
        if (!deadline.Valid || !trust.ValidateClient(deadline) || !deadline.Valid)
        {
            return null;
        }

        using var tcp = new TcpClient();
        await tcp.ConnectAsync(trust.Host, trust.Port, deadline.Token).ConfigureAwait(false);
        if (!deadline.Valid)
        {
            return null;
        }

        using var tls = new SslStream(tcp.GetStream(), false,
            (sender, certificate, chain, errors) => trust.ValidateServer(certificate, chain, errors, deadline));
        SslStreamCertificateContext? context = trust.CreateClientContextBounded(deadline);
        if (context is null || !deadline.Valid)
        {
            return null;
        }

        var options = new SslClientAuthenticationOptions
        {
            TargetHost = trust.Host,
            ClientCertificateContext = context,
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            CertificateRevocationCheckMode = System.Security.Cryptography.X509Certificates.X509RevocationMode.NoCheck,
            CertificateChainPolicy = trust.CreateTlsPolicy(),
        };
        if (!deadline.Valid)
        {
            return null;
        }

        if (!await trust.AuthenticateBoundedAsync(tls, options, deadline).ConfigureAwait(false))
        {
            return null;
        }
        if (!deadline.Valid || !tls.IsMutuallyAuthenticated || tls.LocalCertificate is null
            || !tls.LocalCertificate.GetRawCertData().AsSpan().SequenceEqual(trust.ClientCertificate.RawData)
            || !trust.ValidateClient(deadline) || !deadline.Valid)
        {
            return null;
        }

        string request = "GET " + path + " HTTP/1.1\r\nHost: " + trust.Host
            + (trust.Port == 443 ? "" : ":" + trust.Port.ToString(CultureInfo.InvariantCulture))
            + "\r\nAccept: " + type + "\r\nCache-Control: no-store\r\nPragma: no-cache\r\nConnection: close\r\n\r\n";
        await tls.WriteAsync(Encoding.ASCII.GetBytes(request), deadline.Token).ConfigureAwait(false);
        if (!deadline.Valid)
        {
            return null;
        }

        await tls.FlushAsync(deadline.Token).ConfigureAwait(false);
        if (!deadline.Valid)
        {
            return null;
        }

        return await ReadResponseAsync(tls, type, deadline).ConfigureAwait(false);
    }

    private static async Task<byte[]?> ReadResponseAsync(SslStream stream, string type, P1ReceiptHttpDeadline deadline)
    {
        byte[] header = new byte[16_384];
        byte[] one = new byte[1];
        int count = 0;
        while (count < header.Length)
        {
            int read = await stream.ReadAsync(one, deadline.Token).ConfigureAwait(false);
            if (!deadline.Valid || read != 1 || one[0] > 127 || (one[0] < 32 && one[0] is not (byte)'\r' and not (byte)'\n'))
            {
                return null;
            }

            header[count++] = one[0];
            if (count >= 4 && header.AsSpan(count - 4, 4).SequenceEqual("\r\n\r\n"u8))
            {
                break;
            }
        }

        if (count < 4 || !header.AsSpan(count - 4, 4).SequenceEqual("\r\n\r\n"u8))
        {
            return null;
        }

        string[] lines = Encoding.ASCII.GetString(header, 0, count - 2).Split("\r\n", StringSplitOptions.None);
        if (!lines[0].StartsWith("HTTP/1.1 200 ", StringComparison.Ordinal)
            && !string.Equals(lines[0], "HTTP/1.1 200", StringComparison.Ordinal))
        {
            return null;
        }

        string? contentType = null;
        string? cacheControl = null;
        int? length = null;
        foreach (string line in lines.Skip(1))
        {
            if (line.Length == 0)
            {
                continue;
            }

            int colon = line.IndexOf(':');
            if (colon <= 0 || line[..colon].Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            {
                return null;
            }

            string name = line[..colon];
            string value = line[(colon + 1)..].Trim(' ', '\t');
            if (name.Contains("cache", StringComparison.OrdinalIgnoreCase)
                && !name.Equals("Cache-Control", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                if (contentType is not null) return null;
                contentType = value;
            }
            else if (name.Equals("Cache-Control", StringComparison.OrdinalIgnoreCase))
            {
                if (cacheControl is not null) return null;
                cacheControl = value;
            }
            else if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                if (length is not null || !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed)) return null;
                length = parsed;
            }
            else if (name.Equals("Content-Encoding", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Content-Range", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Expires", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Age", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Surrogate-Control", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }

        if (contentType != type || cacheControl != "no-store" || length is null or <= 0 or > P1SignedEnvelopeV1.MaxResponseLength)
        {
            return null;
        }

        byte[] body = new byte[length.Value];
        int offset = 0;
        while (offset < body.Length)
        {
            int read = await stream.ReadAsync(body.AsMemory(offset), deadline.Token).ConfigureAwait(false);
            if (!deadline.Valid || read <= 0)
            {
                return null;
            }

            offset += read;
        }

        int extra = await stream.ReadAsync(one, deadline.Token).ConfigureAwait(false);
        return deadline.Valid && extra == 0 ? body : null;
    }

    private static P1ReceiptHttpResult Refuse(P1ReceiptHttpFailure failure) => new(failure, null);

    private static bool Hex(string? value)
        => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
