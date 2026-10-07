using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.Platform.Custody.Tests;

/// <summary>Behavior evidence for bounded cleanup, separate from actual custody/restore qualification.</summary>
public sealed class IdentityHistoryCleanupTests
{
    private readonly AggregateIdentity _identity = new("tenant-a", "party", "party-1");
    private readonly IdentityHistoryPolicy _policy = new("party-actor-retention-v1", TimeSpan.FromDays(365), "binding-effective-at");
    private readonly DateTimeOffset _effectiveAt = new(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);
    private readonly CustodyFixtureClock _clock = new();
    private readonly IdentityHistoryCustodyEvidence _evidence;
    private readonly IdentityHistoryCleanupFixture _provider;
    private readonly IdentityHistoryCleanup _cleanup;

    /// <summary>Creates an exact expired synthetic retention unit, with no actual cryptographic material.</summary>
    public IdentityHistoryCleanupTests()
    {
        _evidence = new(_policy.PolicyId, "party-actor-history-v1", _effectiveAt.AddDays(365), 1, "receipt-1", true, true, true);
        _clock.Now = _evidence.ExpiresAt;
        _provider = new(_clock);
        _provider.Register(_identity, _evidence);
        _cleanup = new(_clock, _provider);
    }

    private Task<IdentityHistoryCleanupOutcome> RunAsync(CancellationToken? token = null)
        => _cleanup.ProcessAsync(_identity, _policy, _effectiveAt, _evidence, token ?? TestContext.Current.CancellationToken);

    /// <summary>Cleanup never contacts custody before exclusive expiry.</summary>
    [Theory]
    [InlineData(-1L)]
    [InlineData(-864000000000L)]
    public async Task BeforeExpiry_DoesNotContactProvider(long offsetTicks)
    {
        _clock.Now = _evidence.ExpiresAt.AddTicks(offsetTicks);
        (await RunAsync()).ShouldBe(IdentityHistoryCleanupOutcome.NotExpired);
        _provider.Attempts.ShouldBeEmpty();
        _provider.ReadChecks.ShouldBe(0);
        _provider.IsDestroyed(_identity, _evidence).ShouldBeFalse();
    }

