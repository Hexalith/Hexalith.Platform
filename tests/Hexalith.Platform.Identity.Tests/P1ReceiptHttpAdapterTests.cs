using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Net.Sockets;
using Hexalith.Platform.Identity;
using Shouldly;

namespace Hexalith.Platform.Identity.Tests;

/// <summary>Loopback-only TLS/mTLS transport tests. Fixture identities are never operational pins.</summary>
public sealed class P1ReceiptHttpAdapterTests
{
    [Fact]
    public async Task TwoOrigins_ReturnExactCopiedFramesAndFreshStatus()
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        P1ReceiptHttpAdapter adapter = fixture.Adapter();
        P1ReceiptHttpResult first = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken);
        first.Failure.ShouldBe(P1ReceiptHttpFailure.None);
        first.Observation.ShouldNotBeNull();
        first.Observation.ReceiptFrame.ShouldBe(fixture.ReceiptFrame);
        first.Observation.StatusFrame.ShouldBe(fixture.LastStatusFrame);
        first.Observation.Evidence.ReceiptPayloadSha256.ShouldBe(Digest(P1SignedEnvelopeV1.TryDecode(fixture.ReceiptFrame, out P1SignedDocument? receipt)
            ? receipt!.Payload : []));
        first.Observation.Evidence.ReceiptSignatureSha256.ShouldBe(Digest(receipt!.Signature));
        P1SignedEnvelopeV1.TryDecode(first.Observation.StatusFrame, out P1SignedDocument? status).ShouldBeTrue();
        status.ShouldNotBeNull();
        first.Observation.Evidence.StatusPayloadSha256.ShouldBe(Digest(status.Payload));
        first.Observation.Evidence.StatusSignatureSha256.ShouldBe(Digest(status.Signature));
        first.Observation.Evidence.SubjectSha256.ShouldBe(Digest(fixture.Subject));
        first.Observation.Evidence.RequestNonce.Length.ShouldBe(64);
        first.Observation.Evidence.RequestNonce.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f').ShouldBeTrue();
        fixture.ReceiptServer.Requests.Count.ShouldBe(1);
        fixture.StatusServer.Requests.Count.ShouldBe(1);
        fixture.ReceiptServer.Requests.Single().NoStore.ShouldBeTrue();
        fixture.StatusServer.Requests.Single().NoStore.ShouldBeTrue();
        fixture.ReceiptServer.Requests.Single().Mutual.ShouldBeTrue();
        fixture.StatusServer.Requests.Single().Mutual.ShouldBeTrue();
        fixture.ReceiptServer.Requests.Single().Method.ShouldBe("GET");
        fixture.StatusServer.Requests.Single().Method.ShouldBe("GET");
        fixture.ReceiptServer.Requests.Single().Path.ShouldBe("/v1/receipts/" + fixture.Expected.ReceiptId);
        fixture.ReceiptServer.Requests.Single().Host.ShouldBe(new Uri(fixture.ReceiptServer.Origin).Authority);
        fixture.StatusServer.Requests.Single().Host.ShouldBe(new Uri(fixture.StatusServer.Origin).Authority);
        fixture.StatusServer.Requests.Single().Path.ShouldBe("/v1/status/" + fixture.Expected.ReceiptId
            + "?nonce=" + first.Observation.Evidence.RequestNonce);

        first.Observation.ReceiptFrame[0] ^= 1;
        first.Observation.StatusFrame[0] ^= 1;
        first.Observation.SubjectBytes[0] ^= 1;
        first.Observation.ReceiptFrame.ShouldBe(fixture.ReceiptFrame);
        first.Observation.StatusFrame.ShouldBe(fixture.LastStatusFrame);
        first.Observation.SubjectBytes.ShouldBe(fixture.Subject);

        P1ReceiptHttpResult second = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            first.Observation, TestContext.Current.CancellationToken);
        second.Succeeded.ShouldBeTrue();
        second.Observation!.Evidence.RequestNonce.ShouldNotBe(first.Observation.Evidence.RequestNonce);
    }

    [Fact]
    public async Task PinOnlyFixtureSucceedsAndWrongPinRefuses()
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        var pinOnly = new P1ReceiptHttpAdapter(fixture.Enrollment, fixture.PinOnlyReceiptTrust(), fixture.StatusTrust);
        (await pinOnly.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken)).Succeeded.ShouldBeTrue();
        var wrong = new P1ReceiptHttpAdapter(fixture.Enrollment, fixture.PinOnlyReceiptTrust(new string('f', 64)), fixture.StatusTrust);
        P1ReceiptHttpResult refused = await wrong.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken);
        refused.Succeeded.ShouldBeFalse();
        refused.Observation.ShouldBeNull();
        fixture.StatusServer.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task WrongStatusTrustAndProductionTrustRefuse()
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        Should.Throw<ArgumentException>(() => new P1ReceiptHttpAdapter(fixture.Enrollment, fixture.ReceiptTrust, fixture.WrongReceiptTrust()));
        var wrongStatus = new P1ReceiptHttpAdapter(fixture.Enrollment, fixture.ReceiptTrust, fixture.WrongStatusTrust());
        P1ReceiptHttpResult wrongStatusResult = await wrongStatus.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken);
        wrongStatusResult.Succeeded.ShouldBeFalse();
        wrongStatusResult.Observation.ShouldBeNull();
        fixture.ReceiptServer.Requests.Count.ShouldBe(1);
        fixture.StatusServer.Requests.ShouldBeEmpty();
        var production = new P1ReceiptHttpAdapter(fixture.Enrollment, fixture.ProductionReceiptTrust(), fixture.StatusTrust);
        P1ReceiptHttpResult result = await production.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken);
        result.Succeeded.ShouldBeFalse();
        result.Observation.ShouldBeNull();
    }

    [Fact]
    public async Task CorrectRootWithWrongStatusAnchorRefuses()
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        var adapter = new P1ReceiptHttpAdapter(fixture.Enrollment, fixture.ReceiptTrust, fixture.WrongStatusAnchorTrust());
        P1ReceiptHttpResult result = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken);
        result.Succeeded.ShouldBeFalse();
        result.Observation.ShouldBeNull();
        fixture.StatusServer.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Age: 1\r\n")]
    [InlineData("CF-Cache-Status: HIT\r\n")]
    [InlineData("X-Cache-Status: HIT\r\n")]
    [InlineData("Expires: Wed, 21 Oct 2026 07:28:00 GMT\r\n")]
    [InlineData("Content-Encoding: gzip\r\n")]
    [InlineData("Cache-Control: public\r\n")]
    [InlineData("Content-Type: application/vnd.hexalith.p1-receipt.v1\r\n")]
    public async Task ContradictoryHeadersRefuseWithoutStatus(string header)
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        fixture.ReceiptServer.ExtraHeaders = header;
        P1ReceiptHttpResult result = await fixture.Adapter().RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken);
        result.Succeeded.ShouldBeFalse();
        result.Observation.ShouldBeNull();
        fixture.StatusServer.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("public")]
    public async Task SoleMissingOrPublicCacheDirectiveRefuses(string? directive)
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        fixture.ReceiptServer.CacheControl = directive;
        P1ReceiptHttpResult result = await fixture.Adapter().RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken);
        result.Observation.ShouldBeNull();
        fixture.StatusServer.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("duplicate-length")]
    [InlineData("transfer")]
    [InlineData("truncated")]
    [InlineData("extra-byte")]
    [InlineData("short-length")]
    [InlineData("long-length")]
    [InlineData("bare-lf")]
    [InlineData("bare-cr")]
    [InlineData("ascii-del")]
    public async Task MalformedHttpFramingRefusesBeforeStatus(string mode)
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        switch (mode)
        {
            case "duplicate-length": fixture.ReceiptServer.ExtraHeaders = "Content-Length: " + fixture.ReceiptFrame.Length + "\r\n"; break;
            case "transfer": fixture.ReceiptServer.ExtraHeaders = "Transfer-Encoding: chunked\r\n"; break;
            case "truncated": fixture.ReceiptServer.BodyBytesToSend = fixture.ReceiptFrame.Length - 1; break;
            case "extra-byte": fixture.ReceiptServer.BodySuffix = [0]; break;
            case "short-length": fixture.ReceiptServer.AdvertisedLength = fixture.ReceiptFrame.Length - 1; break;
            case "long-length": fixture.ReceiptServer.AdvertisedLength = fixture.ReceiptFrame.Length + 1; break;
            case "bare-lf": fixture.ReceiptServer.ExtraHeaders = "X-Unknown: value\nCF-Cache-Status: HIT\r\n"; break;
            case "bare-cr": fixture.ReceiptServer.ExtraHeaders = "X-Unknown: value\rmore\r\n"; break;
            case "ascii-del": fixture.ReceiptServer.ExtraHeaders = "X-Unknown: \u007f\r\n"; break;
        }

        P1ReceiptHttpResult result = await fixture.Adapter().RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken);
        result.Observation.ShouldBeNull();
        fixture.StatusServer.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public async Task ResponseHeaderTerminatorAtExactLimitIsAcceptedAndBeyondLimitRefused(int excessBytes, bool accepted)
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        string prefix = "HTTP/1.1 200 OK\r\nContent-Type: " + P1ReceiptTransportV1.ReceiptMediaType
            + "\r\nCache-Control: no-store\r\nContent-Length: " + fixture.ReceiptFrame.Length
            + "\r\nConnection: close\r\n";
        int padding = 16_384 - prefix.Length - "X-Pad: \r\n\r\n".Length + excessBytes;
        fixture.ReceiptServer.ExtraHeaders = "X-Pad: " + new string('a', padding) + "\r\n";
        (prefix + fixture.ReceiptServer.ExtraHeaders + "\r\n").Length.ShouldBe(16_384 + excessBytes);

        P1ReceiptHttpResult result = await fixture.Adapter().RetrieveAsync(fixture.Expected, fixture.Subject,
            fixture.Now, cancellationToken: TestContext.Current.CancellationToken);
        result.Succeeded.ShouldBe(accepted);
        if (!accepted)
        {
            result.Observation.ShouldBeNull();
            fixture.StatusServer.Requests.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task ChangedValidSignatureIsIncidentWithoutStatusOrReplacement()
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        P1ReceiptHttpAdapter adapter = fixture.Adapter();
        P1ReceiptHttpResult first = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken);
        first.Succeeded.ShouldBeTrue();
        byte[] original = fixture.ReceiptFrame;
        fixture.ReceiptFrame = fixture.SignReceiptFrame(fixture.Expected);
        P1SignedEnvelopeV1.TryDecode(original, out P1SignedDocument? originalDocument).ShouldBeTrue();
        P1SignedEnvelopeV1.TryDecode(fixture.ReceiptFrame, out P1SignedDocument? changedDocument).ShouldBeTrue();
        changedDocument!.Payload.ShouldBe(originalDocument!.Payload);
        changedDocument.Signature.ShouldNotBe(originalDocument.Signature);
        P1ReceiptHttpResult changed = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken);
        changed.Failure.ShouldBe(P1ReceiptHttpFailure.ImmutableIdIncident);
        changed.Observation.ShouldBeNull();
        fixture.StatusServer.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task SignedWrongScopeRefusesBeforeStatusAndDoesNotSeedHistory()
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        P1ReceiptHttpAdapter adapter = fixture.Adapter();
        byte[] first = fixture.ReceiptFrame;
        fixture.ReceiptFrame = fixture.SignReceiptFrame(fixture.Expected with { TargetSha256 = new string('b', 64) });
        P1ReceiptHttpResult refused = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken);
        refused.Failure.ShouldBe(P1ReceiptHttpFailure.Authority);
        refused.Observation.ShouldBeNull();
        fixture.StatusServer.Requests.ShouldBeEmpty();
        fixture.ReceiptFrame = first;
        (await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken)).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task NullIdAndExpiredStatusRefuseWithoutObservation()
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        P1ReceiptHttpAdapter adapter = fixture.Adapter();
        P1ReceiptHttpResult badId = await adapter.RetrieveAsync(fixture.Expected with { ReceiptId = null! }, fixture.Subject,
            fixture.Now, cancellationToken: TestContext.Current.CancellationToken);
        badId.Observation.ShouldBeNull();
        fixture.ReceiptServer.Requests.ShouldBeEmpty();
        fixture.StatusMutation = claims => claims with { ExpiresAtUtc = fixture.Now };
        P1ReceiptHttpResult expired = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken);
        expired.Succeeded.ShouldBeFalse();
        expired.Observation.ShouldBeNull();
    }

    [Theory]
    [InlineData("redirect")]
    [InlineData("wrong-type")]
    [InlineData("bad-frame")]
    [InlineData("oversize")]
    [InlineData("partial")]
    public async Task ReceiptTransportRefusalsNeverRequestStatus(string mode)
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        switch (mode)
        {
            case "redirect": fixture.ReceiptServer.StatusCode = 302; break;
            case "wrong-type": fixture.ReceiptServer.MediaType = P1ReceiptTransportV1.StatusMediaType; break;
            case "bad-frame": fixture.ReceiptFrame = fixture.ReceiptFrame[..^1]; break;
            case "oversize": fixture.ReceiptFrame = new byte[1_048_577]; break;
            case "partial": fixture.ReceiptServer.ExtraHeaders = "Content-Range: bytes 0-1/2\r\n"; break;
        }

        P1ReceiptHttpResult result = await fixture.Adapter().RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken);
        result.Succeeded.ShouldBeFalse();
        result.Observation.ShouldBeNull();
        fixture.StatusServer.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("revoked")]
    [InlineData("stale-nonce")]
    [InlineData("wrong-type")]
    public async Task StatusAuthorityAndTransportRefusalsReturnNoObservation(string mode)
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        if (mode == "revoked") fixture.StatusMutation = claims => claims with { ReceiptState = "revoked" };
        if (mode == "stale-nonce") fixture.StatusMutation = claims => claims with { Nonce = new string('e', 64) };
        if (mode == "wrong-type") fixture.StatusServer.MediaType = P1ReceiptTransportV1.ReceiptMediaType;
        P1ReceiptHttpResult result = await fixture.Adapter().RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken);
        result.Succeeded.ShouldBeFalse();
        result.Observation.ShouldBeNull();
        fixture.StatusServer.Requests.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("revoked")]
    [InlineData("wrong-type")]
    public async Task RefusedStatusDoesNotSeedImmutableIdHistory(string mode)
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        var adapter = fixture.Adapter();
        if (mode == "revoked") fixture.StatusMutation = claims => claims with { ReceiptState = "revoked" };
        else fixture.StatusServer.MediaType = P1ReceiptTransportV1.ReceiptMediaType;
        P1ReceiptHttpResult refused = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken);
        refused.Observation.ShouldBeNull();
        fixture.StatusMutation = null;
        fixture.StatusServer.MediaType = P1ReceiptTransportV1.StatusMediaType;
        fixture.ReceiptFrame = fixture.SignReceiptFrame(fixture.Expected);
        (await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken)).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task CallerSuppliedPriorFrameDetectsChangeAcrossAdapterInstances()
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        P1ReceiptHttpResult first = await fixture.Adapter().RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken);
        first.Succeeded.ShouldBeTrue();
        fixture.ReceiptFrame = fixture.SignReceiptFrame(fixture.Expected);
        P1ReceiptHttpResult changed = await fixture.Adapter().RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            first.Observation, TestContext.Current.CancellationToken);
        changed.Failure.ShouldBe(P1ReceiptHttpFailure.ImmutableIdIncident);
        changed.Observation.ShouldBeNull();
        fixture.StatusServer.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task HistoryFullIdRetryWithChangedValidFrameRemainsCapacityRefusal()
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        var history = new P1ReceiptHttpHistory();
        for (int i = 0; i < P1ReceiptHttpHistory.MaxIds; i++)
        {
            history.Commit(i.ToString("x64"), [1], () => true).ShouldBe(P1ReceiptHttpFailure.None);
        }

        var adapter = new P1ReceiptHttpAdapter(fixture.Enrollment, fixture.ReceiptTrust, fixture.StatusTrust,
            new P1BootTimeClock(), history);
        P1ReceiptHttpResult first = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken);
        first.Failure.ShouldBe(P1ReceiptHttpFailure.HistoryFull);
        first.Observation.ShouldBeNull();
        fixture.ReceiptFrame = fixture.SignReceiptFrame(fixture.Expected);
        P1ReceiptHttpResult second = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken);
        second.Failure.ShouldBe(P1ReceiptHttpFailure.HistoryFull);
        second.Observation.ShouldBeNull();
    }

    [Theory]
    [InlineData("spiffe://receipt.test/client?query=1")]
    [InlineData("spiffe://user@receipt.test/client")]
    [InlineData("spiffe://receipt.test/client#fragment")]
    [InlineData("spiffe://receipt.test/client%2Fother")]
    [InlineData("spiffe://receipt.test/client//other")]
    [InlineData("https://receipt.test/client")]
    public async Task NoncanonicalSpiffeIdsAreRejectedAtTrustConstruction(string id)
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        Should.Throw<ArgumentException>(() => new P1ReceiptHttpTrust(fixture.ReceiptServer.Origin,
            fixture.ReceiptTrust.ServerAnchor, null, fixture.ReceiptTrust.ClientCertificate,
            fixture.ReceiptTrust.ClientIssuer, id));
    }

    [Fact]
    public async Task NullIssuerAndIpLiteralOriginAreRejectedAtTrustConstruction()
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        Should.Throw<ArgumentNullException>(() => new P1ReceiptHttpTrust(fixture.ReceiptServer.Origin,
            fixture.ReceiptTrust.ServerAnchor, null, fixture.ReceiptTrust.ClientCertificate, null!,
            "spiffe://receipt.test/client"));
        Should.Throw<ArgumentException>(() => new P1ReceiptHttpTrust("https://127.0.0.1:443",
            fixture.ReceiptTrust.ServerAnchor, null, fixture.ReceiptTrust.ClientCertificate,
            fixture.ReceiptTrust.ClientIssuer, "spiffe://receipt.test/client"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WrongOrSecondClientSpiffeUriRefusesBeforeHttp(bool secondSan)
    {
        await using var fixture = new P1LoopbackReceiptFixture(secondClientUriSan: secondSan);
        P1ReceiptHttpTrust trust = secondSan ? fixture.ReceiptTrust : fixture.WrongClientSpiffeTrust();
        var adapter = new P1ReceiptHttpAdapter(fixture.Enrollment, trust, fixture.StatusTrust);
        P1ReceiptHttpResult result = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken);
        result.Observation.ShouldBeNull();
        fixture.ReceiptServer.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InitialAuthenticatedCertificateExpiryRefusesBeforeHttp(bool serverExpires)
    {
        await using var fixture = new P1LoopbackReceiptFixture(
            receiptServerLifetime: serverExpires ? TimeSpan.FromMinutes(30) : null,
            receiptClientLifetime: serverExpires ? null : TimeSpan.FromMinutes(30));
        P1ReceiptHttpResult result = await fixture.Adapter().RetrieveAsync(fixture.Expected, fixture.Subject,
            fixture.Now.AddMinutes(31), cancellationToken: TestContext.Current.CancellationToken);
        result.Succeeded.ShouldBeFalse();
        result.Observation.ShouldBeNull();
        fixture.ReceiptServer.Requests.ShouldBeEmpty();
        fixture.StatusServer.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(true, false, "localhost")]
    [InlineData(false, true, "localhost")]
    [InlineData(false, false, "wrong.localhost")]
    public async Task MissingPurposeOrWrongDnsRefusesBeforeHttp(bool missingClientEku, bool missingServerEku, string dns)
    {
        await using var fixture = new P1LoopbackReceiptFixture(serverAuthEku: !missingServerEku,
            clientAuthEku: !missingClientEku, serverDnsName: dns);
        P1ReceiptHttpResult result = await fixture.Adapter().RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken);
        result.Succeeded.ShouldBeFalse();
        result.Observation.ShouldBeNull();
        fixture.ReceiptServer.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ServerWithoutClientCertificateRequestRefusesBeforeHttp()
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        fixture.ReceiptServer.RequestClientCertificate = false;
        P1ReceiptHttpResult result = await fixture.Adapter().RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken);
        result.Succeeded.ShouldBeFalse();
        result.Observation.ShouldBeNull();
        fixture.ReceiptServer.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task SuppliedServerAndClientIntermediatesVerifyAndMissingClientIntermediateRefuses()
    {
        await using var fixture = new P1LoopbackReceiptFixture(serverIntermediate: true, clientIntermediate: true);
        fixture.ReceiptServer.RequireClientChainThrough(fixture.ReceiptClientIntermediate!);
        P1ReceiptHttpResult positive = await fixture.Adapter().RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken);
        positive.Succeeded.ShouldBeTrue();
        var missing = new P1ReceiptHttpAdapter(fixture.Enrollment, fixture.MissingClientIntermediateTrust(), fixture.StatusTrust);
        P1ReceiptHttpResult refused = await missing.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken);
        refused.Succeeded.ShouldBeFalse();
        refused.Observation.ShouldBeNull();
        fixture.StatusServer.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task SelfSignedClientLeafCannotEnrollAsItsOwnIssuer()
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=self-client", key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new("1.3.6.1.5.5.7.3.2") }, true));
        var san = new SubjectAlternativeNameBuilder();
        san.AddUri(new Uri("spiffe://receipt.test/client"));
        request.CertificateExtensions.Add(san.Build());
        using X509Certificate2 leaf = request.CreateSelfSigned(fixture.Now.AddDays(-1), fixture.Now.AddDays(1));

        Should.Throw<ArgumentException>(() => new P1ReceiptHttpTrust(fixture.ReceiptServer.Origin,
            fixture.ReceiptServerCertificate, null, leaf, leaf, "spiffe://receipt.test/client"));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task DistinctClientIssuerRequiresCaAndCertificateSigningUsage(bool isCa, bool canSignCertificates)
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=invalid-client-issuer", key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(isCa, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            canSignCertificates ? X509KeyUsageFlags.KeyCertSign : X509KeyUsageFlags.DigitalSignature, true));
        using X509Certificate2 issuer = request.CreateSelfSigned(fixture.Now.AddDays(-1), fixture.Now.AddDays(1));
        issuer.RawData.ShouldNotBe(fixture.ReceiptTrust.ClientCertificate.RawData);

        Should.Throw<ArgumentException>(() => new P1ReceiptHttpTrust(fixture.ReceiptServer.Origin,
            fixture.ReceiptServerCertificate, null, fixture.ReceiptTrust.ClientCertificate, issuer,
            "spiffe://receipt.test/client"));
    }

    [Fact]
    public async Task ExpiredSuppliedIntermediateAndDifferentEnrolledIssuerRefuseBeforeHttp()
    {
        await using var expired = new P1LoopbackReceiptFixture(clientIntermediate: true,
            clientIntermediateLifetime: TimeSpan.FromMinutes(30));
        expired.ReceiptClientIntermediate!.NotAfter.ToUniversalTime().ShouldBeLessThan(expired.Now.AddMinutes(31).UtcDateTime);
        expired.ReceiptClientCertificate.NotAfter.ToUniversalTime().ShouldBeGreaterThan(expired.Now.AddMinutes(31).UtcDateTime);
        P1ReceiptHttpResult expiredResult = await expired.Adapter().RetrieveAsync(expired.Expected, expired.Subject,
            expired.Now.AddMinutes(31), cancellationToken: TestContext.Current.CancellationToken);
        expiredResult.Observation.ShouldBeNull();
        expired.ReceiptServer.Requests.ShouldBeEmpty();

        await using var different = new P1LoopbackReceiptFixture();
        var adapter = new P1ReceiptHttpAdapter(different.Enrollment, different.WrongClientIssuerTrust(), different.StatusTrust);
        P1ReceiptHttpResult differentResult = await adapter.RetrieveAsync(different.Expected, different.Subject,
            different.Now, cancellationToken: TestContext.Current.CancellationToken);
        differentResult.Observation.ShouldBeNull();
        different.ReceiptServer.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ServerLeafExpiringDuringChainBuildRefusesBeforeHttp()
    {
        await using var fixture = new P1LoopbackReceiptFixture(receiptServerLifetime: TimeSpan.FromMinutes(30));
        var clock = new P1TestBootTimeClock();
        fixture.ReceiptTrust.BeforeServerChainBuild = () => clock.Advance(TimeSpan.FromSeconds(2));
        var adapter = new P1ReceiptHttpAdapter(fixture.Enrollment, fixture.ReceiptTrust, fixture.StatusTrust, clock);
        P1ReceiptHttpResult result = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject,
            fixture.Now.AddMinutes(29).AddSeconds(59), cancellationToken: TestContext.Current.CancellationToken);
        result.Observation.ShouldBeNull();
        fixture.ReceiptServer.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ClientExpiryDuringContextCreationRefusesWithNoHttp()
    {
        await using var fixture = new P1LoopbackReceiptFixture(receiptClientLifetime: TimeSpan.FromMinutes(30));
        var clock = new P1TestBootTimeClock();
        fixture.ReceiptTrust.ContextCreated = () => clock.Advance(TimeSpan.FromSeconds(2));
        var adapter = new P1ReceiptHttpAdapter(fixture.Enrollment, fixture.ReceiptTrust, fixture.StatusTrust, clock);
        P1ReceiptHttpResult result = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject,
            fixture.Now.AddMinutes(29).AddSeconds(59), cancellationToken: TestContext.Current.CancellationToken);
        result.Succeeded.ShouldBeFalse();
        result.Observation.ShouldBeNull();
        fixture.ReceiptServer.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ClientExpiryDuringTlsHandshakeRefusesWithNoHttp()
    {
        await using var fixture = new P1LoopbackReceiptFixture(receiptClientLifetime: TimeSpan.FromMinutes(30));
        var clock = new P1TestBootTimeClock();
        fixture.ReceiptTrust.BeforeServerChainBuild = () => clock.Advance(TimeSpan.FromSeconds(2));
        var adapter = new P1ReceiptHttpAdapter(fixture.Enrollment, fixture.ReceiptTrust, fixture.StatusTrust, clock);
        P1ReceiptHttpResult result = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject,
            fixture.Now.AddMinutes(29).AddSeconds(59), cancellationToken: TestContext.Current.CancellationToken);
        result.Observation.ShouldBeNull();
        fixture.ReceiptServer.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StatusOriginCertificateExpiryBetweenGetsRefusesBeforeStatusHttp(bool serverExpires)
    {
        await using var fixture = new P1LoopbackReceiptFixture(
            statusServerLifetime: serverExpires ? TimeSpan.FromMinutes(30) : null,
            statusClientLifetime: serverExpires ? null : TimeSpan.FromMinutes(30));
        var clock = new P1TestBootTimeClock();
        fixture.ReceiptServer.BeforeResponse = token =>
        {
            clock.Advance(TimeSpan.FromSeconds(2));
            return Task.CompletedTask;
        };
        var adapter = new P1ReceiptHttpAdapter(fixture.Enrollment, fixture.ReceiptTrust, fixture.StatusTrust, clock);
        P1ReceiptHttpResult result = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject,
            fixture.Now.AddMinutes(29).AddSeconds(59), cancellationToken: TestContext.Current.CancellationToken);
        result.Succeeded.ShouldBeFalse();
        result.Observation.ShouldBeNull();
        fixture.ReceiptServer.Requests.Count.ShouldBe(1);
        fixture.StatusServer.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task UnavailableBootTimeClockRefusesWithoutNetwork()
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        var adapter = new P1ReceiptHttpAdapter(fixture.Enrollment, fixture.ReceiptTrust, fixture.StatusTrust,
            new P1UnavailableBootTimeClock());
        P1ReceiptHttpResult result = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken);
        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldBe(P1ReceiptHttpFailure.Deadline);
        result.Observation.ShouldBeNull();
        fixture.ReceiptServer.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task FinalStatusExpiryRefusesWithoutSeedingHistory()
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        var clock = new P1TestBootTimeClock();
        fixture.StatusMutation = claims => claims with { ExpiresAtUtc = fixture.Now.AddSeconds(2) };
        var adapter = new P1ReceiptHttpAdapter(fixture.Enrollment, fixture.ReceiptTrust, fixture.StatusTrust, clock);
        adapter.BeforeFinalCommit = () => clock.Advance(TimeSpan.FromSeconds(3));
        P1ReceiptHttpResult refused = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken);
        refused.Failure.ShouldBe(P1ReceiptHttpFailure.Deadline);
        refused.Observation.ShouldBeNull();
        adapter.BeforeFinalCommit = null;
        fixture.StatusMutation = null;
        fixture.ReceiptFrame = fixture.SignReceiptFrame(fixture.Expected);
        P1ReceiptHttpResult retried = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken);
        retried.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task FinalReceiptExpiryRefusesWithoutObservation()
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        fixture.SetReceipt(fixture.Expected with { ExpiresAtUtc = fixture.Now.AddSeconds(2) });
        var clock = new P1TestBootTimeClock();
        var adapter = new P1ReceiptHttpAdapter(fixture.Enrollment, fixture.ReceiptTrust, fixture.StatusTrust, clock);
        adapter.BeforeFinalCommit = () => clock.Advance(TimeSpan.FromSeconds(3));
        P1ReceiptHttpResult refused = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken);
        refused.Failure.ShouldBe(P1ReceiptHttpFailure.Deadline);
        refused.Observation.ShouldBeNull();
        adapter.BeforeFinalCommit = null;
        fixture.SetReceipt(fixture.Expected with { ExpiresAtUtc = fixture.Now.AddHours(1) });
        P1ReceiptHttpResult retried = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken);
        retried.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task FinalCancellationRefusesAndDoesNotSeedHistory()
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var adapter = fixture.Adapter();
        adapter.BeforeFinalCommit = cancellation.Cancel;
        P1ReceiptHttpResult refused = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: cancellation.Token);
        refused.Failure.ShouldBe(P1ReceiptHttpFailure.Deadline);
        refused.Observation.ShouldBeNull();
        adapter.BeforeFinalCommit = null;
        fixture.ReceiptFrame = fixture.SignReceiptFrame(fixture.Expected);
        (await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken)).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task PendingBodyRefusesAfterBoottimeJump()
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        var clock = new P1TestBootTimeClock();
        fixture.ReceiptServer.BodyBytesToSend = 5;
        fixture.ReceiptServer.AfterPartialBody = async token =>
        {
            clock.Advance(TimeSpan.FromSeconds(301));
            await Task.Delay(Timeout.Infinite, token);
        };
        var adapter = new P1ReceiptHttpAdapter(fixture.Enrollment, fixture.ReceiptTrust, fixture.StatusTrust, clock);
        P1ReceiptHttpResult result = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);
        result.Failure.ShouldBe(P1ReceiptHttpFailure.Deadline);
        result.Observation.ShouldBeNull();
        fixture.StatusServer.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task CompleteBodyWaitingForFinalEofRefusesAtTenSecondDeadline()
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        var clock = new P1TestBootTimeClock();
        fixture.ReceiptServer.AfterPartialBody = async token =>
        {
            clock.Advance(TimeSpan.FromSeconds(11));
            await Task.Delay(Timeout.Infinite, token);
        };
        var adapter = new P1ReceiptHttpAdapter(fixture.Enrollment, fixture.ReceiptTrust, fixture.StatusTrust, clock);
        P1ReceiptHttpResult result = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);
        result.Failure.ShouldBe(P1ReceiptHttpFailure.Deadline);
        result.Observation.ShouldBeNull();
        fixture.StatusServer.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("connect")]
    [InlineData("tls")]
    [InlineData("headers")]
    [InlineData("partial-body")]
    public async Task PendingIoRefusesAtOwnTenSecondGetLimit(string stage)
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        var clock = new P1TestBootTimeClock();
        var adapter = new P1ReceiptHttpAdapter(fixture.Enrollment, fixture.ReceiptTrust, fixture.StatusTrust, clock);
        if (stage == "connect")
        {
            adapter.ConnectAsync = async (_, _, token) =>
            {
                clock.Advance(TimeSpan.FromSeconds(11));
                await Task.Delay(Timeout.Infinite, token);
                return new TcpClient();
            };
        }
        else
        {
            Func<CancellationToken, Task> stall = async token =>
            {
                clock.Advance(TimeSpan.FromSeconds(11));
                await Task.Delay(Timeout.Infinite, token);
            };
            if (stage == "tls") fixture.ReceiptServer.BeforeTlsHandshake = stall;
            else if (stage == "headers") fixture.ReceiptServer.BeforeResponse = stall;
            else
            {
                fixture.ReceiptServer.BodyBytesToSend = 5;
                fixture.ReceiptServer.AfterPartialBody = stall;
            }
        }

        P1ReceiptHttpResult result = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);
        result.Failure.ShouldBe(P1ReceiptHttpFailure.Deadline);
        result.Observation.ShouldBeNull();
        fixture.StatusServer.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task PendingStatusGetRefusesAtItsOwnTenSecondLimitWithoutHistoryCommit()
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        var clock = new P1TestBootTimeClock();
        fixture.StatusServer.BeforeResponse = async token =>
        {
            clock.Advance(TimeSpan.FromSeconds(11));
            await Task.Delay(Timeout.Infinite, token);
        };
        var adapter = new P1ReceiptHttpAdapter(fixture.Enrollment, fixture.ReceiptTrust, fixture.StatusTrust, clock);
        P1ReceiptHttpResult result = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);
        result.Failure.ShouldBe(P1ReceiptHttpFailure.Deadline);
        result.Observation.ShouldBeNull();
        fixture.ReceiptServer.Requests.Count.ShouldBe(1);
        fixture.StatusServer.Requests.Count.ShouldBe(1);
        fixture.StatusServer.BeforeResponse = null;
        P1ReceiptHttpResult retry = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken);
        retry.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task StatusGetReceivesFreshTenSecondAllowance()
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        var clock = new P1TestBootTimeClock();
        fixture.ReceiptServer.BeforeResponse = token =>
        {
            clock.Advance(TimeSpan.FromSeconds(6));
            return Task.CompletedTask;
        };
        fixture.StatusServer.BeforeResponse = token =>
        {
            clock.Advance(TimeSpan.FromSeconds(6));
            return Task.CompletedTask;
        };
        var adapter = new P1ReceiptHttpAdapter(fixture.Enrollment, fixture.ReceiptTrust, fixture.StatusTrust, clock);
        (await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken)).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task WholeChainDeadlineRefusesBetweenIndividuallyValidGetsWithoutSeedingHistory()
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        var clock = new P1TestBootTimeClock();
        var history = new P1ReceiptHttpHistory();
        var adapter = new P1ReceiptHttpAdapter(fixture.Enrollment, fixture.ReceiptTrust, fixture.StatusTrust,
            clock, history)
        {
            BeforeStatusGet = () => clock.Advance(TimeSpan.FromSeconds(301)),
        };

        P1ReceiptHttpResult result = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken);
        result.Failure.ShouldBe(P1ReceiptHttpFailure.Deadline);
        result.Observation.ShouldBeNull();
        fixture.ReceiptServer.Requests.Count.ShouldBe(1);
        fixture.StatusServer.Requests.ShouldBeEmpty();
        byte[] differentFrame = fixture.ReceiptFrame.ToArray();
        differentFrame[^1] ^= 1;
        history.Compare(fixture.Expected.ReceiptId, differentFrame).ShouldBe(P1ReceiptHttpFailure.None);
    }

    [Fact]
    public async Task PublicServerGuardRefusesLocalRootDirectlyAndInRealHandshake()
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        var clock = new P1BootTimeClock();
        clock.TryRead(out long start).ShouldBeTrue();
        await using (var deadline = new P1ReceiptHttpDeadline(clock, start, fixture.Now,
            TestContext.Current.CancellationToken))
        {
            fixture.ProductionReceiptTrust().ValidateServer(fixture.ReceiptServerCertificate, null,
                SslPolicyErrors.None, deadline).ShouldBeFalse();
        }

        P1ReceiptHttpTrust controlled = fixture.ProductionServerGuardTrust();
        int reached = 0;
        controlled.BeforeServerChainBuild = () => Interlocked.Increment(ref reached);
        var adapter = new P1ReceiptHttpAdapter(fixture.Enrollment, controlled, fixture.StatusTrust);
        P1ReceiptHttpResult result = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken);
        result.Succeeded.ShouldBeFalse();
        result.Observation.ShouldBeNull();
        reached.ShouldBeGreaterThan(0);
        fixture.ReceiptServer.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task MissingClientOfflineRevocationEvidenceRefusesOtherwiseValidChain()
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        var clock = new P1BootTimeClock();
        clock.TryRead(out long start).ShouldBeTrue();
        await using var deadline = new P1ReceiptHttpDeadline(clock, start, fixture.Now,
            TestContext.Current.CancellationToken);
        fixture.ReceiptTrust.ValidateClient(deadline).ShouldBeTrue();
        fixture.ProductionReceiptTrust().ValidateClient(deadline).ShouldBeFalse();
    }

    [Fact]
    public async Task OfflineCrlAndAiaUrlsNeverReceiveAConnection()
    {
        await using var trap = new P1NetworkTrap();
        await using var fixture = new P1LoopbackReceiptFixture(revocationUrl: trap.Url);
        fixture.ReceiptTrust.ClientCertificate.Extensions["2.5.29.31"]!.Format(true).ShouldContain(trap.Url);
        fixture.ReceiptTrust.ClientCertificate.Extensions["1.3.6.1.5.5.7.1.1"]!.Format(true).ShouldContain(trap.Url);
        var clock = new P1BootTimeClock();
        clock.TryRead(out long start).ShouldBeTrue();
        await using var deadline = new P1ReceiptHttpDeadline(clock, start, fixture.Now,
            TestContext.Current.CancellationToken);
        fixture.ProductionReceiptTrust().ValidateClient(deadline).ShouldBeFalse();
        await Task.Delay(100, TestContext.Current.CancellationToken);
        trap.Hits.ShouldBe(0);
        fixture.ReceiptServer.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ServerCrlAiaAndOcspUrlsNeverReceiveAConnection()
    {
        await using var trap = new P1NetworkTrap();
        await using var fixture = new P1LoopbackReceiptFixture(serverRevocationUrl: trap.Url);
        fixture.ReceiptServerCertificate.Extensions["2.5.29.31"]!.Format(true).ShouldContain(trap.Url);
        fixture.ReceiptServerCertificate.Extensions["1.3.6.1.5.5.7.1.1"]!.Format(true).ShouldContain(trap.Url);
        var aia = new System.Formats.Asn1.AsnReader(
            fixture.ReceiptServerCertificate.Extensions["1.3.6.1.5.5.7.1.1"]!.RawData,
            System.Formats.Asn1.AsnEncodingRules.DER).ReadSequence();
        var accessMethods = new List<string>();
        while (aia.HasData) accessMethods.Add(aia.ReadSequence().ReadObjectIdentifier());
        accessMethods.ShouldContain("1.3.6.1.5.5.7.48.1");
        P1ReceiptHttpTrust controlled = fixture.ProductionServerGuardTrust();
        int reached = 0;
        controlled.BeforeServerChainBuild = () => Interlocked.Increment(ref reached);
        var adapter = new P1ReceiptHttpAdapter(fixture.Enrollment, controlled, fixture.StatusTrust);
        P1ReceiptHttpResult result = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken);
        result.Observation.ShouldBeNull();
        reached.ShouldBeGreaterThan(0);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        trap.Hits.ShouldBe(0);
        fixture.ReceiptServer.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task StalledServerWorkerStopsTlsCallbackAndRetainsConcurrencySlot()
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        var clock = new P1TestBootTimeClock();
        using var gate = new ManualResetEventSlim(false);
        fixture.ReceiptTrust.BeforeServerChainBuild = () =>
        {
            clock.Advance(TimeSpan.FromSeconds(11));
            gate.Wait();
        };
        var adapter = new P1ReceiptHttpAdapter(fixture.Enrollment, fixture.ReceiptTrust, fixture.StatusTrust, clock);
        try
        {
            P1ReceiptHttpResult first = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
                cancellationToken: TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5),
                    TestContext.Current.CancellationToken);
            first.Failure.ShouldBe(P1ReceiptHttpFailure.Deadline);
            first.Observation.ShouldBeNull();
            fixture.ReceiptServer.Requests.ShouldBeEmpty();
            P1ReceiptHttpResult second = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
                cancellationToken: TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5),
                    TestContext.Current.CancellationToken);
            second.Succeeded.ShouldBeFalse();
            second.Observation.ShouldBeNull();
            fixture.ReceiptServer.Requests.ShouldBeEmpty();
        }
        finally
        {
            gate.Set();
        }
    }

    [Fact]
    public async Task StalledClientContextWorkerRefusesPromptlyAndRetainsSlot()
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        var clock = new P1TestBootTimeClock();
        using var gate = new ManualResetEventSlim(false);
        fixture.ReceiptTrust.ContextCreated = () =>
        {
            clock.Advance(TimeSpan.FromSeconds(11));
            gate.Wait();
        };
        var adapter = new P1ReceiptHttpAdapter(fixture.Enrollment, fixture.ReceiptTrust, fixture.StatusTrust, clock);
        try
        {
            P1ReceiptHttpResult first = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
                cancellationToken: TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5),
                    TestContext.Current.CancellationToken);
            first.Failure.ShouldBe(P1ReceiptHttpFailure.Deadline);
            first.Observation.ShouldBeNull();
            P1ReceiptHttpResult second = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
                cancellationToken: TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5),
                    TestContext.Current.CancellationToken);
            second.Observation.ShouldBeNull();
            fixture.ReceiptServer.Requests.ShouldBeEmpty();
        }
        finally
        {
            gate.Set();
        }
    }

    [Fact]
    public async Task ConcurrentTrustWorkersDoNotStarveBoottimeDeadline()
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        var clock = new P1TestBootTimeClock();
        using var gate = new ManualResetEventSlim(false);
        int entered = 0;
        Task<P1ReceiptHttpResult>[] attempts = new Task<P1ReceiptHttpResult>[12];
        try
        {
            for (int i = 0; i < attempts.Length; i++)
            {
                var trust = new P1ReceiptHttpTrust(fixture.ReceiptServer.Origin,
                    fixture.ReceiptTrust.ServerAnchor, null, fixture.ReceiptTrust.ClientCertificate,
                    fixture.ReceiptTrust.ClientIssuer, fixture.ReceiptTrust.ClientSpiffeId, null,
                    fixture.ReceiptTrust.ServerAnchor);
                trust.ContextCreated = () =>
                {
                    Interlocked.Increment(ref entered);
                    gate.Wait();
                };
                var adapter = new P1ReceiptHttpAdapter(fixture.Enrollment, trust, fixture.StatusTrust, clock);
                attempts[i] = adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
                    cancellationToken: TestContext.Current.CancellationToken);
            }

            for (int i = 0; i < 40 && Volatile.Read(ref entered) == 0; i++)
            {
                await Task.Delay(25, TestContext.Current.CancellationToken);
            }

            Volatile.Read(ref entered).ShouldBeGreaterThan(0);
            clock.Advance(TimeSpan.FromSeconds(11));
            P1ReceiptHttpResult[] results = await Task.WhenAll(attempts).WaitAsync(TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);
            results.All(result => result.Failure == P1ReceiptHttpFailure.Deadline && result.Observation is null).ShouldBeTrue();
            fixture.ReceiptServer.Requests.ShouldBeEmpty();
        }
        finally
        {
            gate.Set();
        }
    }

    [Fact]
    public void HistoryHasBothFrameAndIdLimitsWithoutEviction()
    {
        var history = new P1ReceiptHttpHistory();
        for (int i = 0; i < P1ReceiptHttpHistory.MaxIds; i++)
        {
            history.Commit(i.ToString("x64"), [1], () => true).ShouldBe(P1ReceiptHttpFailure.None);
        }

        history.Commit("new", [2], () => true).ShouldBe(P1ReceiptHttpFailure.HistoryFull);
        history.Commit("0".PadLeft(64, '0'), [1], () => true).ShouldBe(P1ReceiptHttpFailure.None);
        int existingGuardCalls = 0;
        history.Commit("0".PadLeft(64, '0'), [1], () => ++existingGuardCalls == 1)
            .ShouldBe(P1ReceiptHttpFailure.Deadline);
        existingGuardCalls.ShouldBe(2);
        history.Commit("0".PadLeft(64, '0'), [2], () => true).ShouldBe(P1ReceiptHttpFailure.ImmutableIdIncident);
        var byteHistory = new P1ReceiptHttpHistory();
        byteHistory.Commit("first", new byte[P1ReceiptHttpHistory.MaxFrameBytes], () => true).ShouldBe(P1ReceiptHttpFailure.None);
        byteHistory.Commit("second", [1], () => true).ShouldBe(P1ReceiptHttpFailure.HistoryFull);
        var rollback = new P1ReceiptHttpHistory();
        int guardCalls = 0;
        rollback.Commit("id", [1], () => ++guardCalls == 1).ShouldBe(P1ReceiptHttpFailure.Deadline);
        rollback.Commit("id", [2], () => true).ShouldBe(P1ReceiptHttpFailure.None);
    }

    [Fact]
    public async Task SharedMonitorCapsActiveDeadlinesAndReleasesRegistrations()
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        var clock = new P1TestBootTimeClock();
        clock.TryRead(out long start).ShouldBeTrue();
        int initial = P1ReceiptHttpDeadlineMonitor.ActiveCount;
        var registrations = new List<P1ReceiptHttpDeadline>();
        try
        {
            for (int index = initial; index < P1ReceiptHttpDeadlineMonitor.MaxActive; index++)
            {
                var deadline = new P1ReceiptHttpDeadline(clock, start, DateTimeOffset.UtcNow,
                    TestContext.Current.CancellationToken);
                deadline.Valid.ShouldBeTrue();
                registrations.Add(deadline);
            }

            P1ReceiptHttpDeadlineMonitor.ActiveCount.ShouldBe(P1ReceiptHttpDeadlineMonitor.MaxActive);
            await using var excess = new P1ReceiptHttpDeadline(clock, start, DateTimeOffset.UtcNow,
                TestContext.Current.CancellationToken);
            excess.Valid.ShouldBeFalse();
            excess.DeadlineFailed.ShouldBeTrue();
            P1ReceiptHttpDeadlineMonitor.ActiveCount.ShouldBe(P1ReceiptHttpDeadlineMonitor.MaxActive);
            var adapter = new P1ReceiptHttpAdapter(fixture.Enrollment, fixture.ReceiptTrust, fixture.StatusTrust, clock);
            P1ReceiptHttpResult refused = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
                cancellationToken: TestContext.Current.CancellationToken);
            refused.Failure.ShouldBe(P1ReceiptHttpFailure.Deadline);
            refused.Observation.ShouldBeNull();
            fixture.ReceiptServer.Requests.ShouldBeEmpty();
        }
        finally
        {
            foreach (P1ReceiptHttpDeadline deadline in registrations)
            {
                await deadline.DisposeAsync();
            }
        }

        P1ReceiptHttpDeadlineMonitor.ActiveCount.ShouldBe(initial);
        await using var retry = new P1ReceiptHttpDeadline(clock, start, DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);
        retry.Valid.ShouldBeTrue();
    }

    [Fact]
    public async Task AggregateHandshakeCapRefusesExcessTrustInstancesWithoutLeakingSlots()
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        var clock = new P1TestBootTimeClock();
        using var gate = new ManualResetEventSlim(false);
        int baseline = P1ReceiptHttpTrust.AvailableHandshakeSlots;
        baseline.ShouldBe(P1ReceiptHttpTrust.MaxConcurrentHandshakes);
        var attempts = new List<Task<P1ReceiptHttpResult>>();
        try
        {
            for (int index = 0; index < P1ReceiptHttpTrust.MaxConcurrentHandshakes; index++)
            {
                var trust = new P1ReceiptHttpTrust(fixture.ReceiptServer.Origin,
                    fixture.ReceiptTrust.ServerAnchor, null, fixture.ReceiptTrust.ClientCertificate,
                    fixture.ReceiptTrust.ClientIssuer, fixture.ReceiptTrust.ClientSpiffeId, null,
                    fixture.ReceiptTrust.ServerAnchor);
                trust.BeforeHandshake = gate.Wait;
                var adapter = new P1ReceiptHttpAdapter(fixture.Enrollment, trust, fixture.StatusTrust, clock);
                attempts.Add(adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
                    cancellationToken: TestContext.Current.CancellationToken));
            }

            for (int index = 0; index < 200 && P1ReceiptHttpTrust.AvailableHandshakeSlots != 0; index++)
            {
                await Task.Delay(25, TestContext.Current.CancellationToken);
            }

            P1ReceiptHttpTrust.AvailableHandshakeSlots.ShouldBe(0);
            var excessTrust = new P1ReceiptHttpTrust(fixture.ReceiptServer.Origin,
                fixture.ReceiptTrust.ServerAnchor, null, fixture.ReceiptTrust.ClientCertificate,
                fixture.ReceiptTrust.ClientIssuer, fixture.ReceiptTrust.ClientSpiffeId, null,
                fixture.ReceiptTrust.ServerAnchor);
            var excessAdapter = new P1ReceiptHttpAdapter(fixture.Enrollment, excessTrust, fixture.StatusTrust, clock);
            P1ReceiptHttpResult excess = await excessAdapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
                cancellationToken: TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5),
                    TestContext.Current.CancellationToken);
            excess.Failure.ShouldBe(P1ReceiptHttpFailure.Transport);
            excess.Observation.ShouldBeNull();

            clock.Advance(TimeSpan.FromSeconds(11));
            P1ReceiptHttpResult[] expired = await Task.WhenAll(attempts).WaitAsync(TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);
            expired.All(result => result.Failure == P1ReceiptHttpFailure.Deadline && result.Observation is null).ShouldBeTrue();
            P1ReceiptHttpTrust.AvailableHandshakeSlots.ShouldBe(0);
        }
        finally
        {
            gate.Set();
        }

        for (int index = 0; index < 200 && P1ReceiptHttpTrust.AvailableHandshakeSlots != baseline; index++)
        {
            await Task.Delay(25, TestContext.Current.CancellationToken);
        }

        P1ReceiptHttpTrust.AvailableHandshakeSlots.ShouldBe(baseline);
    }

    [Fact]
    public async Task OneFailedClockSamplePermanentlyInvalidatesThatDeadline()
    {
        var clock = new P1TestBootTimeClock();
        clock.TryRead(out long start).ShouldBeTrue();
        await using var deadline = new P1ReceiptHttpDeadline(clock, start, DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);
        deadline.Valid.ShouldBeTrue();
        clock.FailNextRead();
        deadline.Valid.ShouldBeFalse();
        deadline.DeadlineFailed.ShouldBeTrue();
        deadline.Valid.ShouldBeFalse();
    }

    [Fact]
    public async Task RequestDeadlineIsExclusiveAtTenSecondsAndBoottimeRollbackStaysFailed()
    {
        const long start = 1_000_000_000_000;
        var clock = new P1FixedBootTimeClock(start);
        await using (var deadline = new P1ReceiptHttpDeadline(clock, start, DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken))
        {
            clock.Set(start + 9_999_999_999);
            deadline.Valid.ShouldBeTrue();
            clock.Set(start + 10_000_000_000);
            deadline.Valid.ShouldBeFalse();
            deadline.DeadlineFailed.ShouldBeTrue();
        }

        clock.Set(start);
        await using var rollback = new P1ReceiptHttpDeadline(clock, start, DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);
        clock.Set(start + 1_000);
        rollback.Valid.ShouldBeTrue();
        clock.Set(start - 1);
        rollback.Valid.ShouldBeFalse();
        clock.Set(start + 2_000);
        rollback.Valid.ShouldBeFalse();
        rollback.DeadlineFailed.ShouldBeTrue();
    }

    [Fact]
    public async Task BlockedCancellationCallbackCannotFreezeOtherDeadline()
    {
        var firstClock = new P1TestBootTimeClock();
        var secondClock = new P1TestBootTimeClock();
        firstClock.TryRead(out long firstStart).ShouldBeTrue();
        secondClock.TryRead(out long secondStart).ShouldBeTrue();
        await using var first = new P1ReceiptHttpDeadline(firstClock, firstStart, DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);
        await using var second = new P1ReceiptHttpDeadline(secondClock, secondStart, DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);
        using var gate = new ManualResetEventSlim(false);
        using var entered = new ManualResetEventSlim(false);
        using var callback = first.Token.Register(() =>
        {
            entered.Set();
            gate.Wait();
        });
        try
        {
            firstClock.Advance(TimeSpan.FromSeconds(11));
            (await Task.Run(() => entered.Wait(TimeSpan.FromSeconds(5)), TestContext.Current.CancellationToken)).ShouldBeTrue();
            secondClock.Advance(TimeSpan.FromSeconds(11));
            for (int i = 0; i < 100 && !second.Token.IsCancellationRequested; i++)
            {
                await Task.Delay(20, TestContext.Current.CancellationToken);
            }
            second.Token.IsCancellationRequested.ShouldBeTrue();
        }
        finally
        {
            gate.Set();
        }
    }

    [Fact]
    public async Task AggregateValidationCapRetainsSlotsAfterTimedOutTrustWorkers()
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        var clock = new P1TestBootTimeClock();
        using var gate = new ManualResetEventSlim(false);
        int baseline = P1ReceiptHttpTrust.AvailableValidationSlots;
        baseline.ShouldBe(P1ReceiptHttpTrust.MaxConcurrentValidationWorkers);
        var attempts = new List<Task<P1ReceiptHttpResult>>();
        try
        {
            for (int i = 0; i < P1ReceiptHttpTrust.MaxConcurrentValidationWorkers; i++)
            {
                var trust = new P1ReceiptHttpTrust(fixture.ReceiptServer.Origin,
                    fixture.ReceiptTrust.ServerAnchor, null, fixture.ReceiptTrust.ClientCertificate,
                    fixture.ReceiptTrust.ClientIssuer, fixture.ReceiptTrust.ClientSpiffeId, null,
                    fixture.ReceiptTrust.ServerAnchor);
                trust.ContextCreated = gate.Wait;
                attempts.Add(new P1ReceiptHttpAdapter(fixture.Enrollment, trust, fixture.StatusTrust, clock)
                    .RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
                        cancellationToken: TestContext.Current.CancellationToken));
            }

            for (int i = 0; i < 200 && P1ReceiptHttpTrust.AvailableValidationSlots != 0; i++)
            {
                await Task.Delay(25, TestContext.Current.CancellationToken);
            }

            P1ReceiptHttpTrust.AvailableValidationSlots.ShouldBe(0);
            var excessTrust = new P1ReceiptHttpTrust(fixture.ReceiptServer.Origin,
                fixture.ReceiptTrust.ServerAnchor, null, fixture.ReceiptTrust.ClientCertificate,
                fixture.ReceiptTrust.ClientIssuer, fixture.ReceiptTrust.ClientSpiffeId, null,
                fixture.ReceiptTrust.ServerAnchor);
            P1ReceiptHttpResult excess = await new P1ReceiptHttpAdapter(fixture.Enrollment, excessTrust,
                fixture.StatusTrust, clock).RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
                    cancellationToken: TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5),
                        TestContext.Current.CancellationToken);
            excess.Failure.ShouldBe(P1ReceiptHttpFailure.Transport);
            excess.Observation.ShouldBeNull();
            clock.Advance(TimeSpan.FromSeconds(11));
            P1ReceiptHttpResult[] timedOut = await Task.WhenAll(attempts).WaitAsync(TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);
            timedOut.All(result => result.Failure == P1ReceiptHttpFailure.Deadline && result.Observation is null).ShouldBeTrue();
            P1ReceiptHttpTrust.AvailableValidationSlots.ShouldBe(0);
        }
        finally
        {
            gate.Set();
        }

        for (int i = 0; i < 200 && P1ReceiptHttpTrust.AvailableValidationSlots != baseline; i++)
        {
            await Task.Delay(25, TestContext.Current.CancellationToken);
        }
        P1ReceiptHttpTrust.AvailableValidationSlots.ShouldBe(baseline);
    }

    [Fact]
    public async Task ConcurrentValidFramesForOneIdHaveOneWinnerAndOneIncident()
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        byte[] firstFrame = fixture.SignReceiptFrame(fixture.Expected);
        byte[] secondFrame = fixture.SignReceiptFrame(fixture.Expected);
        firstFrame.SequenceEqual(secondFrame).ShouldBeFalse();
        fixture.ReceiptFrameQueue = new System.Collections.Concurrent.ConcurrentQueue<byte[]>([firstFrame, secondFrame]);
        var history = new P1ReceiptHttpHistory();
        P1ReceiptHttpTrust secondReceiptTrust = fixture.PinOnlyReceiptTrust();
        P1ReceiptHttpTrust secondStatusTrust = new(fixture.StatusTrust.Origin,
            fixture.StatusTrust.ServerAnchor, null, fixture.StatusTrust.ClientCertificate,
            fixture.StatusTrust.ClientIssuer, fixture.StatusTrust.ClientSpiffeId, null,
            fixture.StatusTrust.ServerAnchor);
        var firstAdapter = new P1ReceiptHttpAdapter(fixture.Enrollment, fixture.ReceiptTrust,
            fixture.StatusTrust, new P1BootTimeClock(), history);
        var secondAdapter = new P1ReceiptHttpAdapter(fixture.Enrollment, secondReceiptTrust,
            secondStatusTrust, new P1BootTimeClock(), history);
        P1ReceiptHttpResult[] results = await Task.WhenAll(
            firstAdapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
                cancellationToken: TestContext.Current.CancellationToken),
            secondAdapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
                cancellationToken: TestContext.Current.CancellationToken));
        results.Count(result => result.Succeeded).ShouldBe(1);
        results.Count(result => result.Failure == P1ReceiptHttpFailure.ImmutableIdIncident).ShouldBe(1);
    }

    [Fact]
    public async Task TransientClockFailureAtCommitRefusesAndDoesNotSeedHistory()
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        var clock = new P1TestBootTimeClock();
        var adapter = new P1ReceiptHttpAdapter(fixture.Enrollment, fixture.ReceiptTrust, fixture.StatusTrust, clock);
        adapter.BeforeFinalCommit = clock.FailNextRead;
        P1ReceiptHttpResult refused = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken);
        refused.Failure.ShouldBe(P1ReceiptHttpFailure.Deadline);
        refused.Observation.ShouldBeNull();

        adapter.BeforeFinalCommit = null;
        fixture.ReceiptFrame = fixture.SignReceiptFrame(fixture.Expected);
        P1ReceiptHttpResult retry = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken);
        retry.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task IoErrorAfterBoottimeSignalIsDeadline()
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        var clock = new P1TestBootTimeClock();
        var adapter = new P1ReceiptHttpAdapter(fixture.Enrollment, fixture.ReceiptTrust, fixture.StatusTrust, clock);
        adapter.BeforeFinalCommit = () =>
        {
            clock.Advance(TimeSpan.FromSeconds(11));
            Thread.Sleep(100);
            throw new IOException("The transport failed after the boottime signal.");
        };
        P1ReceiptHttpResult result = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);
        result.Failure.ShouldBe(P1ReceiptHttpFailure.Deadline);
        result.Observation.ShouldBeNull();
        adapter.BeforeFinalCommit = null;
        fixture.ReceiptFrame = fixture.SignReceiptFrame(fixture.Expected);
        (await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken)).Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData("receipt-envelope", P1ReceiptHttpFailure.Transport)]
    [InlineData("receipt-signature", P1ReceiptHttpFailure.Authority)]
    [InlineData("status-envelope", P1ReceiptHttpFailure.Transport)]
    [InlineData("status-signature", P1ReceiptHttpFailure.Authority)]
    public async Task EnvelopeAndAuthorityRefusalsHaveDistinctFailureCodes(string mode, P1ReceiptHttpFailure failure)
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        if (mode.StartsWith("receipt", StringComparison.Ordinal))
        {
            fixture.ReceiptFrame = fixture.ReceiptFrame.ToArray();
            fixture.ReceiptFrame[mode == "receipt-envelope" ? 0 : ^1] ^= 1;
        }
        else
        {
            fixture.StatusFrameMutation = frame =>
            {
                byte[] changed = frame.ToArray();
                changed[mode == "status-envelope" ? 0 : ^1] ^= 1;
                return changed;
            };
        }

        P1ReceiptHttpResult result = await fixture.Adapter().RetrieveAsync(fixture.Expected, fixture.Subject,
            fixture.Now, cancellationToken: TestContext.Current.CancellationToken);
        result.Failure.ShouldBe(failure);
        result.Observation.ShouldBeNull();
    }

    [Theory]
    [InlineData("HTTP/1.1 200", "")]
    [InlineData("HTTP/1.1 200 OK", "X-Unknown: value\u007f\r\n")]
    public async Task MalformedStatusOrDelHeaderRefusesBeforeStatus(string statusLine, string extraHeader)
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        fixture.ReceiptServer.StatusLine = statusLine;
        fixture.ReceiptServer.ExtraHeaders = extraHeader;
        P1ReceiptHttpResult result = await fixture.Adapter().RetrieveAsync(fixture.Expected, fixture.Subject,
            fixture.Now, cancellationToken: TestContext.Current.CancellationToken);
        result.Failure.ShouldBe(P1ReceiptHttpFailure.Transport);
        result.Observation.ShouldBeNull();
        fixture.StatusServer.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task RootSpkiPinSucceedsForServerChainElement()
    {
        await using var fixture = new P1LoopbackReceiptFixture();
        var adapter = new P1ReceiptHttpAdapter(fixture.Enrollment, fixture.RootPinReceiptTrust(), fixture.StatusTrust);
        P1ReceiptHttpResult result = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken);
        result.Succeeded.ShouldBeTrue();
        result.Observation!.StatusFrame.ShouldBe(fixture.LastStatusFrame);
    }

    [Fact]
    public async Task RsaServerSpkiPinSucceeds()
    {
        await using var fixture = new P1LoopbackReceiptFixture(rsaServer: true);
        var adapter = new P1ReceiptHttpAdapter(fixture.Enrollment, fixture.PinOnlyReceiptTrust(), fixture.StatusTrust);
        P1ReceiptHttpResult result = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken);
        result.Succeeded.ShouldBeTrue();
    }

    private static string Digest(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
