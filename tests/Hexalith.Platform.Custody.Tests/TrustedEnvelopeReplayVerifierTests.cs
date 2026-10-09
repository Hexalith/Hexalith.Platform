using Hexalith.EventStore.Contracts.Security;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Custody.Tests;

/// <summary>Real canonical HMAC before synthetic exact first-seen source; fixtures cannot qualify Agents-owned production replay handlers.</summary>
public sealed class TrustedEnvelopeReplayVerifierTests
{
    private static TrustedEnvelopeIdentity Identity() => new(1, "fixture-r1", "fixture-issuer",
        new(TrustedPrincipalKind.User, "tenant-a", "actor-a", "party-a", 1, "current-roles", null, null, null, null),
        "Contracts.EditProposal", "EditProposal", "tenant-a", "interaction-a", "correlation", "cause",
        ["tenant-a", "User", "actor-a", "EditProposal", "client-key"], "fingerprint", "digest-v1", "fixture-audience", "logical-command");

    /// <summary>Lost registration acknowledgement and a later exact retry retain the original source times and one receipt.</summary>
    [Fact]
    public async Task LostAcknowledgementAndRetryReuseStoredFirstSeenWithoutRenewal()
    {
        var clock = new CustodyFixtureClock(); var profiles = new CustodyFixtureProfileProvider(clock); var keys = new CustodyFixtureKeyProvider(clock);
        var authenticator = new TrustedEnvelopeAuthenticator(keys, profiles, clock); var expected = Identity();
        var envelope = (await authenticator.IssueAsync(expected, TimeSpan.FromMinutes(2), TestContext.Current.CancellationToken)).Envelope!;
        var registrar = Substitute.For<ITrustedEnvelopeReplayRegistrar>(); TrustedEnvelopeReplayReceipt? original = null;
        registrar.RegisterAsync(Arg.Any<TrustedEnvelopeReplayIntent>(), Arg.Any<DateTimeOffset>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns<TrustedEnvelopeReplayReceipt?>(call =>
            {
                var intent = call.Arg<TrustedEnvelopeReplayIntent>();
                original ??= new(intent, call.Arg<DateTimeOffset>(), call.Arg<DateTimeOffset>() + call.Arg<TimeSpan>(), 1, "original-replay-receipt");
                original.Intent.ShouldBe(intent); throw new IOException("controlled lost first-seen acknowledgement");
            });
        registrar.LookupAsync(Arg.Any<TrustedEnvelopeReplayIntent>(), Arg.Any<CancellationToken>()).Returns(call =>
            original?.Intent == call.Arg<TrustedEnvelopeReplayIntent>() ? original : null);
        var verifier = new TrustedEnvelopeReplayVerifier(authenticator, registrar, profiles, clock);
        (await verifier.VerifyAsync(envelope, expected, TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyStatus.Succeeded);
        var retained = original!; clock.Now += TimeSpan.FromSeconds(1);
        (await new TrustedEnvelopeReplayVerifier(authenticator, registrar, profiles, clock).VerifyAsync(envelope, expected,
            TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyStatus.Succeeded);
        original.ShouldBe(retained); original!.FirstSeenAt.ShouldBe(envelope.IssuedAt);
        original.RetainUntil.ShouldBe(envelope.IssuedAt + profiles.Profile!.ReplayRetention);
    }

    /// <summary>Forged bytes perform no replay mutation; unknown, changed-scope/source or completion-time expiry releases no verified envelope.</summary>
    [Theory]
    [InlineData("forgery")][InlineData("unknown")][InlineData("cross-tenant")][InlineData("changed-source")][InlineData("expiry")]
    public async Task UncertainReplayOrExpiredAuthenticationCannotReleaseEnvelope(string vector)
    {
        var clock = new CustodyFixtureClock(); var profiles = new CustodyFixtureProfileProvider(clock); var keys = new CustodyFixtureKeyProvider(clock);
        var authenticator = new TrustedEnvelopeAuthenticator(keys, profiles, clock); var expected = Identity();
        var envelope = (await authenticator.IssueAsync(expected, TimeSpan.FromMinutes(2), TestContext.Current.CancellationToken)).Envelope!;
        if (vector == "forgery") { envelope = envelope with { Tag = new string('0', 64) }; }
        var registrar = Substitute.For<ITrustedEnvelopeReplayRegistrar>(); TrustedEnvelopeReplayReceipt? stored = null;
        registrar.RegisterAsync(Arg.Any<TrustedEnvelopeReplayIntent>(), Arg.Any<DateTimeOffset>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var intent = call.Arg<TrustedEnvelopeReplayIntent>();
                if (vector == "cross-tenant") { intent = intent with { TargetTenantId = "tenant-b" }; }
                if (vector == "expiry") { clock.Now = envelope.ExpiresAt; }
                stored = vector == "unknown" ? null : new(intent, envelope.IssuedAt, envelope.IssuedAt + profiles.Profile!.ReplayRetention, 1, "original-receipt");
                return stored;
            });
        registrar.LookupAsync(Arg.Any<TrustedEnvelopeReplayIntent>(), Arg.Any<CancellationToken>()).Returns(_ =>
            vector == "changed-source" ? stored! with { ReceiptId = "substituted-receipt" } : stored);
        var result = await new TrustedEnvelopeReplayVerifier(authenticator, registrar, profiles, clock).VerifyAsync(envelope, expected,
            TestContext.Current.CancellationToken);
        result.Status.ShouldNotBe(CustodyStatus.Succeeded); result.Envelope.ShouldBeNull();
        if (vector == "forgery") { registrar.ReceivedCalls().ShouldBeEmpty(); }
    }

    /// <summary>Explicit composition resolves consumers while raw exception capabilities remain absent from ordinary DI.</summary>
    [Fact]
    public void OnlyPrivateConsumerClosuresReceiveExceptionCapabilities()
    {
        var registrar = Substitute.For<ITrustedEnvelopeReplayRegistrar>(); var compromise = Substitute.For<IDeletionCapabilityCompromiseRegistrar>();
        var auth = Substitute.For<IDeletionCapabilityRevocationAuthenticator>(); var mirror = Substitute.For<IDeletionCapabilityGuardRevocationMirror>();
        using var services = new ServiceCollection().AddPlatformCustody().AddPrivateTrustedEnvelopeVerifier(registrar)
            .AddPrivateSecurityDenialRecorder(new SecuritySpoolFixture().Spool)
            .AddPrivateDeletionRevocationSubscriber(new("issuer", "protection", "tenant-a"), auth, compromise, mirror).BuildServiceProvider();
        services.GetRequiredService<TrustedEnvelopeReplayVerifier>().ShouldNotBeNull();
        services.GetRequiredService<DeletionCapabilityRevocationSubscriber>().ShouldNotBeNull();
        services.GetRequiredService<PlatformSecurityDenialRecorder>().ShouldNotBeNull();
        services.GetService<ITrustedEnvelopeReplayRegistrar>().ShouldBeNull(); services.GetService<IDeletionCapabilityCompromiseRegistrar>().ShouldBeNull();
        services.GetService<IDeletionCapabilityGuardRevocationMirror>().ShouldBeNull();
        services.GetService<ReplicatedSecurityObservationSpool>().ShouldBeNull(); services.GetService<ISecurityObservationRecorder>().ShouldBeNull();
    }

    /// <summary>Neither a shorter nor a renewed independently supplied replay receipt matches the accepted exclusive retention equation, initially or at lookup.</summary>
    [Theory]
    [InlineData(-1, false)][InlineData(1, false)][InlineData(-1, true)][InlineData(1, true)][InlineData(0, false)][InlineData(0, true)]
    public async Task ExactReplayRetentionIsRequiredAtRegistrationAndTerminalConfirmation(int deviationSeconds, bool substituteAtLookup)
    {
        var clock = new CustodyFixtureClock(); var profiles = new CustodyFixtureProfileProvider(clock); var keys = new CustodyFixtureKeyProvider(clock);
        var authenticator = new TrustedEnvelopeAuthenticator(keys, profiles, clock); var expected = Identity();
        var envelope = (await authenticator.IssueAsync(expected, TimeSpan.FromMinutes(2), TestContext.Current.CancellationToken)).Envelope!;
        var registrar = Substitute.For<ITrustedEnvelopeReplayRegistrar>(); TrustedEnvelopeReplayReceipt? receipt = null;
        registrar.RegisterAsync(Arg.Any<TrustedEnvelopeReplayIntent>(), Arg.Any<DateTimeOffset>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var first = call.Arg<DateTimeOffset>(); receipt = new(call.Arg<TrustedEnvelopeReplayIntent>(), first,
                first + call.Arg<TimeSpan>() + TimeSpan.FromSeconds(substituteAtLookup ? 0 : deviationSeconds), 1, "exact-original"); return receipt;
        });
        registrar.LookupAsync(Arg.Any<TrustedEnvelopeReplayIntent>(), Arg.Any<CancellationToken>()).Returns(_ => substituteAtLookup ? receipt! with { RetainUntil = receipt!.RetainUntil.AddSeconds(deviationSeconds) } : receipt);
        var result = await new TrustedEnvelopeReplayVerifier(authenticator, registrar, profiles, clock).VerifyAsync(envelope, expected, TestContext.Current.CancellationToken);
        result.Status.ShouldBe(deviationSeconds == 0 ? CustodyStatus.Succeeded : CustodyStatus.Unavailable);
        if (deviationSeconds != 0) { result.Envelope.ShouldBeNull(); }
    }
    /// <summary>Both actual synchronous profile boundaries share the operation budget; late profile results cannot release an envelope.</summary>
    [Theory]
    [InlineData(false, false)][InlineData(false, true)][InlineData(true, false)][InlineData(true, true)]
    public async Task SuspendedReplayProfileUsesWholeBudget(bool final, bool cancellation)
    {
        var (clock, advance) = PrivateOwnerDeadlineTestClock.Create(); var fixtureClock = new CustodyFixtureClock { Now = clock.GetUtcNow() };
        var stable = new CustodyFixtureProfileProvider(fixtureClock); var keys = new CustodyFixtureKeyProvider(fixtureClock);
        var authenticator = new TrustedEnvelopeAuthenticator(keys, stable, clock); var expected = Identity();
        var envelope = (await authenticator.IssueAsync(expected, TimeSpan.FromMinutes(2), TestContext.Current.CancellationToken)).Envelope!;
        var profiles = Substitute.For<IPlatformSigningProfileProvider>(); int calls = 0;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); using var release = new ManualResetEventSlim();
        profiles.GetCurrent().Returns(_ => { if (Interlocked.Increment(ref calls) == (final ? 2 : 1)) { entered.TrySetResult(); release.Wait(); } return stable.Profile; });
        var registrar = ReplayRegistrar(); using var caller = new CancellationTokenSource();
        var verifier = new TrustedEnvelopeReplayVerifier(authenticator, registrar, profiles, clock);
        // The test invokes on a worker to observe the pre-fix synchronous provider stall; production never abandons its continuation.
        var reading = Task.Run(() => verifier.VerifyAsync(envelope, expected, caller.Token), CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        try
        {
            if (cancellation) { caller.Cancel(); var error = await Should.ThrowAsync<OperationCanceledException>(() => reading.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken)); error.CancellationToken.ShouldBe(caller.Token); }
            else { advance(TimeSpan.FromSeconds(30)); var result = await reading.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken); result.Status.ShouldBe(CustodyStatus.Unavailable); result.Envelope.ShouldBeNull(); }
            int registrations = registrar.ReceivedCalls().Count(); release.Set();
            SpinWait.SpinUntil(() => reading.IsCompleted, TimeSpan.FromSeconds(2)).ShouldBeTrue(); registrar.ReceivedCalls().Count().ShouldBe(registrations);
        }
        finally { release.Set(); }
    }

    /// <summary>A final profile provider cannot cancel the original caller or advance the whole budget and then return authenticated success.</summary>
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task FinalProfileMustReconfirmWholeBudget(bool cancellation)
    {
        var (clock, advance) = PrivateOwnerDeadlineTestClock.Create(); var fixtureClock = new CustodyFixtureClock { Now = clock.GetUtcNow() };
        var stable = new CustodyFixtureProfileProvider(fixtureClock); var authenticator = new TrustedEnvelopeAuthenticator(new CustodyFixtureKeyProvider(fixtureClock), stable, clock);
        var expected = Identity(); var envelope = (await authenticator.IssueAsync(expected, TimeSpan.FromMinutes(2), TestContext.Current.CancellationToken)).Envelope!;
        using var caller = new CancellationTokenSource(); var profiles = Substitute.For<IPlatformSigningProfileProvider>(); int calls = 0;
        profiles.GetCurrent().Returns(_ => { if (++calls == 2) { if (cancellation) { caller.Cancel(); } else { advance(TimeSpan.FromSeconds(30)); } } return stable.Profile; });
        var verifier = new TrustedEnvelopeReplayVerifier(authenticator, ReplayRegistrar(), profiles, clock);
        if (cancellation) { var error = await Should.ThrowAsync<OperationCanceledException>(() => verifier.VerifyAsync(envelope, expected, caller.Token)); error.CancellationToken.ShouldBe(caller.Token); }
        else { var result = await verifier.VerifyAsync(envelope, expected, caller.Token); result.Status.ShouldBe(CustodyStatus.Unavailable); result.Envelope.ShouldBeNull(); }
    }

    private static ITrustedEnvelopeReplayRegistrar ReplayRegistrar()
    {
        var registrar = Substitute.For<ITrustedEnvelopeReplayRegistrar>(); TrustedEnvelopeReplayReceipt? receipt = null;
        registrar.RegisterAsync(Arg.Any<TrustedEnvelopeReplayIntent>(), Arg.Any<DateTimeOffset>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(call => receipt ??= new(call.Arg<TrustedEnvelopeReplayIntent>(), call.Arg<DateTimeOffset>(), call.Arg<DateTimeOffset>() + call.Arg<TimeSpan>(), 1, "exact-original"));
        registrar.LookupAsync(Arg.Any<TrustedEnvelopeReplayIntent>(), Arg.Any<CancellationToken>()).Returns(_ => receipt);
        return registrar;
    }

}
