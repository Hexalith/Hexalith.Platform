using System.Text;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Custody.Tests;

/// <summary>Actual spool and HMAC source with synthetic durability/current enrollment; no runtime replication or Agents SecurityEventLog qualification.</summary>
public sealed class PlatformSecurityDenialRecorderTests
{
    /// <summary>Only authenticated human/Workflow origin determines tenant routing; claimed foreign targets and Platform/unknown route safely.</summary>
    [Theory]
    [InlineData("human", "tenant-a")][InlineData("workflow", "tenant-a")][InlineData("platform", "system")][InlineData("forged", "system")][InlineData("unknown", "system")]
    public async Task RoutingUsesAuthenticatedOriginAndRetainedReceiptDigest(string kind, string routing)
    {
        var fixture = new SecuritySpoolFixture(); var profiles = new CustodyFixtureProfileProvider(fixture.Clock); var keys = new CustodyFixtureKeyProvider(fixture.Clock);
        var auth = new TrustedEnvelopeAuthenticator(keys, profiles, fixture.Clock); var digests = new PlatformHmacService(keys, profiles, fixture.Clock);
        var principal = kind switch
        {
            "workflow" => new TrustedPrincipal(TrustedPrincipalKind.Workflow, null, null, null, null, null, "SystemTimer", "workflow", "tick", null),
            "platform" => new TrustedPrincipal(TrustedPrincipalKind.Platform, "system", "actor", null, null, "roles", null, null, null, null),
            _ => new TrustedPrincipal(TrustedPrincipalKind.User, "tenant-a", "actor", "party", 1, "roles", null, null, null, null),
        };
        var identity = new TrustedEnvelopeIdentity(1, "fixture-r1", "fixture-issuer", principal, "Contracts.Exact", "Exact", "tenant-a", "resource", "c", "c",
            ["logical"], "fingerprint", "digest-v1", "fixture-audience", "logical");
        var envelope = (await auth.IssueAsync(identity, TimeSpan.FromMinutes(2), TestContext.Current.CancellationToken)).Envelope!;
        if (kind == "forged") { envelope = envelope with { Identity = envelope.Identity with { TargetTenantId = "client-claimed-foreign" } }; }
        var recorder = new PlatformSecurityDenialRecorder(auth, digests, fixture.Spool, fixture.Clock);
        var first = await recorder.ObserveAsync("retained-server-receipt", "invalid-tag", ["untrusted-personal-secret"], "key-v1",
            kind == "unknown" ? null : envelope, kind == "unknown" ? null : identity, TestContext.Current.CancellationToken);
        first!.Intent.RoutingTenantId.ShouldBe(routing); first.Intent.DigestKeyVersion.ShouldBe("key-v1");
        fixture.Clock.Now += TimeSpan.FromSeconds(1); keys.CurrentVersion = "key-v2";
        var retry = await recorder.ObserveAsync("retained-server-receipt", "invalid-tag", ["untrusted-personal-secret"], "key-v1",
            kind == "unknown" ? null : envelope, kind == "unknown" ? null : identity, TestContext.Current.CancellationToken);
        retry.ShouldBe(first); fixture.Read()!.Records.Count.ShouldBe(1);
        Encoding.UTF8.GetString(fixture.Persisted!).ShouldNotContain("untrusted-personal-secret");
        Encoding.UTF8.GetString(fixture.Persisted!).ShouldNotContain("client-claimed-foreign");
    }

    /// <summary>Missing replication authority and missing original digest key return no classified-denial evidence.</summary>
    [Fact]
    public async Task UnavailableSpoolOrOriginalKeyNeverReportsProcessedDenial()
    {
        var fixture = new SecuritySpoolFixture(); var profiles = new CustodyFixtureProfileProvider(fixture.Clock); var keys = new CustodyFixtureKeyProvider(fixture.Clock);
        var auth = new TrustedEnvelopeAuthenticator(keys, profiles, fixture.Clock); var recorder = new PlatformSecurityDenialRecorder(auth,
            new(keys, profiles, fixture.Clock), fixture.Spool, fixture.Clock);
        (await recorder.ObserveAsync("server-receipt", "invalid-tag", ["secret"], "unknown-version", cancellationToken: TestContext.Current.CancellationToken)).ShouldBeNull();
        fixture.Persisted.ShouldBeNull();
        fixture.Authority.GetCurrentAsync(Arg.Any<CancellationToken>()).Returns((ReplicatedSecuritySpoolTarget?)null);
        (await recorder.ObserveAsync("server-receipt", "invalid-tag", ["secret"], "key-v1", cancellationToken: TestContext.Current.CancellationToken)).ShouldBeNull();
        fixture.Persisted.ShouldBeNull();
    }

