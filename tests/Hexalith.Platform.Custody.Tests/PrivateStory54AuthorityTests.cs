using System.Security.Cryptography;
using Hexalith.EventStore.Contracts.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Custody.Tests;

/// <summary>Supplied Story 5.4 credentials sign, deny, and recover a lost spool acknowledgement without a deployed authority.</summary>
public sealed class PrivateStory54AuthorityTests
{
    [Fact]
    public void SuppliedP256CredentialSignsOnceAndChangedOrMissingCredentialsWriteNothing()
    {
        using ECDsa key = P256();
        var authority = PrivateCustodyBindingAuthority.Create(key, "story-issuer", "story-audience", "tenant-a", "story-authority", 1);
        authority.ShouldNotBeNull();
        var payload = Payload(authority);
        string? first = authority.Sign(payload, key);
        first.ShouldNotBeNull();
        authority.Verify(payload, first, key).ShouldBeTrue();
        authority.Sign(payload, key).ShouldBe(first);
        using ECDsa other = P256();
        authority.Sign(payload, other).ShouldBeNull();
        authority.Sign(payload with { Issuer = "other-issuer" }, key).ShouldBeNull();
        authority.Sign(payload with { Audience = "other-audience" }, key).ShouldBeNull();
        authority.Sign(payload with { TenantId = "tenant-b" }, key).ShouldBeNull();
        authority.Sign(payload, key).ShouldBe(first);
        authority.AsStale().Sign(payload, key).ShouldBeNull();
        PrivateCustodyBindingAuthority.Create(null, "story-issuer", "story-audience", "tenant-a", "story-authority", 1).ShouldBeNull();
        PrivateCustodyBindingAuthority.Create(key, "story-issuer", "story-audience", "tenant-a", "story-authority", 0).ShouldBeNull();
        byte[] bound = authority.RequestFingerprint("destroy-manifest", "reservation", new string('A', 64));
        authority.BindRequest("destroy-manifest", "reservation", new string('A', 64)).ShouldBeTrue();
        authority.AuthorizesExact("story-issuer", "story-audience", "tenant-a", "story-authority", 1, "destroy-manifest", "reservation", new string('A', 64), bound).ShouldBeTrue();
        authority.AuthorizesExact("other-issuer", "story-audience", "tenant-a", "story-authority", 1, "destroy-manifest", "reservation", new string('A', 64), bound).ShouldBeFalse();
        authority.AuthorizesExact("story-issuer", "story-audience", "tenant-a", "story-authority", 2, "destroy-manifest", "reservation", new string('A', 64), bound).ShouldBeFalse();
        authority.AuthorizesExact("story-issuer", "story-audience", "tenant-a", "story-authority", 1, "destroy-manifest", "reservation", new string('A', 64), [1, 2, 3]).ShouldBeFalse();
        var changed = PrivateCustodyBindingAuthority.Create(other, "story-issuer", "story-audience", "tenant-a", "story-authority", 1);
        changed.ShouldNotBeNull();
        changed.AuthorizesExact("story-issuer", "story-audience", "tenant-a", "story-authority", 1, "destroy-manifest", "reservation", new string('A', 64), bound).ShouldBeFalse();
        changed.Sign(payload, other).ShouldNotBeNull();
        authority.Sign(payload, key).ShouldBe(first);
    }

