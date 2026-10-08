using System.Reflection;
using System.Text.Json;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Testing.Fakes;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Custody.Tests;

/// <summary>Actual delivery ledger persistence/restart/loss schedules with synthetic direct-provider/current authority ports, never real key release.</summary>
public sealed class ExportKeyDeliveryActorTests
{
    private static ExportKeyDeliveryIdentity Identity(CustodyFixtureClock clock) => new("tenant-a", "delivery-1", "export-1", 7,
        "actor-1", "export-download", "principal-direct-channel", clock.Now.AddMinutes(1), "lifecycle-v1", "store-v1", 1);
    private static ExportKeyDeliveryActor Actor(ExportKeyDeliveryIdentity identity, IActorStateManager backend, CustodyFixtureClock clock,
        IExportKeyDeliveryAuthority? authority = null, IExportKeyDirectDeliveryProvider? provider = null)
    {
        var actor = new ExportKeyDeliveryActor(ActorHost.CreateForTest<ExportKeyDeliveryActor>(new ActorTestOptions { ActorId = new(identity.ActorId) }), clock, authority, provider);
        typeof(Dapr.Actors.Runtime.Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance)!.SetValue(actor, backend); return actor;
    }
    private static IExportKeyDeliveryAuthority Authority()
    {
        var authority = Substitute.For<IExportKeyDeliveryAuthority>();
        authority.AuthorizeOperationAsync(Arg.Any<ExportKeyDeliveryIdentity>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        authority.AuthorizeAsync(Arg.Any<ExportKeyDeliveryIdentity>(), Arg.Any<CancellationToken>()).Returns(true); return authority;
    }

    /// <summary>Lost release response is recovered solely by exact lookup, with immutable Delivered time after expiry/revocation and serialized restart.</summary>
    [Fact]
    public async Task LostReleaseAcknowledgementRecoversWithoutSecondRelease()
    {
        var clock = new CustodyFixtureClock(); var identity = Identity(clock); var backend = new InMemoryStateManager();
        var authority = Authority(); var provider = Substitute.For<IExportKeyDirectDeliveryProvider>();
        var outcome = new ExportKeyDeliveryOutcome(identity, ExportKeyDeliveryState.Delivered, clock.Now);
        provider.ReleaseAsync(identity, Arg.Any<CancellationToken>()).Returns(Task.FromException<ExportKeyDeliveryOutcome>(new HttpRequestException("Controlled response loss after synthetic principal delivery.")));
        provider.LookupAsync(identity, Arg.Any<CancellationToken>()).Returns(outcome);
        var actor = Actor(identity, backend, clock, authority, provider);
        (await actor.DeliverAsync(identity)).State.ShouldBe(ExportKeyDeliveryState.Unknown);
        backend.CommittedState.Single().Value.ShouldBeOfType<ExportKeyDeliveryOutcome>().State.ShouldBe(ExportKeyDeliveryState.Unknown);
        (await actor.DeliverAsync(identity)).ShouldBe(outcome);
        var saved = backend.CommittedState.Single();
        var restored = new InMemoryStateManager(); await restored.SetStateAsync(saved.Key, JsonSerializer.Deserialize<ExportKeyDeliveryOutcome>(JsonSerializer.SerializeToUtf8Bytes(saved.Value))!, TestContext.Current.CancellationToken);
        await restored.SaveStateAsync(TestContext.Current.CancellationToken); clock.Now = identity.ExclusiveExpiry.AddDays(1);
        authority.AuthorizeAsync(identity, Arg.Any<CancellationToken>()).Returns(false);
        (await Actor(identity, restored, clock, authority, provider).DeliverAsync(identity)).ShouldBe(outcome);
        await provider.Received(1).ReleaseAsync(identity, Arg.Any<CancellationToken>());
        await provider.Received(1).LookupAsync(identity, Arg.Any<CancellationToken>());
    }

    /// <summary>Unknown lookup retains the exact original durable reservation and performs no repeat release.</summary>
    [Fact]
    public async Task UnknownLookupCannotRepeatRelease()
    {
        var clock = new CustodyFixtureClock(); var identity = Identity(clock); var backend = new InMemoryStateManager();
        var provider = Substitute.For<IExportKeyDirectDeliveryProvider>();
        provider.ReleaseAsync(identity, Arg.Any<CancellationToken>()).Returns(new ExportKeyDeliveryOutcome(identity, ExportKeyDeliveryState.Unknown));
        provider.LookupAsync(identity, Arg.Any<CancellationToken>()).Returns(new ExportKeyDeliveryOutcome(identity, ExportKeyDeliveryState.Unavailable));
        var actor = Actor(identity, backend, clock, Authority(), provider);
        (await actor.DeliverAsync(identity)).State.ShouldBe(ExportKeyDeliveryState.Unknown);
        (await actor.DeliverAsync(identity)).State.ShouldBe(ExportKeyDeliveryState.Unknown);
        (await Actor(identity, backend, clock, Authority(), provider).LookupAsync(identity)).State.ShouldBe(ExportKeyDeliveryState.Unknown);
        await provider.Received(1).ReleaseAsync(identity, Arg.Any<CancellationToken>());
        backend.CommittedState.Single().Value.ShouldBeOfType<ExportKeyDeliveryOutcome>().Identity.ShouldBe(identity);
    }

    /// <summary>Current authority or exclusive expiry changing during the final awaited check prevents release.</summary>
    [Theory]
    [InlineData("expiry")]
    [InlineData("revocation")]
    public async Task FinalAuthorityBoundaryPreventsRelease(string vector)
    {
        var clock = new CustodyFixtureClock(); var identity = Identity(clock); var backend = new InMemoryStateManager();
        var provider = Substitute.For<IExportKeyDirectDeliveryProvider>(); var authority = Authority(); int calls = 0;
        authority.AuthorizeAsync(identity, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            if (++calls == 2 && vector == "expiry") { clock.Now = identity.ExclusiveExpiry; }
            return calls != 2 || vector != "revocation";
        });
        (await Actor(identity, backend, clock, authority, provider).DeliverAsync(identity)).State.ShouldBe(ExportKeyDeliveryState.NotDelivered);
        await provider.DidNotReceive().ReleaseAsync(Arg.Any<ExportKeyDeliveryIdentity>(), Arg.Any<CancellationToken>());
        backend.CommittedState.Single().Value.ShouldBeOfType<ExportKeyDeliveryOutcome>().Reason.ShouldBe("authority-or-expiry-changed");
    }

