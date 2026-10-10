using System.Formats.Asn1;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Hexalith.Platform.Identity;

/// <summary>Independently supplied server pin and scoped mTLS identity for one exact HTTPS DNS origin.</summary>
public sealed class P1ReceiptHttpTrust
{
    private const string ServerAuthOid = "1.3.6.1.5.5.7.3.1";
    private const string ClientAuthOid = "1.3.6.1.5.5.7.3.2";
    private readonly SemaphoreSlim _validationSlot = new(1, 1);
    private readonly SemaphoreSlim _handshakeSlot = new(1, 1);
    private readonly X509Certificate2? _fixtureRoot;
    private readonly bool _fixtureClientNoCheck;

    /// <summary>Constructs production trust. A local custom root cannot be supplied through this constructor.</summary>
    public P1ReceiptHttpTrust(string origin, X509Certificate2? serverAnchor, string? serverSpkiSha256,
        X509Certificate2 clientCertificate, X509Certificate2 clientIssuer, string clientSpiffeId,
        IEnumerable<X509Certificate2>? clientIntermediates = null)
        : this(origin, serverAnchor, serverSpkiSha256, clientCertificate, clientIssuer, clientSpiffeId,
            clientIntermediates, null, false)
    {
    }

    internal P1ReceiptHttpTrust(string origin, X509Certificate2? serverAnchor, string? serverSpkiSha256,
        X509Certificate2 clientCertificate, X509Certificate2 clientIssuer, string clientSpiffeId,
        IEnumerable<X509Certificate2>? clientIntermediates, X509Certificate2? fixtureRoot,
        bool fixtureClientNoCheck = false)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out Uri? uri)
            || uri.Scheme != Uri.UriSchemeHttps || uri.HostNameType != UriHostNameType.Dns
            || uri.Host.Length == 0 || uri.AbsolutePath != "/" || uri.UserInfo.Length != 0
            || uri.Query.Length != 0 || uri.Fragment.Length != 0
            || !string.Equals(uri.GetLeftPart(UriPartial.Authority), origin, StringComparison.Ordinal))
        {
            throw new ArgumentException("An exact HTTPS DNS origin is required.", nameof(origin));
        }

        ArgumentNullException.ThrowIfNull(clientCertificate);
        ArgumentNullException.ThrowIfNull(clientIssuer);
        if (!clientCertificate.HasPrivateKey || (serverAnchor is null) == (serverSpkiSha256 is null))
        {
            throw new ArgumentException("A client private key and exactly one server anchor or SPKI pin are required.");
        }

        if (serverSpkiSha256 is not null && !Hex(serverSpkiSha256))
        {
            throw new ArgumentException("The server SPKI pin must be a lowercase SHA-256 digest.", nameof(serverSpkiSha256));
        }

        if (!ValidSpiffeId(clientSpiffeId))
        {
            throw new ArgumentException("A canonical SPIFFE trust-domain path is required.", nameof(clientSpiffeId));
        }

        if ((fixtureRoot is not null || fixtureClientNoCheck) && (!uri.IsLoopback || uri.HostNameType != UriHostNameType.Dns))
        {
            throw new ArgumentException("Fixture trust is limited to a loopback DNS origin.", nameof(fixtureRoot));
        }

        Origin = origin;
        Host = uri.IdnHost;
        Port = uri.Port;
        ServerAnchor = serverAnchor;
        ServerSpkiSha256 = serverSpkiSha256;
        ClientCertificate = clientCertificate;
        ClientIssuer = clientIssuer;
        ClientSpiffeId = clientSpiffeId;
        ClientIntermediates = clientIntermediates?.ToArray() ?? [];
        _fixtureRoot = fixtureRoot;
        _fixtureClientNoCheck = fixtureClientNoCheck;
    }

    /// <summary>Exact enrolled origin.</summary>
    public string Origin { get; }

    internal string Host { get; }
    internal int Port { get; }
    internal X509Certificate2? ServerAnchor { get; }
    internal string? ServerSpkiSha256 { get; }
    internal X509Certificate2 ClientCertificate { get; }
    internal X509Certificate2 ClientIssuer { get; }
    internal string ClientSpiffeId { get; }
    internal X509Certificate2[] ClientIntermediates { get; }
    internal bool IsFixture => _fixtureRoot is not null;
    internal Action? ContextCreated { get; set; }
    internal Action? BeforeServerChainBuild { get; set; }

    internal SslStreamCertificateContext CreateClientContext()
    {
        SslStreamCertificateContext context = SslStreamCertificateContext.Create(ClientCertificate,
            new X509Certificate2Collection(ClientIntermediates), offline: true);
        ContextCreated?.Invoke();
        return context;
    }

    internal SslStreamCertificateContext? CreateClientContextBounded(P1ReceiptHttpDeadline deadline)
    {
        if (!deadline.Valid || !_validationSlot.Wait(0))
        {
            return null;
        }

        Task<SslStreamCertificateContext> worker = Task.Run(() =>
        {
            try
            {
                return CreateClientContext();
            }
            finally
            {
                _validationSlot.Release();
            }
        });
        try
        {
            SslStreamCertificateContext context = worker.WaitAsync(deadline.Token).GetAwaiter().GetResult();
            return deadline.Valid ? context : null;
        }
        catch (Exception exception) when (exception is OperationCanceledException or AggregateException or CryptographicException
            or ArgumentException or InvalidOperationException or ObjectDisposedException)
        {
            return null;
        }
    }

    internal X509ChainPolicy CreateTlsPolicy()
    {
        var policy = new X509ChainPolicy
        {
            DisableCertificateDownloads = true,
            RevocationMode = IsFixture ? X509RevocationMode.NoCheck : X509RevocationMode.Offline,
            RevocationFlag = X509RevocationFlag.EntireChain,
            VerificationFlags = X509VerificationFlags.NoFlag,
        };
        policy.ApplicationPolicy.Add(new Oid(ServerAuthOid));
        if (IsFixture)
        {
            policy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            policy.CustomTrustStore.Add(_fixtureRoot!);
        }

        return policy;
    }

    internal async Task<bool> AuthenticateBoundedAsync(SslStream stream, SslClientAuthenticationOptions options,
        P1ReceiptHttpDeadline deadline)
    {
        if (!deadline.Valid || !_handshakeSlot.Wait(0))
        {
            return false;
        }

        Task authentication;
        try
        {
            authentication = stream.AuthenticateAsClientAsync(options, deadline.Token);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or ObjectDisposedException
            or AuthenticationException or IOException or CryptographicException)
        {
            _handshakeSlot.Release();
            return false;
        }

        _ = authentication.ContinueWith(completed =>
        {
            if (completed.IsFaulted) _ = completed.Exception;
            _handshakeSlot.Release();
        }, CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        try
        {
            await authentication.WaitAsync(deadline.Token).ConfigureAwait(false);
            return deadline.Valid;
        }
        catch (Exception exception) when (exception is OperationCanceledException or AuthenticationException or IOException
            or CryptographicException or ObjectDisposedException)
        {
            return false;
        }
    }

    internal bool ValidateClient(P1ReceiptHttpDeadline deadline)
        => ValidateBounded(() => ValidateChain(ClientCertificate, ClientIntermediates, ClientAuthOid, true, deadline), deadline);

    internal bool ValidateServer(X509Certificate? peer, X509Chain? tlsChain, SslPolicyErrors errors, P1ReceiptHttpDeadline deadline)
    {
        if (peer is null || (errors & (SslPolicyErrors.RemoteCertificateNotAvailable | SslPolicyErrors.RemoteCertificateNameMismatch)) != 0)
        {
            return false;
        }

        try
        {
            var leaf = new X509Certificate2(peer);
            X509Certificate2[] supplied = tlsChain?.ChainElements.Cast<X509ChainElement>().Skip(1)
                .Select(element => X509CertificateLoader.LoadCertificate(element.Certificate.RawData)).ToArray() ?? [];
            return ValidateBounded(() =>
            {
                try
                {
                    return ValidateChain(leaf, supplied, ServerAuthOid, false, deadline);
                }
                finally
                {
                    leaf.Dispose();
                    foreach (X509Certificate2 certificate in supplied) certificate.Dispose();
                }
            }, deadline, () =>
            {
                leaf.Dispose();
                foreach (X509Certificate2 certificate in supplied) certificate.Dispose();
            });
        }
        catch (Exception exception) when (exception is CryptographicException or ArgumentException or ObjectDisposedException)
        {
            return false;
        }
    }

    private bool ValidateBounded(Func<bool> validation, P1ReceiptHttpDeadline deadline, Action? whenNotStarted = null)
    {
        if (!deadline.Valid || !_validationSlot.Wait(0))
        {
            whenNotStarted?.Invoke();
            return false;
        }

        Task<bool> worker = Task.Run(() =>
        {
            try
            {
                return validation();
            }
            finally
            {
                _validationSlot.Release();
            }
        });
        try
        {
            return worker.WaitAsync(deadline.Token).GetAwaiter().GetResult() && deadline.Valid;
        }
        catch (Exception exception) when (exception is OperationCanceledException or AggregateException or CryptographicException
            or ArgumentException or InvalidOperationException or ObjectDisposedException)
        {
            return false;
        }
    }

    private bool ValidateChain(X509Certificate2 leaf, X509Certificate2[] supplied, string purpose, bool client,
        P1ReceiptHttpDeadline deadline)
    {
        if (!deadline.TryAuthenticatedUtc(out DateTimeOffset before) || !ExplicitEku(leaf, purpose)
            || (client && (!ValidClientSan(leaf) || !leaf.RawData.AsSpan().SequenceEqual(ClientCertificate.RawData))))
        {
            return false;
        }

        using var chain = new X509Chain();
        X509ChainPolicy policy = chain.ChainPolicy;
        policy.VerificationTime = before.UtcDateTime;
        policy.DisableCertificateDownloads = true;
        policy.RevocationMode = IsFixture || (client && _fixtureClientNoCheck)
            ? X509RevocationMode.NoCheck : X509RevocationMode.Offline;
        policy.RevocationFlag = X509RevocationFlag.EntireChain;
        policy.VerificationFlags = X509VerificationFlags.NoFlag;
        policy.ApplicationPolicy.Add(new Oid(purpose));
        foreach (X509Certificate2 intermediate in supplied)
        {
            policy.ExtraStore.Add(intermediate);
        }

        if (client || IsFixture)
        {
            policy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            policy.CustomTrustStore.Add(client ? ClientIssuer : _fixtureRoot!);
        }

        if (!client) BeforeServerChainBuild?.Invoke();
        if (!chain.Build(leaf) || !deadline.TryAuthenticatedUtc(out DateTimeOffset after))
        {
            return false;
        }

        if (!IsFixture && !(client && _fixtureClientNoCheck))
        {
            policy.VerificationTime = after.UtcDateTime;
            if (!chain.Build(leaf) || !deadline.TryAuthenticatedUtc(out DateTimeOffset secondAfter)
                || after.ToUnixTimeSeconds() != secondAfter.ToUnixTimeSeconds())
            {
                return false;
            }

            after = secondAfter;
        }

        X509ChainElementCollection elements = chain.ChainElements;
        if (elements.Count == 0 || !elements[0].Certificate.RawData.AsSpan().SequenceEqual(leaf.RawData)
            || elements.Cast<X509ChainElement>().Any(e => !ValidTime(e.Certificate, after)))
        {
            return false;
        }

        if (client)
        {
            if (!elements[^1].Certificate.RawData.AsSpan().SequenceEqual(ClientIssuer.RawData))
            {
                return false;
            }

            foreach (X509ChainElement element in elements.Cast<X509ChainElement>().Skip(1).Take(elements.Count - 2))
            {
                if (!ClientIntermediates.Any(c => c.RawData.AsSpan().SequenceEqual(element.Certificate.RawData)))
                {
                    return false;
                }
            }

            return deadline.Valid;
        }

        bool anchor = elements.Cast<X509ChainElement>().Any(e => ServerAnchor is not null
            && e.Certificate.RawData.AsSpan().SequenceEqual(ServerAnchor.RawData));
        bool pin = elements.Cast<X509ChainElement>().Any(e => ServerSpkiSha256 is not null
            && TrySpkiDigest(e.Certificate, out string? digest) && digest == ServerSpkiSha256);
        return (anchor || pin) && deadline.Valid;
    }

    private bool ValidClientSan(X509Certificate2 leaf)
    {
        X509SubjectAlternativeNameExtension[] extensions = leaf.Extensions.OfType<X509SubjectAlternativeNameExtension>().ToArray();
        if (extensions.Length != 1)
        {
            return false;
        }

        try
        {
            var reader = new AsnReader(extensions[0].RawData, AsnEncodingRules.DER);
            AsnReader names = reader.ReadSequence();
            int count = 0;
            string? uri = null;
            var uriTag = new Asn1Tag(TagClass.ContextSpecific, 6);
            while (names.HasData)
            {
                if (names.PeekTag().HasSameClassAndValue(uriTag))
                {
                    uri = names.ReadCharacterString(UniversalTagNumber.IA5String, uriTag);
                    count++;
                }
                else
                {
                    names.ReadEncodedValue();
                }
            }

            reader.ThrowIfNotEmpty();
            return count == 1 && string.Equals(uri, ClientSpiffeId, StringComparison.Ordinal);
        }
        catch (AsnContentException)
        {
            return false;
        }
    }

    private static bool ValidSpiffeId(string? id)
    {
        if (id is null || !id.StartsWith("spiffe://", StringComparison.Ordinal)
            || !Uri.TryCreate(id, UriKind.Absolute, out Uri? uri)
            || uri.Scheme != "spiffe" || uri.HostNameType != UriHostNameType.Dns || uri.Host.Length == 0
            || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0
            || uri.Port != -1 || uri.AbsolutePath is "/" or "" || id.EndsWith('/'))
        {
            return false;
        }

        if (!string.Equals(uri.AbsoluteUri, id, StringComparison.Ordinal) || uri.AbsolutePath.Contains('%'))
        {
            return false;
        }

        string[] labels = uri.Host.Split('.');
        if (labels.Any(label => label.Length == 0 || label[0] == '-' || label[^1] == '-'
            || label.Any(c => c is not (>= 'a' and <= 'z' or >= '0' and <= '9' or '-'))))
        {
            return false;
        }

        return uri.AbsolutePath.Split('/').Skip(1).All(segment => segment.Length > 0 && segment is not "." and not ".."
            && segment.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-'));
    }

    private static bool ExplicitEku(X509Certificate2 certificate, string purpose)
        => certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>()
            .Any(extension => extension.EnhancedKeyUsages.Cast<Oid>().Any(oid => oid.Value == purpose));

    private static bool ValidTime(X509Certificate2 certificate, DateTimeOffset now)
        => certificate.NotBefore.ToUniversalTime() <= now.UtcDateTime && now.UtcDateTime < certificate.NotAfter.ToUniversalTime();

    private static bool TrySpkiDigest(X509Certificate2 certificate, out string? digest)
    {
        digest = null;
        try
        {
            byte[] spki = certificate.PublicKey.ExportSubjectPublicKeyInfo();
            if (spki.Length == 0)
            {
                return false;
            }

            digest = Convert.ToHexStringLower(SHA256.HashData(spki));
            return true;
        }
        catch (Exception exception) when (exception is CryptographicException or NotSupportedException)
        {
            return false;
        }
    }

    private static bool Hex(string? value)
        => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