    [Fact]
    public async Task NullEndpointOrStaleSpoolAuthorityWritesNothing()
    {
        var fixture = new SecuritySpoolFixture();
        PrivateReplicatedSpoolAuthority.Create(null, fixture.Target, fixture.Clock).ShouldBeNull();
        PrivateReplicatedSpoolAuthority.Create("https://192.168.1.30:3500", fixture.Target, fixture.Clock).ShouldBeNull();
        PrivateReplicatedSpoolAuthority.Create(fixture.Target.ComponentName, fixture.Target, fixture.Clock).ShouldBeNull();
        var bare = PlatformPrivateCapabilityComposition.CapturePrivateSpool(fixture.Client, fixture.Clock, null, fixture.Recorder);
        (await bare.ObserveAsync(SecuritySpoolFixture.Intent(), TestContext.Current.CancellationToken)).ShouldBeNull();
        (await bare.DrainAsync(10, TestContext.Current.CancellationToken)).ShouldBe(0);
        fixture.Persisted.ShouldBeNull();
        fixture.PhysicalAppends.ShouldBe(0);
        var authority = PrivateReplicatedSpoolAuthority.Create("file:/mounted/spool-credential", fixture.Target, fixture.Clock);
        authority.ShouldNotBeNull();
        var stale = PlatformPrivateCapabilityComposition.CapturePrivateSpool(fixture.Client, fixture.Clock, authority.AsStale(), fixture.Recorder);
        (await stale.ObserveAsync(SecuritySpoolFixture.Intent(), TestContext.Current.CancellationToken)).ShouldBeNull();
        fixture.Persisted.ShouldBeNull();
        (await authority.AuthorizeAsync(fixture.Target with { AuthorityRevision = "stale-revision" }, "Observe", SecuritySpoolFixture.Intent(), TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await authority.AuthorizeAsync(fixture.Target, "Drain", null, TestContext.Current.CancellationToken)).ShouldBeTrue();
        var record = new SecurityObservationRecord(SecuritySpoolFixture.Intent(), fixture.Clock.Now, "2026-10-06", 1, null);
        (await authority.AuthorizeAgedOriginalRecoveryAsync(fixture.Target, record, new(SecurityEventRecorderLookupState.Unknown), TestContext.Current.CancellationToken)).ShouldBeNull();
        SecurityObservationRecoveryGrant? grant = await authority.AuthorizeAgedOriginalRecoveryAsync(fixture.Target, record, new(SecurityEventRecorderLookupState.NotRecorded), TestContext.Current.CancellationToken);
        grant.ShouldNotBeNull();
        (await authority.VerifyAgedOriginalRecoveryAsync(grant, new(SecurityEventRecorderLookupState.Unknown), TestContext.Current.CancellationToken)).ShouldBeFalse();
        var services = new ServiceCollection();
        services.AddPrivateSecurityObservationDrainWorker(bare, TimeSpan.FromHours(1), 1);
        services.ShouldNotContain(descriptor => descriptor.ServiceType == typeof(ReplicatedSecurityObservationSpool));
        services.ShouldNotContain(descriptor => descriptor.ServiceType == typeof(IReplicatedSecuritySpoolAuthority));
        services.ShouldNotContain(descriptor => descriptor.ServiceType == typeof(ReplicatedSecurityObservationSpoolWorker));
        services.ShouldNotContain(descriptor => descriptor.ServiceType == typeof(DeletionCapabilitySigningActor));
    }

    [Fact]
    public async Task LostSpoolAcknowledgementStaysPendingUntilTheWorkerRestarts()
    {
        var fixture = new SecuritySpoolFixture { LoseAppendAcknowledgement = true };
        var authority = PrivateReplicatedSpoolAuthority.Create("file:/mounted/spool-credential", fixture.Target, fixture.Clock);
        authority.ShouldNotBeNull();
        var spool = PlatformPrivateCapabilityComposition.CapturePrivateSpool(fixture.Client, fixture.Clock, authority, fixture.Recorder);
        int phase = 0;
        fixture.Recorder.LookupAsync(Arg.Any<SecurityObservationRecord>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var record = call.Arg<SecurityObservationRecord>();
            if (phase == 0)
            {
                phase = 1;
                return new SecurityEventRecorderLookup(SecurityEventRecorderLookupState.NotRecorded);
            }
            if (phase == 1) { return new SecurityEventRecorderLookup(SecurityEventRecorderLookupState.Unknown); }
            return fixture.Recorded.TryGetValue(record.Intent.ObservationId, out SecurityEventRecordReceipt? receipt)
                ? new SecurityEventRecorderLookup(SecurityEventRecorderLookupState.Recorded, receipt)
                : new SecurityEventRecorderLookup(SecurityEventRecorderLookupState.Unknown);
        });
        (await spool.ObserveAsync(SecuritySpoolFixture.Intent(), TestContext.Current.CancellationToken)).ShouldNotBeNull();
        (await spool.DrainAsync(10, TestContext.Current.CancellationToken)).ShouldBe(0);
        fixture.Read()!.Records.Single().Receipt.ShouldBeNull();
        fixture.PhysicalAppends.ShouldBe(1);
        phase = 2;
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddPrivateSecurityObservationDrainWorker(spool, TimeSpan.FromMilliseconds(10), 1);
        using ServiceProvider provider = services.BuildServiceProvider();
        provider.GetService<ReplicatedSecurityObservationSpool>().ShouldBeNull();
        IHostedService worker = provider.GetServices<IHostedService>().Single();
        await worker.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            DateTime until = DateTime.UtcNow.AddSeconds(5);
            while (fixture.Read()!.Records.Single().Receipt is null && DateTime.UtcNow < until)
            { await Task.Delay(10, TestContext.Current.CancellationToken); }
            fixture.Read()!.Records.Single().Receipt.ShouldNotBeNull();
            fixture.PhysicalAppends.ShouldBe(1);
        }
        finally { await worker.StopAsync(TestContext.Current.CancellationToken); }
    }

    private static ECDsa P256()
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return key;
    }

    private static DeletionBatchCapabilityV1 Payload(PrivateCustodyBindingAuthority authority)
        => new(authority.Issuer, authority.Audience, authority.TenantId, "delete-request", "seal", "manifest", 1, "batch-1", new string('A', 64), "guard", 1, 1, 1, authority.KeyVersion);
}