    /// <summary>Current origin authentication can expire after a first observation; the same retained server receipt still resolves its original authenticated routing and identity.</summary>
    [Fact]
    public async Task ExpiredOriginRetryPreservesOriginalAuthenticatedRouting()
    {
        var fixture = new SecuritySpoolFixture(); var profiles = new CustodyFixtureProfileProvider(fixture.Clock); var keys = new CustodyFixtureKeyProvider(fixture.Clock);
        var auth = new TrustedEnvelopeAuthenticator(keys, profiles, fixture.Clock); var recorder = new PlatformSecurityDenialRecorder(auth, new(keys, profiles, fixture.Clock), fixture.Spool, fixture.Clock);
        var principal = new TrustedPrincipal(TrustedPrincipalKind.User, "tenant-a", "actor", "party", 1, "roles", null, null, null, null);
        var identity = new TrustedEnvelopeIdentity(1, "fixture-r1", "fixture-issuer", principal, "Contracts.Exact", "Exact", "tenant-a", "resource", "c", "c", ["logical"], "fingerprint", "digest-v1", "fixture-audience", "logical");
        var envelope = (await auth.IssueAsync(identity, TimeSpan.FromMinutes(2), TestContext.Current.CancellationToken)).Envelope!;
        var original = await recorder.ObserveAsync("original-server-receipt", "invalid-tag", ["secret"], "key-v1", envelope, identity, TestContext.Current.CancellationToken);
        original!.Intent.RoutingTenantId.ShouldBe("tenant-a"); fixture.Clock.Now += TimeSpan.FromMinutes(3);
        (await auth.VerifyAsync(envelope, identity, TestContext.Current.CancellationToken)).Status.ShouldNotBe(CustodyStatus.Succeeded);
        (await recorder.ObserveAsync("original-server-receipt", "invalid-tag", ["secret"], "key-v1", envelope, identity, TestContext.Current.CancellationToken)).ShouldBe(original);
        fixture.Read()!.Records.Count.ShouldBe(1);
        (await recorder.ObserveAsync("original-server-receipt", "invalid-tag", ["changed-secret"], "key-v1", envelope, identity, TestContext.Current.CancellationToken)).ShouldBeNull();
        fixture.Read()!.Records.Count.ShouldBe(1);
        fixture.Authority.AuthorizeOriginalLookupAsync(fixture.Target, Arg.Any<SecurityObservationOriginalLookup>(), Arg.Any<CancellationToken>()).Returns(false);
        (await recorder.ObserveAsync("original-server-receipt", "invalid-tag", ["secret"], "key-v1", envelope, identity, TestContext.Current.CancellationToken)).ShouldBeNull();
        fixture.Read()!.Records.Count.ShouldBe(1);
    }

