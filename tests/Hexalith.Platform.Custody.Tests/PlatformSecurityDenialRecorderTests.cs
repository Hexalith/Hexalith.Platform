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
}
