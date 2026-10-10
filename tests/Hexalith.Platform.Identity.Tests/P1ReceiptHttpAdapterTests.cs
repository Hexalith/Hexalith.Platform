using System.Security.Cryptography;
using System.Net.Security;
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
        fixture.StatusServer.Requests.Single().Path.ShouldBe("/v1/status/" + fixture.Expected.ReceiptId
            + "?nonce=" + first.Observation.Evidence.RequestNonce);

        first.Observation.ReceiptFrame[0] ^= 1;
        first.Observation.StatusFrame[0] ^= 1;
        first.Observation.SubjectBytes[0] ^= 1;
        first.Observation.ReceiptFrame.ShouldBe(fixture.ReceiptFrame);
        first.Observation.StatusFrame.ShouldBe(P1SignedEnvelopeV1.Encode(status));
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
        fixture.ReceiptServer.BeforeResponse = async token =>
        {
            clock.Advance(TimeSpan.FromSeconds(301));
            await Task.Delay(Timeout.Infinite, token);
        };
        var adapter = new P1ReceiptHttpAdapter(fixture.Enrollment, fixture.ReceiptTrust, fixture.StatusTrust, clock);
        P1ReceiptHttpResult result = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
            cancellationToken: TestContext.Current.CancellationToken);
        result.Failure.ShouldBe(P1ReceiptHttpFailure.Deadline);
        result.Observation.ShouldBeNull();
        fixture.StatusServer.Requests.ShouldBeEmpty();
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
                cancellationToken: TestContext.Current.CancellationToken);
            first.Failure.ShouldBe(P1ReceiptHttpFailure.Deadline);
            first.Observation.ShouldBeNull();
            fixture.ReceiptServer.Requests.ShouldBeEmpty();
            P1ReceiptHttpResult second = await adapter.RetrieveAsync(fixture.Expected, fixture.Subject, fixture.Now,
                cancellationToken: TestContext.Current.CancellationToken);
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
    public void HistoryHasBothFrameAndIdLimitsWithoutEviction()
    {
        var history = new P1ReceiptHttpHistory();
        for (int i = 0; i < P1ReceiptHttpHistory.MaxIds; i++)
        {
            history.Commit(i.ToString("x64"), [1], () => true).ShouldBe(P1ReceiptHttpFailure.None);
        }

        history.Commit("new", [2], () => true).ShouldBe(P1ReceiptHttpFailure.HistoryFull);
        history.Commit("0".PadLeft(64, '0'), [1], () => true).ShouldBe(P1ReceiptHttpFailure.None);
        history.Commit("0".PadLeft(64, '0'), [2], () => true).ShouldBe(P1ReceiptHttpFailure.ImmutableIdIncident);
        var byteHistory = new P1ReceiptHttpHistory();
        byteHistory.Commit("first", new byte[P1ReceiptHttpHistory.MaxFrameBytes], () => true).ShouldBe(P1ReceiptHttpFailure.None);
        byteHistory.Commit("second", [1], () => true).ShouldBe(P1ReceiptHttpFailure.HistoryFull);
        var rollback = new P1ReceiptHttpHistory();
        int guardCalls = 0;
        rollback.Commit("id", [1], () => ++guardCalls == 1).ShouldBe(P1ReceiptHttpFailure.Deadline);
        rollback.Commit("id", [2], () => true).ShouldBe(P1ReceiptHttpFailure.None);
    }

    private static string Digest(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