    /// <summary>The same scoped delivery id cannot substitute actor, export, purpose, audience, expiry, lifecycle, store or contract.</summary>
    [Fact]
    public async Task ChangedCompleteIdentityConflictsWithoutRelease()
    {
        var clock = new CustodyFixtureClock(); var identity = Identity(clock); var backend = new InMemoryStateManager();
        var provider = Substitute.For<IExportKeyDirectDeliveryProvider>(); provider.ReleaseAsync(identity, Arg.Any<CancellationToken>()).Returns(new ExportKeyDeliveryOutcome(identity, ExportKeyDeliveryState.Delivered, clock.Now));
        var actor = Actor(identity, backend, clock, Authority(), provider); await actor.DeliverAsync(identity);
        foreach (var changed in new[] { identity with { RequesterActorId = "other-actor" }, identity with { ExportId = "other-export" },
            identity with { Purpose = "other-purpose" }, identity with { Audience = "other-audience" }, identity with { CommitAcknowledgementRevision = 8 },
            identity with { ExclusiveExpiry = identity.ExclusiveExpiry.AddHours(1) }, identity with { LifecycleVersion = "other-lifecycle" }, identity with { StoreTarget = "other-store" } })
        { (await actor.DeliverAsync(changed)).State.ShouldBe(ExportKeyDeliveryState.Conflict); }
        await Should.ThrowAsync<ArgumentException>(() => actor.DeliverAsync(identity with { ContractVersion = 2 }));
        await Should.ThrowAsync<ArgumentException>(() => actor.DeliverAsync(identity with { TenantId = "tenant-b" }));
        await provider.Received(1).ReleaseAsync(identity, Arg.Any<CancellationToken>());
    }

