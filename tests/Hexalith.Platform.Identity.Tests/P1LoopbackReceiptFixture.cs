using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Hexalith.Platform.Identity;

namespace Hexalith.Platform.Identity.Tests;

/// <summary>Ephemeral two-origin TLS/mTLS, bootstrap, receipt and status fixture with no operational authority.</summary>
internal sealed class P1LoopbackReceiptFixture : IAsyncDisposable
{
    private readonly ECDsa _bootstrapKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly ECDsa _receiptKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly ECDsa _statusKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly X509Certificate2 _receiptRoot;
    private readonly X509Certificate2 _statusRoot;
    private readonly X509Certificate2? _receiptServerIntermediate;
    private readonly X509Certificate2? _receiptClientIntermediate;
    private readonly X509Certificate2 _receiptServerCertificate;
    private readonly X509Certificate2 _statusServerCertificate;
    private readonly X509Certificate2 _receiptClientCertificate;
    private readonly X509Certificate2 _statusClientCertificate;

    internal P1LoopbackReceiptFixture(TimeSpan? receiptServerLifetime = null, TimeSpan? receiptClientLifetime = null,
        bool serverAuthEku = true, bool clientAuthEku = true, string serverDnsName = "localhost",
        bool serverIntermediate = false, bool clientIntermediate = false, string? revocationUrl = null,
        TimeSpan? statusServerLifetime = null, TimeSpan? statusClientLifetime = null)
    {
        DateTimeOffset utc = DateTimeOffset.UtcNow;
        Now = new DateTimeOffset(utc.Ticks - utc.Ticks % TimeSpan.TicksPerSecond, TimeSpan.Zero);
        _receiptRoot = CreateRoot("receipt-fixture-root", Now);
        _statusRoot = CreateRoot("status-fixture-root", Now);
        _receiptServerIntermediate = serverIntermediate ? CreateIntermediate(_receiptRoot, "receipt-server-intermediate", Now) : null;
        _receiptClientIntermediate = clientIntermediate ? CreateIntermediate(_receiptRoot, "receipt-client-intermediate", Now) : null;
        _receiptServerCertificate = CreateLeaf(_receiptServerIntermediate ?? _receiptRoot, "receipt-server", Now, true, null,
            receiptServerLifetime ?? TimeSpan.FromHours(4), serverAuthEku, serverDnsName);
        _statusServerCertificate = CreateLeaf(_statusRoot, "status-server", Now, true, null,
            statusServerLifetime ?? TimeSpan.FromHours(4), true, "localhost");
        _receiptClientCertificate = CreateLeaf(_receiptClientIntermediate ?? _receiptRoot, "receipt-client", Now, false, "spiffe://receipt.test/client",
            receiptClientLifetime ?? TimeSpan.FromHours(4), clientAuthEku, "localhost", revocationUrl);
        _statusClientCertificate = CreateLeaf(_statusRoot, "status-client", Now, false, "spiffe://status.test/client",
            statusClientLifetime ?? TimeSpan.FromHours(4), true, "localhost");
        ReceiptFrame = P1SignedEnvelopeV1.Encode(SignReceipt(CreateReceiptClaims()));
        ReceiptServer = new(_receiptServerCertificate, _receiptClientCertificate,
            (path, token) => Task.FromResult(ReceiptFrame), P1ReceiptTransportV1.ReceiptMediaType,
            _receiptServerIntermediate);
        StatusServer = new(_statusServerCertificate, _statusClientCertificate,
            (path, token) => Task.FromResult(CreateStatusFrame(path)), P1ReceiptTransportV1.StatusMediaType);

        byte[] receiptSpki = _receiptKey.ExportSubjectPublicKeyInfo();
        byte[] statusSpki = _statusKey.ExportSubjectPublicKeyInfo();
        var bootstrapClaims = new P1BootstrapClaims("fixture-r1", "https://p1.test.invalid", "hexalith:memories:c1:v1",
            ReceiptServer.Origin, StatusServer.Origin, "fixture-map-r1", receiptSpki, Digest(receiptSpki), statusSpki,
            Digest(statusSpki), Now.AddMinutes(-10), Now.AddDays(1));
        P1SignedDocument bootstrap = Sign(P1BootstrapWireV1.Encode(bootstrapClaims), _bootstrapKey);
        byte[] rootSpki = _bootstrapKey.ExportSubjectPublicKeyInfo();
        var pin = new P1BootstrapPin(rootSpki, Digest(rootSpki), Digest(bootstrap.Payload), bootstrapClaims.Revision,
            bootstrapClaims.Issuer, bootstrapClaims.Audience, bootstrapClaims.RetrievalOrigin,
            bootstrapClaims.StatusOrigin, bootstrapClaims.ReceiptKeyFingerprint, bootstrapClaims.StatusKeyFingerprint,
            bootstrapClaims.PrincipalMappingRevision);
        if (!P1BootstrapVerifier.TryVerify(bootstrap, pin, Now, out P1AuthenticatedEnrollment? verified))
        {
            throw new InvalidOperationException("The fixture bootstrap must verify.");
        }

        Enrollment = verified!;
        ReceiptTrust = new P1ReceiptHttpTrust(ReceiptServer.Origin, _receiptRoot, null, _receiptClientCertificate,
            _receiptRoot, "spiffe://receipt.test/client", _receiptClientIntermediate is null ? null : [_receiptClientIntermediate],
            _receiptRoot);
        StatusTrust = new P1ReceiptHttpTrust(StatusServer.Origin, _statusRoot, null, _statusClientCertificate,
            _statusRoot, "spiffe://status.test/client", null, _statusRoot);
    }