    /// <summary>Exactly at and after expiry require provider destruction and fresh denial.</summary>
    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(864000000000L)]
    public async Task AtOrAfterExpiry_ConfirmsExactUnitAndFreshDenial(long offsetTicks)
    {
        _clock.Now = _evidence.ExpiresAt.AddTicks(offsetTicks);
        (await RunAsync()).ShouldBe(IdentityHistoryCleanupOutcome.ProviderConfirmedDestroyed);
        _provider.IsDestroyed(_identity, _evidence).ShouldBeTrue();
        _provider.Attempts.Single().ShouldBe((_identity, _evidence));
        _provider.ReadChecks.ShouldBe(1);
    }

    /// <summary>Invalid policy/scope/evidence cannot authorize any destruction attempt.</summary>
    [Theory]
    [InlineData("domain")][InlineData("null-identity")][InlineData("null-policy")][InlineData("null-evidence")]
    [InlineData("id")][InlineData("zero")][InlineData("negative")][InlineData("duration")][InlineData("trigger")]
    [InlineData("purpose")][InlineData("expiry")][InlineData("revision")][InlineData("receipt")]
    [InlineData("source")][InlineData("restore")][InlineData("copies")][InlineData("overflow")]
    public async Task InvalidInput_DeniesBeforeProvider(string field)
    {
        AggregateIdentity identity = field == "domain" ? new("tenant-a", "other", "party-1") : _identity;
        IdentityHistoryPolicy policy = field switch
        {
            "id" => _policy with { PolicyId = "different-version" },
            "zero" => _policy with { Retention = TimeSpan.Zero },
            "negative" => _policy with { Retention = TimeSpan.FromDays(-1) },
            "duration" => _policy with { Retention = TimeSpan.FromDays(366) },
            "trigger" => _policy with { ExpiryTrigger = "profile-erasure" },
            "overflow" => _policy with { Retention = TimeSpan.MaxValue },
            _ => _policy,
        };
        IdentityHistoryCustodyEvidence evidence = field switch
        {
            "purpose" => _evidence with { Purpose = "party-profile" },
            "expiry" => _evidence with { ExpiresAt = _evidence.ExpiresAt.AddTicks(1) },
            "revision" => _evidence with { LifecycleRevision = 0 },
            "receipt" => _evidence with { EvidenceId = " " },
            "source" => _evidence with { SourceExpiryEnforced = false },
            "restore" => _evidence with { RestoreSafe = false },
            "copies" => _evidence with { DerivedCopiesCovered = false },
            _ => _evidence,
        };
        (await _cleanup.ProcessAsync(field == "null-identity" ? null! : identity,
            field == "null-policy" ? null! : policy, _effectiveAt, field == "null-evidence" ? null! : evidence,
            TestContext.Current.CancellationToken)).ShouldBe(IdentityHistoryCleanupOutcome.Invalid);
        _provider.Attempts.ShouldBeEmpty();
        _provider.ReadChecks.ShouldBe(0);
        _provider.IsDestroyed(_identity, _evidence).ShouldBeFalse();
    }

    /// <summary>Default registration supplies a usable pending operation, without installing a history provider.</summary>
    [Fact]
    public async Task DefaultRegistration_KeepsMissingCustodyPending()
    {
        using ServiceProvider services = new ServiceCollection().AddSingleton<TimeProvider>(_clock).AddPlatformCustody().BuildServiceProvider();
        using IServiceScope scope = services.CreateScope();
        scope.ServiceProvider.GetService<IIdentityHistoryCustody>().ShouldBeNull();
        IdentityHistoryCleanup cleanup = scope.ServiceProvider.GetRequiredService<IdentityHistoryCleanup>();
        (await cleanup.ProcessAsync(_identity, _policy, _effectiveAt, _evidence, TestContext.Current.CancellationToken))
            .ShouldBe(IdentityHistoryCleanupOutcome.Pending);
    }

    /// <summary>False/failed/cancelled provider outcomes cannot become destruction receipts.</summary>
    [Theory]
    [InlineData("false")][InlineData("sync-throw")][InlineData("async-throw")][InlineData("provider-cancel")]
    public async Task FailedDestruction_RemainsPending(string failure)
    {
        _provider.DestructionHook = (_, _, _) => failure switch
        {
            "false" => Task.FromResult(false),
            "sync-throw" => throw new InvalidOperationException("synthetic failure"),
            "async-throw" => Task.FromException<bool>(new InvalidOperationException("synthetic failure")),
            _ => Task.FromCanceled<bool>(new CancellationToken(canceled: true)),
        };
        (await RunAsync()).ShouldBe(IdentityHistoryCleanupOutcome.Pending);
        _provider.IsDestroyed(_identity, _evidence).ShouldBeFalse();
        _provider.ReadChecks.ShouldBe(0);
    }

    /// <summary>Readable or unavailable lifecycle after provider confirmation cannot establish completion.</summary>
    [Theory]
    [InlineData("readable")][InlineData("sync-throw")][InlineData("async-throw")][InlineData("provider-cancel")]
    public async Task FailedFreshDenial_RemainsPending(string failure)
    {
        _provider.ReadHook = _ => failure switch
        {
            "readable" => Task.FromResult(true),
            "sync-throw" => throw new InvalidOperationException("synthetic failure"),
            "async-throw" => Task.FromException<bool>(new InvalidOperationException("synthetic failure")),
            _ => Task.FromCanceled<bool>(new CancellationToken(canceled: true)),
        };
        (await RunAsync()).ShouldBe(IdentityHistoryCleanupOutcome.Pending);
        _provider.ReadChecks.ShouldBe(1);
    }

    /// <summary>A stalled destruction or final check has a finite wait and remains pending even after late success.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StalledProvider_TimesOutAsPending(bool finalCheck)
    {
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (finalCheck)
        {
            _provider.ReadHook = _ => pending.Task;
        }
        else
        {
            _provider.DestructionHook = (_, _, _) => pending.Task;
        }

        try
        {
            (await RunAsync().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken))
                .ShouldBe(IdentityHistoryCleanupOutcome.Pending);
        }
        finally
        {
            pending.TrySetResult(!finalCheck);
        }
    }

    /// <summary>A provider blocking before returning its task cannot bypass the cleanup deadline.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SynchronousProviderBlocking_DeadlineRemainsPending(bool finalCheck)
    {
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var returned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> Block()
        {
            entered.TrySetResult();
            bool released = release.Wait(TimeSpan.FromSeconds(10));
            returned.TrySetResult();
            return Task.FromResult(released && !finalCheck);
        }
        if (finalCheck)
        {
            _provider.ReadHook = _ => Block();
        }
        else
        {
            _provider.DestructionHook = (_, _, _) => Block();
        }

        Task<IdentityHistoryCleanupOutcome> running = Task.Run(() => RunAsync(), TestContext.Current.CancellationToken);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            (await running.WaitAsync(TimeSpan.FromSeconds(7), TestContext.Current.CancellationToken))
                .ShouldBe(IdentityHistoryCleanupOutcome.Pending);
        }
        finally
        {
            release.Set();
            await returned.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Caller cancellation remains prompt while either provider invocation blocks synchronously.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SynchronousProviderBlocking_CancellationRemainsPrompt(bool finalCheck)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var returned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> Block()
        {
            entered.TrySetResult();
            bool released = release.Wait(TimeSpan.FromSeconds(10));
            returned.TrySetResult();
            return Task.FromResult(released && !finalCheck);
        }
        if (finalCheck)
        {
            _provider.ReadHook = _ => Block();
        }
        else
        {
            _provider.DestructionHook = (_, _, _) => Block();
        }

        Task<IdentityHistoryCleanupOutcome> running = Task.Run(() => RunAsync(cancellation.Token), TestContext.Current.CancellationToken);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            cancellation.Cancel();
            await Should.ThrowAsync<OperationCanceledException>(() => running.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
        }
        finally
        {
            release.Set();
            await returned.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Lost acknowledgement leaves pending; retry recovers exactly the original immutable receipt.</summary>
    [Fact]
    public async Task LostAcknowledgement_RetryRecoversSameDestroyedUnit()
    {
        _provider.DestructionHook = (identity, evidence, _) =>
        {
            _provider.CompleteDestruction(identity, evidence).ShouldBeTrue();
            return Task.FromException<bool>(new IOException("synthetic lost acknowledgement"));
        };
        (await RunAsync()).ShouldBe(IdentityHistoryCleanupOutcome.Pending);
        _provider.IsDestroyed(_identity, _evidence).ShouldBeTrue();
        _provider.DestructionHook = null;
        (await RunAsync()).ShouldBe(IdentityHistoryCleanupOutcome.ProviderConfirmedDestroyed);
        _provider.Attempts.Count.ShouldBe(2);
        _provider.Attempts.ShouldAllBe(attempt => attempt.Identity == _identity && attempt.Evidence == _evidence);
        _evidence.ExpiresAt.ShouldBe(_effectiveAt.AddDays(365));
    }

    /// <summary>Cleanup does not sweep another tenant or a live successor, even when evidence IDs overlap.</summary>
    [Fact]
    public async Task ExactExpiredUnit_PreservesForeignUnitAndLiveSuccessor()
    {
        var foreign = new AggregateIdentity("tenant-b", "party", _identity.AggregateId);
        IdentityHistoryCustodyEvidence successor = _evidence with { EvidenceId = "receipt-2", ExpiresAt = _clock.Now.AddDays(1), LifecycleRevision = 2 };
        _provider.Register(foreign, _evidence);
        _provider.Register(_identity, successor);
        (await RunAsync()).ShouldBe(IdentityHistoryCleanupOutcome.ProviderConfirmedDestroyed);
        _provider.IsDestroyed(_identity, _evidence).ShouldBeTrue();
        _provider.IsDestroyed(foreign, _evidence).ShouldBeFalse();
        _provider.IsDestroyed(_identity, successor).ShouldBeFalse();
    }

    /// <summary>The trusted provider authenticates tenant/unit references rather than treating flags as authority.</summary>
    [Fact]
    public async Task ForeignReceipt_IsRejectedByScopedProvider()
    {
        var foreign = new AggregateIdentity("tenant-b", "party", _identity.AggregateId);
        (await _cleanup.ProcessAsync(foreign, _policy, _effectiveAt, _evidence, TestContext.Current.CancellationToken))
            .ShouldBe(IdentityHistoryCleanupOutcome.Pending);
        _provider.IsDestroyed(_identity, _evidence).ShouldBeFalse();
        _provider.IsDestroyed(foreign, _evidence).ShouldBeFalse();
        _provider.ReadChecks.ShouldBe(0);
    }

    /// <summary>Pre-cancellation prevents custody calls.</summary>
    [Fact]
    public async Task PreCancelled_DoesNotContactProvider()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => RunAsync(cancellation.Token));
        _provider.Attempts.ShouldBeEmpty();
    }

    /// <summary>Caller cancellation interrupts an ignoring provider without relabeling its late outcome.</summary>
    [Theory]
    [InlineData(false, false)][InlineData(false, true)][InlineData(true, false)][InlineData(true, true)]
    public async Task CancelledWhileProviderIgnoresToken_PreservesCancellation(bool finalCheck, bool lateFault)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> Stall()
        {
            entered.SetResult();
            return pending.Task;
        }
        if (finalCheck)
        {
            _provider.ReadHook = _ => Stall();
        }
        else
        {
            _provider.DestructionHook = (_, _, _) => Stall();
        }

        Task<IdentityHistoryCleanupOutcome> running = RunAsync(cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => running.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
        if (lateFault)
        {
            pending.SetException(new IOException("synthetic late failure"));
        }
        else
        {
            pending.SetResult(!finalCheck);
        }
        running.IsCanceled.ShouldBeTrue();
        _provider.ReadChecks.ShouldBe(finalCheck ? 1 : 0);
    }

    /// <summary>Cancellation at either provider return wins over a success result.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelledAtProviderReturn_DoesNotReturnSuccess(bool finalCheck)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        if (finalCheck)
        {
            _provider.ReadHook = _ => { cancellation.Cancel(); return Task.FromResult(false); };
        }
        else
        {
            _provider.DestructionHook = (_, _, _) => { cancellation.Cancel(); return Task.FromResult(true); };
        }
        await Should.ThrowAsync<OperationCanceledException>(() => RunAsync(cancellation.Token));
        _provider.ReadChecks.ShouldBe(finalCheck ? 1 : 0);
    }
}
