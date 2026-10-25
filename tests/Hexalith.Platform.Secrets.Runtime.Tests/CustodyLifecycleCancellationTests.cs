using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class CustodyLifecycleCancellationTests
{
    [Fact]
    public async Task CredentialBoundAuthorizerPreservesCallerCancellationWhenTransportReturnsRefusal()
    {
        using var caller = new CancellationTokenSource();
        var transport = Substitute.For<ISecretsCredentialBoundDaprInvoker>();
        transport.InvokeCredentialBoundAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => { caller.Cancel(); return Task.FromResult<string?>(null); });
        var authorizer = new DaprCustodyAuthorizerClient(transport,
            new Dictionary<string, string> { [DaprCustodyAuthorizerClient.LogicalRole] = "independent-authorizer" }, "file:/private/credential");
        var intent = new CustodyCallIntent("unwrap", "tenant", "owner", "operation", CustodyBindingFixture.Digest, "{}", "attempt", "challenge");
        var failure = await Should.ThrowAsync<OperationCanceledException>(() => authorizer.PrepareAsync(intent, caller.Token));
        failure.CancellationToken.ShouldBe(caller.Token);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CredentialBoundAuthorizerNormalizesAlreadyCancelledForeignTask(bool authorize)
    {
        using var caller = new CancellationTokenSource();
        var transport = Substitute.For<ISecretsCredentialBoundDaprInvoker>();
        transport.InvokeCredentialBoundAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => CancelledWithForeignToken<string?>(caller));
        var authorizer = new DaprCustodyAuthorizerClient(transport,
            new Dictionary<string, string> { [DaprCustodyAuthorizerClient.LogicalRole] = "independent-authorizer" }, "file:/private/credential");
        var intent = new CustodyCallIntent("unwrap", "tenant", "owner", "operation", CustodyBindingFixture.Digest, "{}", "attempt", "challenge");
        var failure = await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            if (authorize) await authorizer.AuthorizeAsync(intent, new("{}", 1024, CustodyBindingFixture.Digest, null), CustodyBindingFixture.Digest, caller.Token);
            else await authorizer.PrepareAsync(intent, caller.Token);
        });
        failure.CancellationToken.ShouldBe(caller.Token);
    }

    [Fact]
    public async Task DiscardedOwnedKeyIsClearedWhenCallerCancelsBeforeResultExposure()
    {
        using var sdk = new DaprClientBuilder().Build(); using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var client = Client(sdk, key); byte[] owned = Enumerable.Range(1, 32).Select(x => (byte)x).ToArray();
        using var caller = new CancellationTokenSource();
        var error = await Should.ThrowAsync<OperationCanceledException>(() => client.InBudgetAsync(caller.Token,
            _ => { caller.Cancel(); return Task.FromResult(new CustodyKeyUseResult("Released", new(owned))); }, () => new CustodyKeyUseResult("Unknown")));
        error.CancellationToken.ShouldBe(caller.Token); owned.ShouldBe(new byte[32]);
    }

    [Fact]
    public async Task AuthorityRestoresCancellationFromRefusingOwnerForwardersAndRegistrationLocator()
    {
        using var sdk = new DaprClientBuilder().Build(); using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var client = Client(sdk, key);
        foreach (string phase in new[] { "state", "operation", "admit", "record", "recover", "verify", "registration" })
        {
            using var caller = new CancellationTokenSource();
            var owner = Substitute.For<ICustodyOwnerStateAuthority>();
            Task<bool> Refusal() { caller.Cancel(); return Task.FromResult(false); }
            owner.ValidateStateAsync(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => Refusal());
            owner.AuthorizeOperationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => Refusal());
            owner.AdmitTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(_ => Refusal());
            owner.RecordTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(_ => Refusal());
            owner.RecoverTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(_ => Refusal());
            owner.VerifyTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(_ => Refusal());
            var registrations = Substitute.For<ICustodyKeyRegistrationSource>();
            registrations.FindAsync(Arg.Any<CustodyKeyObjectIdentity>(), Arg.Any<CancellationToken>()).Returns(_ => { caller.Cancel(); return Task.FromResult<CustodyKeyRegistration?>(null); });
            var authority = new DaprCustodyKeyLifecycleAuthority("tenant", owner, client, Substitute.For<ICustodyKeyLifecycleProvider>(), registrations);
            var transition = new AnchoredStateTransition("owner", 0, 1, CustodyBindingFixture.Digest, CustodyBindingFixture.Digest, [1]);
            var request = CustodyBindingFixture.Lifecycle().Lifecycle!;
            var outcome = new CustodyKeyLifecycleOutcome(request.Identity, request.OperationId, CustodyBindingFixture.Digest, CustodyKeyLifecycleStatus.Unknown);
            Task<bool> call = phase switch {
                "state" => authority.ValidateStateAsync("tenant", 0, CustodyBindingFixture.Digest, caller.Token),
                "operation" => authority.AuthorizeOperationAsync(CustodyBindingFixture.Identity, "operation", "method", CustodyBindingFixture.Digest, caller.Token),
                "admit" => authority.AdmitTransitionAsync(transition, caller.Token),
                "record" => authority.RecordTransitionAsync(transition, caller.Token),
                "recover" => authority.RecoverTransitionAsync(transition, caller.Token),
                "verify" => authority.VerifyTransitionAsync(transition, caller.Token),
                _ => authority.VerifyOutcomeAsync(request, outcome, caller.Token),
            };
            (await Should.ThrowAsync<OperationCanceledException>(() => call)).CancellationToken.ShouldBe(caller.Token);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AuthorityNormalizesAlreadyCancelledForeignOwnerOrRegistrationTask(bool registration)
    {
        using var sdk = new DaprClientBuilder().Build(); using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var caller = new CancellationTokenSource();
        var owner = Substitute.For<ICustodyOwnerStateAuthority>();
        owner.ValidateStateAsync(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => CancelledWithForeignToken<bool>(caller));
        var registrations = Substitute.For<ICustodyKeyRegistrationSource>();
        registrations.FindAsync(Arg.Any<CustodyKeyObjectIdentity>(), Arg.Any<CancellationToken>())
            .Returns(_ => CancelledWithForeignToken<CustodyKeyRegistration?>(caller));
        var authority = new DaprCustodyKeyLifecycleAuthority("tenant", owner, Client(sdk, key), Substitute.For<ICustodyKeyLifecycleProvider>(), registrations);
        var request = CustodyBindingFixture.Lifecycle().Lifecycle!;
        var outcome = new CustodyKeyLifecycleOutcome(request.Identity, request.OperationId, CustodyBindingFixture.Digest, CustodyKeyLifecycleStatus.Pinned);
        var failure = await Should.ThrowAsync<OperationCanceledException>(() => registration
            ? authority.VerifyOutcomeAsync(request, outcome, caller.Token)
            : authority.ValidateStateAsync("tenant", 0, CustodyBindingFixture.Digest, caller.Token));
        failure.CancellationToken.ShouldBe(caller.Token);
    }

    // These regressions must exercise a default caller token; their outer waits still use test cancellation.
#pragma warning disable xUnit1051
    [Fact]
    public async Task AuthorityBoundsUnresponsiveOwnerWithDefaultCallerToken()
    {
        using var sdk = new DaprClientBuilder().Build(); using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var clock = new BudgetClock(); var owner = Substitute.For<ICustodyOwnerStateAuthority>();
        var pending = new TaskCompletionSource<bool>(); CancellationToken downstream = default;
        owner.ValidateStateAsync(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => { downstream = call.ArgAt<CancellationToken>(2); return pending.Task; });
        var authority = new DaprCustodyKeyLifecycleAuthority("tenant", owner, Client(sdk, key, clock),
            Substitute.For<ICustodyKeyLifecycleProvider>(), Substitute.For<ICustodyKeyRegistrationSource>());
        var result = authority.ValidateStateAsync("tenant", 0, CustodyBindingFixture.Digest, cancellationToken: default);
        downstream.CanBeCanceled.ShouldBeTrue(); result.IsCompleted.ShouldBeFalse();
        clock.Advance(TimeSpan.FromSeconds(30));
        (await result.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).ShouldBeFalse(); downstream.IsCancellationRequested.ShouldBeTrue();
    }

    [Fact]
    public async Task AuthorityBoundsUnresponsiveRegistrationLocatorWithDefaultCallerToken()
    {
        using var sdk = new DaprClientBuilder().Build(); using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var clock = new BudgetClock(); var registrations = Substitute.For<ICustodyKeyRegistrationSource>();
        var pending = new TaskCompletionSource<CustodyKeyRegistration?>(); CancellationToken downstream = default;
        registrations.FindAsync(Arg.Any<CustodyKeyObjectIdentity>(), Arg.Any<CancellationToken>())
            .Returns(call => { downstream = call.ArgAt<CancellationToken>(1); return pending.Task; });
        var lifecycle = Substitute.For<ICustodyKeyLifecycleProvider>();
        var authority = new DaprCustodyKeyLifecycleAuthority("tenant", Substitute.For<ICustodyOwnerStateAuthority>(), Client(sdk, key, clock), lifecycle, registrations);
        var request = CustodyBindingFixture.Lifecycle().Lifecycle!;
        var outcome = new CustodyKeyLifecycleOutcome(request.Identity, request.OperationId, CustodyBindingFixture.Digest, CustodyKeyLifecycleStatus.Pinned);
        var result = authority.VerifyOutcomeAsync(request, outcome, cancellationToken: default);
        downstream.CanBeCanceled.ShouldBeTrue(); result.IsCompleted.ShouldBeFalse();
        clock.Advance(TimeSpan.FromSeconds(30));
        (await result.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).ShouldBeFalse(); downstream.IsCancellationRequested.ShouldBeTrue();
        await lifecycle.DidNotReceiveWithAnyArgs().LookupAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task AuthorityRegistrationAndLifecycleLookupShareOriginalDeadline()
    {
        using var sdk = new DaprClientBuilder().Build(); using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var clock = new BudgetClock(); var registrations = Substitute.For<ICustodyKeyRegistrationSource>();
        CancellationToken registrationToken = default, lifecycleToken = default;
        registrations.FindAsync(Arg.Any<CustodyKeyObjectIdentity>(), Arg.Any<CancellationToken>())
            .Returns(call => { registrationToken = call.ArgAt<CancellationToken>(1); clock.Advance(TimeSpan.FromSeconds(20)); return Task.FromResult<CustodyKeyRegistration?>(CustodyBindingFixture.Registration); });
        var lifecycle = Substitute.For<ICustodyKeyLifecycleProvider>(); var pending = new TaskCompletionSource<CustodyKeyLifecycleOutcome>();
        lifecycle.LookupAsync(Arg.Any<CustodyKeyRegistration>(), Arg.Any<CustodyKeyLifecycleRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => { lifecycleToken = call.ArgAt<CancellationToken>(3); return pending.Task; });
        var authority = new DaprCustodyKeyLifecycleAuthority("tenant", Substitute.For<ICustodyOwnerStateAuthority>(), Client(sdk, key, clock), lifecycle, registrations);
        var request = CustodyBindingFixture.Lifecycle().Lifecycle!;
        var outcome = new CustodyKeyLifecycleOutcome(request.Identity, request.OperationId, CustodyBindingFixture.Digest, CustodyKeyLifecycleStatus.Pinned);
        var result = authority.VerifyOutcomeAsync(request, outcome, cancellationToken: default);
        lifecycleToken.ShouldBe(registrationToken); lifecycleToken.CanBeCanceled.ShouldBeTrue();
        clock.Advance(TimeSpan.FromSeconds(9)); result.IsCompleted.ShouldBeFalse();
        clock.Advance(TimeSpan.FromSeconds(1));
        (await result.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).ShouldBeFalse(); lifecycleToken.IsCancellationRequested.ShouldBeTrue();
    }
#pragma warning restore xUnit1051

    private static Task<T> CancelledWithForeignToken<T>(CancellationTokenSource caller)
    {
        using var foreign = CancellationTokenSource.CreateLinkedTokenSource(caller.Token);
        caller.Cancel(); return Task.FromCanceled<T>(foreign.Token);
    }

    private static CustodyLifecycleClient Client(DaprClient sdk, ECDsa key, TimeProvider? clock = null)
        => new(sdk, new Dictionary<string, string>(), "owner", JsonSerializer.Serialize(CustodyBindingFixture.Enrollment),
            key.ExportSubjectPublicKeyInfoPem(), Substitute.For<ICustodyLifecycleCallSource>()) { OperationClock = clock ?? TimeProvider.System };

    private sealed class BudgetClock : TimeProvider
    {
        private long _ticks;
        private readonly List<BudgetTimer> _timers = [];
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        { var timer = new BudgetTimer(this, callback, state); _timers.Add(timer); timer.Change(dueTime, period); return timer; }
        internal void Advance(TimeSpan elapsed)
        {
            _ticks += elapsed.Ticks;
            foreach (var timer in _timers.ToArray()) if (!timer.Disposed && timer.Due <= _ticks) { timer.Disposed = true; timer.Callback(timer.State); }
        }
        private sealed class BudgetTimer(BudgetClock clock, TimerCallback callback, object? state) : ITimer
        {
            internal bool Disposed; internal long Due;
            internal TimerCallback Callback = callback; internal object? State = state;
            public bool Change(TimeSpan dueTime, TimeSpan period) { Disposed = false; Due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : clock._ticks + dueTime.Ticks; return true; }
            public void Dispose() => Disposed = true;
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