    internal DateTimeOffset Now { get; }
    internal P1LoopbackReceiptServer ReceiptServer { get; }
    internal P1LoopbackReceiptServer StatusServer { get; }
    internal P1AuthenticatedEnrollment Enrollment { get; }
    internal P1ReceiptHttpTrust ReceiptTrust { get; }
    internal P1ReceiptHttpTrust StatusTrust { get; }
    internal X509Certificate2 ReceiptServerCertificate => _receiptServerCertificate;
    internal byte[] Subject { get; } = Encoding.UTF8.GetBytes("loopback subject bytes");
    internal P1ReceiptClaims Expected { get; private set; } = null!;
    internal byte[] ReceiptFrame { get; set; }
    internal Func<P1StatusClaims, P1StatusClaims>? StatusMutation { get; set; }

    internal P1ReceiptHttpAdapter Adapter() => new(Enrollment, ReceiptTrust, StatusTrust);

    internal byte[] SignReceiptFrame(P1ReceiptClaims claims)
        => P1SignedEnvelopeV1.Encode(SignReceipt(claims));

    internal void SetReceipt(P1ReceiptClaims claims)
    {
        Expected = claims;
        ReceiptFrame = SignReceiptFrame(claims);
    }

    internal P1ReceiptHttpTrust PinOnlyReceiptTrust(string? pin = null)
        => new(ReceiptServer.Origin, null, pin ?? Digest(_receiptServerCertificate.PublicKey.ExportSubjectPublicKeyInfo()),
            _receiptClientCertificate, _receiptRoot, "spiffe://receipt.test/client", null, _receiptRoot);

    internal P1ReceiptHttpTrust WrongReceiptTrust()
        => new(ReceiptServer.Origin, _statusRoot, null, _receiptClientCertificate,
            _receiptRoot, "spiffe://receipt.test/client", null, _statusRoot);

    internal P1ReceiptHttpTrust WrongStatusTrust()
        => new(StatusServer.Origin, _receiptRoot, null, _statusClientCertificate,
            _statusRoot, "spiffe://status.test/client", null, _receiptRoot);

    internal P1ReceiptHttpTrust ProductionReceiptTrust()
        => new(ReceiptServer.Origin, _receiptRoot, null, _receiptClientCertificate,
            _receiptRoot, "spiffe://receipt.test/client");

    internal P1ReceiptHttpTrust ProductionServerGuardTrust()
        => new(ReceiptServer.Origin, _receiptRoot, null, _receiptClientCertificate,
            _receiptRoot, "spiffe://receipt.test/client", null, null, true);

    internal P1ReceiptHttpTrust MissingClientIntermediateTrust()
        => new(ReceiptServer.Origin, _receiptRoot, null, _receiptClientCertificate,
            _receiptRoot, "spiffe://receipt.test/client", null, _receiptRoot);

    private P1ReceiptClaims CreateReceiptClaims()
    {
        string subjectDigest = Digest(Subject);
        Expected = new(new string('a', 64), "https://p1.test.invalid", Digest(_receiptKey.ExportSubjectPublicKeyInfo()),
            "hexalith:memories:c1:v1", "producer-execution", "capture", "platform:actor:01HX0000000000000000000001",
            "sha256:" + subjectDigest, subjectDigest, Subject.LongLength, new string('d', 64), new string('e', 64),
            "tenant-a", "cluster-a", "namespace-a", new string('f', 64), "session-a", "source-r1", "registry-r1",
            "policy-r1", "grant-a", "observed", "", Now.AddMinutes(-1), Now.AddHours(1));
        return Expected;
    }