    /// <summary>An expired authenticated origin replays the exact original through the immutable archive lookup after head rollover.</summary>
    [Fact]
    public async Task ExpiredOriginReplayFindsArchivedOriginalWithoutNewRoutingDecision()
    {
        var fixture = new SecuritySpoolFixture(); var profiles = new CustodyFixtureProfileProvider(fixture.Clock); var keys = new CustodyFixtureKeyProvider(fixture.Clock);
        var auth = new TrustedEnvelopeAuthenticator(keys, profiles, fixture.Clock);
        var recorder = new PlatformSecurityDenialRecorder(auth, new(keys, profiles, fixture.Clock), fixture.Spool, fixture.Clock);
        var principal = new TrustedPrincipal(TrustedPrincipalKind.User, "tenant-a", "actor", "party", 1, "roles", null, null, null, null);
        var identity = new TrustedEnvelopeIdentity(1, "fixture-r1", "fixture-issuer", principal, "Contracts.Exact", "Exact", "tenant-a", "resource", "c", "c", ["logical"], "fingerprint", "digest-v1", "fixture-audience", "logical");
        var envelope = (await auth.IssueAsync(identity, TimeSpan.FromMinutes(2), TestContext.Current.CancellationToken)).Envelope!;
        var original = await recorder.ObserveAsync("archived-server-receipt", "invalid-tag", ["secret"], "key-v1", envelope, identity, TestContext.Current.CancellationToken);
        original.ShouldNotBeNull(); (await fixture.Spool.DrainAsync(1, TestContext.Current.CancellationToken)).ShouldBe(1);
        var acknowledged = fixture.Read()!; var exact = acknowledged.Records.Single(); exact.Receipt.ShouldNotBeNull();
        var page = new SecuritySpoolArchivePage(0, acknowledged, null);
        string digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(page)));
        var head = acknowledged with { Revision = acknowledged.Revision + 1, PageIndex = 1, ArchivedObservedCount = 1,
            ArchivedAcknowledgedCount = 1, ArchiveHeadDigest = digest, Records = [] };
        fixture.SetArchive(0, System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(page));
        fixture.Persisted = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(head); fixture.Anchor = head.Revision;
        fixture.AnchorDigest = ReplicatedSecurityObservationSpool.StateDigest(head);
        fixture.Clock.Now += TimeSpan.FromMinutes(3);
        (await auth.VerifyAsync(envelope, identity, TestContext.Current.CancellationToken)).Status.ShouldNotBe(CustodyStatus.Succeeded);
        var restarted = new PlatformSecurityDenialRecorder(auth, new(keys, profiles, fixture.Clock),
            new ReplicatedSecurityObservationSpool(fixture.Client, fixture.Clock, fixture.Authority, fixture.Recorder), fixture.Clock);
        (await restarted.ObserveAsync("archived-server-receipt", "invalid-tag", ["secret"], "key-v1", envelope, identity, TestContext.Current.CancellationToken)).ShouldBe(exact);
        (await restarted.ObserveAsync("archived-server-receipt", "invalid-tag", ["changed"], "key-v1", envelope, identity, TestContext.Current.CancellationToken)).ShouldBeNull();
        fixture.Read()!.Records.ShouldBeEmpty(); fixture.Archives.Count.ShouldBe(1); fixture.PhysicalAppends.ShouldBe(1);
    }

    /// <summary>Independent first observations bind the full retained receipt, safe reason, original authenticated route and keyed fields; receipt-only identity cannot pass these vectors.</summary>
    [Theory]
    [InlineData("route")][InlineData("reason")][InlineData("fields")]
    public async Task FirstObservationIdentityIncludesOriginalRouteReasonAndKeyedFields(string vector)
    {
        async Task<SecurityObservationRecord> ObserveAsync(bool altered)
        {
            var fixture = new SecuritySpoolFixture(); var profiles = new CustodyFixtureProfileProvider(fixture.Clock); var keys = new CustodyFixtureKeyProvider(fixture.Clock);
            var auth = new TrustedEnvelopeAuthenticator(keys, profiles, fixture.Clock); var recorder = new PlatformSecurityDenialRecorder(auth, new(keys, profiles, fixture.Clock), fixture.Spool, fixture.Clock);
            var principal = new TrustedPrincipal(TrustedPrincipalKind.User, "tenant-a", "actor", "party", 1, "roles", null, null, null, null);
            var identity = new TrustedEnvelopeIdentity(1, "fixture-r1", "fixture-issuer", principal, "Contracts.Exact", "Exact", "tenant-a", "resource", "c", "c", ["logical"], "fingerprint", "digest-v1", "fixture-audience", "logical");
            var envelope = (await auth.IssueAsync(identity, TimeSpan.FromMinutes(2), TestContext.Current.CancellationToken)).Envelope!;
            return (await recorder.ObserveAsync("same-retained-server-receipt", altered && vector == "reason" ? "scope-mismatch" : "invalid-tag",
                [altered && vector == "fields" ? "different-secret" : "original-secret"], "key-v1", altered && vector == "route" ? null : envelope,
                altered && vector == "route" ? null : identity, TestContext.Current.CancellationToken))!;
        }
        var first = await ObserveAsync(false); var other = await ObserveAsync(true);
        first.Intent.RetainedServerReceiptKey.ShouldBe(other.Intent.RetainedServerReceiptKey); first.Intent.ObservationId.ShouldNotBe(other.Intent.ObservationId);
    }
}
