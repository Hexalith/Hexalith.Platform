using Dapr.Client;
using Grpc.Net.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Custody.Dapr;
using Hexalith.Platform.Secrets.Runtime;

internal static class PublicLifecycleCancellationTests
{
    internal static async Task Run()
    {
        using var f = new Fixture(); var registration = (await f.Call(f.Wrap("budget-root"))).WrappedRegistration!;
        var handler = new SdkBridge(f);
        using var sdk = new DaprClientBuilder().UseGrpcEndpoint("http://127.0.0.1:1").UseGrpcChannelOptions(new GrpcChannelOptions { HttpHandler = handler }).Build();
        using (var caller = new CancellationTokenSource())
        {
            var source = new LifecycleAuthorizer(f) { Stage = _ => caller.Cancel() };
            await Cancelled(() => PublicLifecycleTests.Client(f, sdk, source).LookupOriginalAsync(registration, cancellationToken: caller.Token), caller.Token);
        }
        var use = new CustodyKeyUse("cancel-key-lease", "cancel-key-delivery", "recipient", "transport", registration.OpaqueWrappedKeyReference);
        using (var caller = new CancellationTokenSource())
        {
            handler.AfterResponse = caller.Cancel;
            await Cancelled(() => PublicLifecycleTests.Client(f, sdk, new LifecycleAuthorizer(f)).UnwrapAsync(registration, "cancel-key", use, caller.Token), caller.Token);
            handler.AfterResponse = null;
        }
        if (f.Native.Decrypts != 1) throw new Exception("Cancellation test missed the actual key release boundary.");
        var retry = await PublicLifecycleTests.Client(f, sdk, new LifecycleAuthorizer(f)).UnwrapAsync(registration, "cancel-key", use);
        if (retry.Key is not null || f.Native.Decrypts != 1) throw new Exception("Cancelled key response retry repeated native release.");

        var original = f.Manifest([registration]).Manifest!.Original;
        var request = new CustodyKeyLifecycleRequest(registration.Identity, "budget-destroy", CustodyKeyLifecycleAction.Destroy, 0, null, "barrier", 3, "decision", "reservation");
        string digest = CustodyBindingContract.Hash(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(request));
        var clock = new BudgetClock(); var staged = new LifecycleAuthorizer(f) { Stage = _ => clock.Advance(TimeSpan.FromSeconds(10)) };
        var locator = new CallbackManifestSource(() => { clock.Advance(TimeSpan.FromSeconds(9)); return Task.FromResult<DeletionBatchConsumptionRequest?>(original); });
        var provider = new DaprCustodyKeyLifecycleProvider(PublicLifecycleTests.Client(f, sdk, staged, clock), locator);
        int before = handler.Calls;
        if ((await provider.ExecuteAsync(registration, request, digest)).Status != CustodyKeyLifecycleStatus.Unknown
            || locator.Calls != 1 || handler.Calls != before + 1 || f.Store.State.Checkpoint.Cycle is not null)
            throw new Exception("Nested registration/locator/manifest stages restarted the operation deadline.");

        clock = new BudgetClock(); var blocked = new CallbackManifestSource(() => { clock.Advance(TimeSpan.FromSeconds(31)); return new TaskCompletionSource<DeletionBatchConsumptionRequest?>().Task; });
        provider = new(PublicLifecycleTests.Client(f, sdk, new LifecycleAuthorizer(f), clock), blocked);
        if ((await provider.LookupAsync(registration, request, digest)).Status != CustodyKeyLifecycleStatus.Unknown) throw new Exception("Unresponsive locator did not end as Unknown at the shared deadline.");
        using (var caller = new CancellationTokenSource())
        {
            var cancelled = new CallbackManifestSource(() => { caller.Cancel(); return Task.FromResult<DeletionBatchConsumptionRequest?>(null); });
            provider = new(PublicLifecycleTests.Client(f, sdk, new LifecycleAuthorizer(f)), cancelled);
            await Cancelled(() => provider.LookupAsync(registration, request, digest, caller.Token), caller.Token);
        }

        // Header lookup and every page share the original budget. The deliberately unfinished fixture
        // original must never be parsed/exposed after authorization has exhausted that budget.
        var pageRequest = f.Manifest([registration]);
        var current = await f.Store.ReadAsync("tenant", default); var next = SemanticProvider.Clone(current);
        var retained = await f.Store.PutImmutableAsync(new byte[600 * 1024], "original", default);
        next.Originals.Add(pageRequest.Envelope.OperationId, new(pageRequest.Envelope.RequestDigest, "destroy-manifest", retained, pageRequest));
        await f.Store.CommitAsync(current, next, default);
        clock = new BudgetClock(); var pages = new LifecycleAuthorizer(f) { Stage = _ => clock.Advance(TimeSpan.FromSeconds(5)) };
        before = handler.Calls;
        var assembled = await PublicLifecycleTests.Client(f, sdk, pages, clock).ExecuteManifestOriginalAsync(original, "reservation", true);
        if (assembled.Result.State != DeletionManifestProviderState.Unknown || handler.Calls != before + 2
            || clock.GetTimestamp() != TimeSpan.FromSeconds(30).Ticks)
            throw new Exception("Original header/page stages restarted the shared operation deadline.");
    }
    private static async Task Cancelled(Func<Task> action, CancellationToken token)
    {
        try { await action(); } catch (OperationCanceledException e) when (token.IsCancellationRequested && e.CancellationToken == token) { return; }
        throw new Exception("Explicit caller cancellation became a refusal or Unknown.");
    }
    private sealed class CallbackManifestSource(Func<Task<DeletionBatchConsumptionRequest?>> callback) : ICustodyReservedManifestSource
    {
        internal int Calls;
        public Task<DeletionBatchConsumptionRequest?> FindAsync(string reservationReceiptId, CancellationToken token) { Calls++; return callback(); }
    }
}

internal sealed class BudgetClock : TimeProvider
{
    private long _ticks;
    private readonly List<BudgetTimer> _timers = [];
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => _ticks;
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(_ticks);
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