    private byte[] CreateStatusFrame(string path)
    {
        int nonceIndex = path.IndexOf("?nonce=", StringComparison.Ordinal);
        string nonce = nonceIndex >= 0 ? path[(nonceIndex + 7)..] : "";
        var claims = new P1StatusClaims(Expected.Issuer, "fixture-r1", Expected.ReceiptId, Expected.KeyFingerprint,
            Expected.GrantId, Expected.SessionId, Expected.PolicyRevision, "active", "active", "active", "active",
            "active", nonce, Now.AddSeconds(-1), Now.AddSeconds(60));
        claims = StatusMutation?.Invoke(claims) ?? claims;
        return P1SignedEnvelopeV1.Encode(Sign(P1ReceiptWireV1.Encode(claims), _statusKey));
    }

    private P1SignedDocument SignReceipt(P1ReceiptClaims claims)
        => Sign(P1ReceiptWireV1.Encode(claims), _receiptKey);

    private static P1SignedDocument Sign(byte[] payload, ECDsa key)
        => new(payload, key.SignData(payload, HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation));

    private static string Digest(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static X509Certificate2 CreateRoot(string name, DateTimeOffset now)
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=" + name, key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        return request.CreateSelfSigned(now.AddDays(-1), now.AddDays(2));
    }

    private static X509Certificate2 CreateIntermediate(X509Certificate2 issuer, string name, DateTimeOffset now)
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=" + name, key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        using X509Certificate2 publicCertificate = request.Create(issuer, now.AddDays(-1), now.AddDays(2),
            RandomNumberGenerator.GetBytes(16));
        return publicCertificate.CopyWithPrivateKey(key);
    }

    private static X509Certificate2 CreateLeaf(X509Certificate2 issuer, string name, DateTimeOffset now, bool server,
        string? spiffe, TimeSpan lifetime, bool includeEku, string dnsName, string? revocationUrl = null)
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=" + name, key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        if (includeEku)
        {
            var eku = new OidCollection { new(server ? "1.3.6.1.5.5.7.3.1" : "1.3.6.1.5.5.7.3.2") };
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(eku, true));
        }
        var san = new SubjectAlternativeNameBuilder();
        if (server) san.AddDnsName(dnsName);
        else san.AddUri(new Uri(spiffe!));
        request.CertificateExtensions.Add(san.Build());
        if (revocationUrl is not null)
        {
            var crl = new AsnWriter(AsnEncodingRules.DER);
            crl.PushSequence();
            crl.PushSequence();
            crl.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0, true));
            crl.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0, true));
            crl.WriteCharacterString(UniversalTagNumber.IA5String, revocationUrl,
                new Asn1Tag(TagClass.ContextSpecific, 6));
            crl.PopSequence(new Asn1Tag(TagClass.ContextSpecific, 0, true));
            crl.PopSequence(new Asn1Tag(TagClass.ContextSpecific, 0, true));
            crl.PopSequence();
            crl.PopSequence();
            request.CertificateExtensions.Add(new X509Extension("2.5.29.31", crl.Encode(), false));

            var aia = new AsnWriter(AsnEncodingRules.DER);
            aia.PushSequence();
            aia.PushSequence();
            aia.WriteObjectIdentifier("1.3.6.1.5.5.7.48.2");
            aia.WriteCharacterString(UniversalTagNumber.IA5String, revocationUrl,
                new Asn1Tag(TagClass.ContextSpecific, 6));
            aia.PopSequence();
            aia.PopSequence();
            request.CertificateExtensions.Add(new X509Extension("1.3.6.1.5.5.7.1.1", aia.Encode(), false));
        }
        byte[] serial = RandomNumberGenerator.GetBytes(16);
        using X509Certificate2 publicCertificate = request.Create(issuer, now.AddHours(-1), now.Add(lifetime), serial);
        return publicCertificate.CopyWithPrivateKey(key);
    }

    public async ValueTask DisposeAsync()
    {
        await ReceiptServer.DisposeAsync().ConfigureAwait(false);
        await StatusServer.DisposeAsync().ConfigureAwait(false);
        _receiptClientCertificate.Dispose();
        _statusClientCertificate.Dispose();
        _receiptServerCertificate.Dispose();
        _statusServerCertificate.Dispose();
        _receiptServerIntermediate?.Dispose();
        _receiptClientIntermediate?.Dispose();
        _receiptRoot.Dispose();
        _statusRoot.Dispose();
        _bootstrapKey.Dispose();
        _receiptKey.Dispose();
        _statusKey.Dispose();
    }
}