    /// <summary>Both failed reservation save and committed-but-lost-ack save prevent first release and read only persisted outcome.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedReservationSaveNeverReleasesStagedIntent(bool commitBeforeFault)
    {
        var clock = new CustodyFixtureClock(); var identity = Identity(clock); var backend = new InMemoryStateManager();
        var manager = Substitute.For<IActorStateManager>(); var provider = Substitute.For<IExportKeyDirectDeliveryProvider>();
        manager.ClearCacheAsync(Arg.Any<CancellationToken>()).Returns(call => backend.ClearCacheAsync(call.Arg<CancellationToken>()));
        manager.TryGetStateAsync<ExportKeyDeliveryOutcome>(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => backend.TryGetStateAsync<ExportKeyDeliveryOutcome>(call.Arg<string>(), call.Arg<CancellationToken>()));
        manager.SetStateAsync(Arg.Any<string>(), Arg.Any<ExportKeyDeliveryOutcome>(), Arg.Any<CancellationToken>()).Returns(call => backend.SetStateAsync(call.Arg<string>(), call.Arg<ExportKeyDeliveryOutcome>(), call.Arg<CancellationToken>()));
        manager.SaveStateAsync(Arg.Any<CancellationToken>()).Returns(async call =>
        { if (commitBeforeFault) { await backend.SaveStateAsync(call.Arg<CancellationToken>()); } throw new HttpRequestException("Controlled reservation save fault."); });
        await Should.ThrowAsync<HttpRequestException>(() => Actor(identity, manager, clock, Authority(), provider).DeliverAsync(identity));
        if (commitBeforeFault) { backend.CommittedState.Single().Value.ShouldBeOfType<ExportKeyDeliveryOutcome>().State.ShouldBe(ExportKeyDeliveryState.Unknown); }
        else { backend.CommittedState.ShouldBeEmpty(); }
        var durable = await Actor(identity, backend, clock).LookupAsync(identity); durable.State.ShouldBe(ExportKeyDeliveryState.Unavailable);
        await provider.DidNotReceive().ReleaseAsync(Arg.Any<ExportKeyDeliveryIdentity>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Missing production bindings are unavailable and persist no release authority or outcome.</summary>
    [Fact]
    public async Task MissingBindingsStayUnavailable()
    {
        var clock = new CustodyFixtureClock(); var identity = Identity(clock); var backend = new InMemoryStateManager();
        (await Actor(identity, backend, clock).DeliverAsync(identity)).State.ShouldBe(ExportKeyDeliveryState.Unavailable); backend.CommittedState.ShouldBeEmpty();
    }

    /// <summary>Immutable delivered evidence does not grant a missing/withdrawn caller permission to read or retry the private method.</summary>
    [Fact]
    public async Task DeliveredOutcomeRequiresSeparateCurrentExactMethodCredential()
    {
        var clock = new CustodyFixtureClock(); var identity = Identity(clock); var backend = new InMemoryStateManager(); var authority = Authority();
        var provider = Substitute.For<IExportKeyDirectDeliveryProvider>(); provider.ReleaseAsync(identity, Arg.Any<CancellationToken>()).Returns(new ExportKeyDeliveryOutcome(identity, ExportKeyDeliveryState.Delivered, clock.Now));
        var actor = Actor(identity, backend, clock, authority, provider); var delivered = await actor.DeliverAsync(identity);
        (await Actor(identity, backend, clock).LookupAsync(identity)).State.ShouldBe(ExportKeyDeliveryState.Unavailable);
        authority.AuthorizeOperationAsync(identity, "LookupExportKey", Arg.Any<CancellationToken>()).Returns(false);
        (await actor.LookupAsync(identity)).State.ShouldBe(ExportKeyDeliveryState.Unavailable);
        authority.AuthorizeOperationAsync(identity, "DeliverExportKey", Arg.Any<CancellationToken>()).Returns(false);
        (await actor.DeliverAsync(identity)).State.ShouldBe(ExportKeyDeliveryState.Unavailable);
        backend.CommittedState.Single().Value.ShouldBeOfType<ExportKeyDeliveryOutcome>().ShouldBe(delivered);
        await provider.Received(1).ReleaseAsync(identity, Arg.Any<CancellationToken>());
    }
    /// <summary>Still-unknown delivery survives serialized restart and recovers original historical Delivered after expiry/fresh-authority withdrawal, without another release.</summary>
    [Fact]
    public async Task UnresolvedOriginalReleaseRecoversAfterExpiryAndFreshAuthorityWithdrawal()
    {
        var clock = new CustodyFixtureClock(); var identity = Identity(clock); var backend = new InMemoryStateManager(); var authority = Authority();
        var provider = Substitute.For<IExportKeyDirectDeliveryProvider>(); var delivered = new ExportKeyDeliveryOutcome(identity, ExportKeyDeliveryState.Delivered, clock.Now);
        provider.ReleaseAsync(identity, Arg.Any<CancellationToken>()).Returns(Task.FromException<ExportKeyDeliveryOutcome>(new HttpRequestException("Controlled original delivery response loss.")));
        provider.LookupAsync(identity, Arg.Any<CancellationToken>()).Returns(delivered);
        (await Actor(identity, backend, clock, authority, provider).DeliverAsync(identity)).State.ShouldBe(ExportKeyDeliveryState.Unknown);
        var saved = backend.CommittedState.Single(); ((ExportKeyDeliveryOutcome)saved.Value).State.ShouldBe(ExportKeyDeliveryState.Unknown);
        var restored = new InMemoryStateManager();
        await restored.SetStateAsync(saved.Key, JsonSerializer.Deserialize<ExportKeyDeliveryOutcome>(JsonSerializer.SerializeToUtf8Bytes(saved.Value))!, TestContext.Current.CancellationToken);
        await restored.SaveStateAsync(TestContext.Current.CancellationToken);
        clock.Now = identity.ExclusiveExpiry.AddDays(1); authority.AuthorizeAsync(identity, Arg.Any<CancellationToken>()).Returns(false);
        (await Actor(identity, restored, clock, authority, provider).LookupAsync(identity)).ShouldBe(delivered);
        restored.CommittedState.Single().Value.ShouldBe(delivered);
        await provider.Received(1).ReleaseAsync(identity, Arg.Any<CancellationToken>()); await provider.Received(1).LookupAsync(identity, Arg.Any<CancellationToken>());
    }

}
