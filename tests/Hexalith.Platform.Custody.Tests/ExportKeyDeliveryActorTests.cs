using Hexalith.EventStore.Contracts.Security;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Testing.Fakes;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Custody.Tests;

/// <summary>Actual delivery ledger persistence/restart/loss schedules with synthetic direct-provider/current authority ports, never real key release.</summary>
public sealed class ExportKeyDeliveryActorTests
{
    /// <summary>A separately authorized mutation recovers the independently admitted exact stage after restart without the original caller; read-only evidence cannot advance it and physical effects are not duplicated.</summary>
    [Fact]
    public async Task LaterMutationRecoversPreJournalOriginalWithoutItsCaller()
    {
        var clock = new CustodyFixtureClock(); var identity = Identity(clock); var backend = new InMemoryStateManager(); var authority = Authority();
        var provider = Substitute.For<IExportKeyDirectDeliveryProvider>(); var original = new ExportKeyDeliveryOutcome(identity, ExportKeyDeliveryState.Delivered, clock.Now);
        provider.ReleaseAsync(identity, Arg.Any<CancellationToken>()).Returns(_ => { AnchoredFixtureJournal.SetAvailable(authority, false); return original; });
        provider.LookupAsync(identity, Arg.Any<CancellationToken>()).Returns(new ExportKeyDeliveryOutcome(identity, ExportKeyDeliveryState.Unknown));
        (await Actor(identity, backend, clock, authority, provider).DeliverAsync(identity)).State.ShouldBe(ExportKeyDeliveryState.Unknown);
        var stage = backend.CommittedState.Values.OfType<AnchoredStateTransition>().Single(); authority.ClearReceivedCalls();
        (await Actor(identity, backend, clock, authority, provider).LookupAsync(identity)).State.ShouldBe(ExportKeyDeliveryState.Unknown);
        await authority.DidNotReceive().RecordTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>());
        backend.CommittedState.Values.OfType<AnchoredStateTransition>().Single().ShouldBe(stage);
        AnchoredFixtureJournal.SetAvailable(authority, true);
        (await Actor(identity, backend, clock, authority, provider).DeliverAsync(identity)).ShouldBe(original);
        backend.CommittedState.Single().Value.ShouldBeOfType<ExportKeyDeliveryOutcome>().ShouldBe(original);
        await provider.Received(1).ReleaseAsync(identity, Arg.Any<CancellationToken>());
    }

    private static ExportKeyDeliveryIdentity Identity(CustodyFixtureClock clock) => new("tenant-a", "delivery-1", "export-1", 7,
        "actor-1", "export-download", "principal-direct-channel", clock.Now.AddMinutes(1), "lifecycle-v1", "store-v1", 1);
    private static ExportKeyDeliveryActor Actor(ExportKeyDeliveryIdentity identity, IActorStateManager backend, TimeProvider clock,
        IExportKeyDeliveryAuthority? authority = null, IExportKeyDirectDeliveryProvider? provider = null)
    {
        var actor = new ExportKeyDeliveryActor(ActorHost.CreateForTest<ExportKeyDeliveryActor>(new ActorTestOptions { ActorId = new(identity.ActorId) }), clock, authority, provider);
        typeof(Dapr.Actors.Runtime.Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance)!.SetValue(actor, backend); return actor;
    }
    private static IExportKeyDeliveryAuthority Authority()
    {
        var authority = Substitute.For<IExportKeyDeliveryAuthority>(); long revision = 0; string anchor = Digest((ExportKeyDeliveryOutcome?)null);
        authority.ValidateStateAsync(Arg.Any<ExportKeyDeliveryIdentity>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => call.Arg<string>() == anchor);
        authority.RecordStateAsync(Arg.Any<ExportKeyDeliveryIdentity>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => {
            if (call.ArgAt<string>(1) != anchor) { return false; } anchor = call.ArgAt<string>(2); return true;
        });
        AnchoredFixtureJournal.Attach(authority, Identity(new CustodyFixtureClock()).ActorId + "|export-key-delivery-v1", () => (revision, anchor), (next, digest) => { revision = next; anchor = digest; });
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
        var authority = Authority(); var actor = Actor(identity, backend, clock, authority, provider);
        (await actor.DeliverAsync(identity)).State.ShouldBe(ExportKeyDeliveryState.Unknown);
        (await actor.DeliverAsync(identity)).State.ShouldBe(ExportKeyDeliveryState.Unknown);
        (await Actor(identity, backend, clock, authority, provider).LookupAsync(identity)).State.ShouldBe(ExportKeyDeliveryState.Unknown);
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
    [InlineData(1, false)][InlineData(1, true)][InlineData(2, false)][InlineData(2, true)]
    public async Task FailedReservationSaveNeverReleasesStagedIntent(int failSave, bool committed)
    {
        var clock = new CustodyFixtureClock(); var identity = Identity(clock); var backend = new InMemoryStateManager(); var authority = Authority();
        var provider = Substitute.For<IExportKeyDirectDeliveryProvider>(); var terminal = new ExportKeyDeliveryOutcome(identity, ExportKeyDeliveryState.Delivered, clock.Now);
        provider.ReleaseAsync(identity, Arg.Any<CancellationToken>()).Returns(terminal); provider.LookupAsync(identity, Arg.Any<CancellationToken>()).Returns(terminal);
        var manager = ActorPendingFixture.Faulting<ExportKeyDeliveryOutcome>(backend, failSave, committed);
        await Should.ThrowAsync<HttpRequestException>(() => Actor(identity, manager, clock, authority, provider).DeliverAsync(identity));
        await provider.DidNotReceive().ReleaseAsync(identity, Arg.Any<CancellationToken>()); await backend.ClearCacheAsync(TestContext.Current.CancellationToken);
        var restarted = Actor(identity, backend, clock, authority, provider); (await restarted.DeliverAsync(identity)).ShouldBe(terminal);
        await provider.Received(failSave == 1 && !committed ? 1 : 0).ReleaseAsync(identity, Arg.Any<CancellationToken>());
        await provider.Received(failSave == 1 && !committed ? 0 : 1).LookupAsync(identity, Arg.Any<CancellationToken>());
        (await restarted.LookupAsync(identity)).ShouldBe(terminal);
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

    /// <summary>Absent, older unknown or divergent terminal restore cannot authorize outcome release or a second physical call under unchanged private credentials.</summary>
    [Theory]
    [InlineData("absent")][InlineData("unknown")][InlineData("divergent")]
    public async Task RestoredDeliveryStateCannotReplaceOriginalTerminalOutcome(string restore)
    {
        var clock = new CustodyFixtureClock(); var identity = Identity(clock); var backend = new InMemoryStateManager(); var authority = Authority(); var provider = Substitute.For<IExportKeyDirectDeliveryProvider>();
        var original = new ExportKeyDeliveryOutcome(identity, ExportKeyDeliveryState.Delivered, clock.Now);
        provider.ReleaseAsync(identity, Arg.Any<CancellationToken>()).Returns(Task.FromException<ExportKeyDeliveryOutcome>(new HttpRequestException("Controlled original delivery loss.")));
        provider.LookupAsync(identity, Arg.Any<CancellationToken>()).Returns(original); var actor = Actor(identity, backend, clock, authority, provider);
        await actor.DeliverAsync(identity); var pending = backend.CommittedState.Single(); await actor.LookupAsync(identity); var latest = backend.CommittedState.Single();
        var restored = new InMemoryStateManager();
        if (restore != "absent") { await restored.SetStateAsync(latest.Key, restore == "unknown" ? pending.Value : original with { ObservedAt = clock.Now.AddSeconds(-1) }, TestContext.Current.CancellationToken); await restored.SaveStateAsync(TestContext.Current.CancellationToken); }
        var restarted = Actor(identity, restored, clock, authority, provider); await Should.ThrowAsync<InvalidOperationException>(() => restarted.LookupAsync(identity));
        await restored.SetStateAsync(latest.Key, JsonSerializer.Deserialize<ExportKeyDeliveryOutcome>(JsonSerializer.Serialize(latest.Value))!, TestContext.Current.CancellationToken); await restored.SaveStateAsync(TestContext.Current.CancellationToken);
        (await restarted.LookupAsync(identity)).ShouldBe(original); await provider.Received(1).ReleaseAsync(identity, Arg.Any<CancellationToken>());
    }

    /// <summary>Suspended direct release/lookup releases the modeled tenant turn; late completion cannot change Unknown and exact serialized lookup retains the original pre-expiry observation.</summary>
    [Theory]
    [InlineData(false, false)][InlineData(false, true)][InlineData(true, false)][InlineData(true, true)]
    public async Task SuspendedPhysicalDeliveryReleasesTurnWithoutLateOutcome(bool lookup, bool invocation)
    {
        var (clock, advance) = PrivateOwnerDeadlineTestClock.Create();
        var identity = Identity(new CustodyFixtureClock { Now = clock.GetUtcNow() }); var authority = Authority(); var backend = new InMemoryStateManager();
        var original = new ExportKeyDeliveryOutcome(identity, ExportKeyDeliveryState.Delivered, clock.GetUtcNow());
        var provider = Substitute.For<IExportKeyDirectDeliveryProvider>();
        if (lookup)
        {
            provider.ReleaseAsync(identity, Arg.Any<CancellationToken>()).Returns(Task.FromException<ExportKeyDeliveryOutcome>(new HttpRequestException("Original direct release acknowledgement lost.")));
            (await Actor(identity, backend, clock, authority, provider).DeliverAsync(identity)).State.ShouldBe(ExportKeyDeliveryState.Unknown);
        }
        using var release = new ManualResetEventSlim(); using var turn = new SemaphoreSlim(1);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var returned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<ExportKeyDeliveryOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<ExportKeyDeliveryOutcome> Suspend()
        {
            entered.TrySetResult(); if (!invocation) { return pending.Task; }
            release.Wait(); returned.TrySetResult(); return Task.FromResult(original);
        }
        if (lookup) { provider.LookupAsync(identity, Arg.Any<CancellationToken>()).Returns(_ => Suspend()); }
        else { provider.ReleaseAsync(identity, Arg.Any<CancellationToken>()).Returns(_ => Suspend()); }
        var actor = Actor(identity, backend, clock, authority, provider);
        async Task<T> Turn<T>(Func<Task<T>> operation)
        { await turn.WaitAsync(TestContext.Current.CancellationToken); try { return await operation(); } finally { turn.Release(); } }
        var waiting = Task.Run(() => Turn(() => lookup ? actor.LookupAsync(identity) : actor.DeliverAsync(identity)), TestContext.Current.CancellationToken);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); advance(TimeSpan.FromSeconds(30));
            (await waiting.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken)).State.ShouldBe(ExportKeyDeliveryState.Unknown);
            var saved = backend.CommittedState.Single(); string before = JsonSerializer.Serialize(saved.Value);
            ((ExportKeyDeliveryOutcome)saved.Value).Identity.ShouldBe(identity); ((ExportKeyDeliveryOutcome)saved.Value).ObservedAt.ShouldBeNull();
            (await Turn(() => actor.DeliverAsync(identity with { RequesterActorId = "another-requester" })).WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken))
                .State.ShouldBe(ExportKeyDeliveryState.Conflict);
            if (invocation) { release.Set(); await returned.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); }
            else { pending.TrySetResult(original); }
            JsonSerializer.Serialize(backend.CommittedState.Single().Value).ShouldBe(before);
            var restored = new InMemoryStateManager();
            await restored.SetStateAsync(saved.Key, JsonSerializer.Deserialize<ExportKeyDeliveryOutcome>(before)!, TestContext.Current.CancellationToken);
            await restored.SaveStateAsync(TestContext.Current.CancellationToken);
            advance(TimeSpan.FromMinutes(2)); authority.AuthorizeAsync(identity, Arg.Any<CancellationToken>()).Returns(false);
            provider.LookupAsync(identity, Arg.Any<CancellationToken>()).Returns(original);
            var restarted = Actor(identity, restored, clock, authority, provider);
            (await restarted.LookupAsync(identity)).ShouldBe(original); (await restarted.DeliverAsync(identity)).ShouldBe(original);
            restored.CommittedState.Single().Value.ShouldBe(original); original.ObservedAt!.Value.ShouldBeLessThan(identity.ExclusiveExpiry);
            await provider.Received(1).ReleaseAsync(identity, Arg.Any<CancellationToken>());
        }
        finally { release.Set(); pending.TrySetResult(original); }
    }

    private static string Digest<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));

    /// <summary>Withdrawing only the in-flight method credential withholds response evidence while retaining the exact completed original.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InFlightMethodCredentialWithdrawalRetainsOriginalButWithholdsResponse(bool lookup)
    {
        var clock = new CustodyFixtureClock(); var identity = Identity(clock); var backend = new InMemoryStateManager();
        var authority = Authority(); var provider = Substitute.For<IExportKeyDirectDeliveryProvider>();
        var original = new ExportKeyDeliveryOutcome(identity, ExportKeyDeliveryState.Delivered, clock.Now);
        if (lookup)
        {
            provider.ReleaseAsync(identity, Arg.Any<CancellationToken>()).Returns(Task.FromException<ExportKeyDeliveryOutcome>(new IOException("Original release acknowledgement lost")));
            (await Actor(identity, backend, clock, authority, provider).DeliverAsync(identity)).State.ShouldBe(ExportKeyDeliveryState.Unknown);
        }
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<ExportKeyDeliveryOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (lookup) { provider.LookupAsync(identity, Arg.Any<CancellationToken>()).Returns(_ => { entered.TrySetResult(); return release.Task; }); }
        else { provider.ReleaseAsync(identity, Arg.Any<CancellationToken>()).Returns(_ => { entered.TrySetResult(); return release.Task; }); }
        string method = lookup ? "LookupExportKey" : "DeliverExportKey"; bool methodAllowed = true;
        authority.AuthorizeOperationAsync(identity, method, Arg.Any<CancellationToken>()).Returns(_ => methodAllowed);
        var actor = Actor(identity, backend, clock, authority, provider);
        var operation = lookup ? actor.LookupAsync(identity) : actor.DeliverAsync(identity);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        backend.CommittedState.Single().Value.ShouldBeOfType<ExportKeyDeliveryOutcome>().State.ShouldBe(ExportKeyDeliveryState.Unknown);
        methodAllowed = false; clock.Now = clock.Now.AddSeconds(1);
        release.TrySetResult(original);

        var response = await operation.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        response.State.ShouldBe(ExportKeyDeliveryState.Unavailable); response.ObservedAt.ShouldBeNull();
        var saved = backend.CommittedState.Single(); saved.Value.ShouldBe(original);
        var restored = new InMemoryStateManager();
        await restored.SetStateAsync(saved.Key, JsonSerializer.Deserialize<ExportKeyDeliveryOutcome>(JsonSerializer.SerializeToUtf8Bytes(saved.Value))!, TestContext.Current.CancellationToken);
        await restored.SaveStateAsync(TestContext.Current.CancellationToken);
        var restarted = Actor(identity, restored, clock, authority, provider);
        (lookup ? await restarted.LookupAsync(identity) : await restarted.DeliverAsync(identity)).State.ShouldBe(ExportKeyDeliveryState.Unavailable);
        JsonSerializer.SerializeToUtf8Bytes(restored.CommittedState.Single().Value).ShouldBe(JsonSerializer.SerializeToUtf8Bytes(original));
        methodAllowed = true; clock.Now = identity.ExclusiveExpiry.AddDays(1);
        (await restarted.LookupAsync(identity)).ShouldBe(original);
        var retained = restored.CommittedState.Single().Value.ShouldBeOfType<ExportKeyDeliveryOutcome>();
        retained.ObservedAt.ShouldBe(original.ObservedAt); retained.Identity.ExclusiveExpiry.ShouldBe(identity.ExclusiveExpiry);
        await provider.Received(1).ReleaseAsync(identity, Arg.Any<CancellationToken>());
        if (lookup) { await provider.Received(1).LookupAsync(identity, Arg.Any<CancellationToken>()); }
        else { await provider.DidNotReceive().LookupAsync(identity, Arg.Any<CancellationToken>()); }
        // Physical release authority stays independently allowed throughout this method-only withdrawal.
        (await authority.AuthorizeAsync(identity, TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

}
