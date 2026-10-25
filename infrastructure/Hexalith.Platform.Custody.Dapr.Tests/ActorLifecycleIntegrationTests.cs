using System.Reflection;
using System.Text;
using System.Text.Json;
using Dapr.Actors.Runtime;
using Dapr.Client;
using Grpc.Net.Client;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Custody.Dapr;
using Hexalith.Platform.Secrets.Runtime;

internal static class ActorLifecycleIntegrationTests
{
    internal static async Task Run()
    {
        string scope = CustodyKeyLifecycleActor.GetActorId("tenant") + "|custody-key-lifecycle-candidate-v1";
        var genesis = new CustodyKeyLifecycleLedger("tenant", 0, []);
        using var f = new Fixture(CustodyBindingContract.Hash(JsonSerializer.SerializeToUtf8Bytes(genesis)), scope);
        var handler = new SdkBridge(f);
        using var sdk = new DaprClientBuilder().UseGrpcEndpoint("http://127.0.0.1:1").UseGrpcChannelOptions(new GrpcChannelOptions { HttpHandler = handler }).Build();
        var client = PublicLifecycleTests.Client(f, sdk, new LifecycleAuthorizer(f));
        var originals = new RegistrationMap();
        var physical = new DaprCustodyKeyLifecycleProvider(client, new ManifestLocator(null));
        var owner = new CustodyOwnerAuthority(sdk, "custody", "tenant", scope, Encoding.UTF8.GetString(CustodyJson.Bytes(f.Profile.Enrollment)), f.Profile.ComponentPublicKeyPem, new OwnerCalls(f));
        var authority = new DaprCustodyKeyLifecycleAuthority("tenant", owner, client, physical, originals);
        var gate = new DeferredProvider(physical);
        var actor = new CustodyKeyLifecycleActor(ActorHost.CreateForTest<CustodyKeyLifecycleActor>(new ActorTestOptions { ActorId = new(CustodyKeyLifecycleActor.GetActorId("tenant")) }), authority, gate);
        var state = DispatchProxy.Create<IActorStateManager, SerializedActorState>();
        typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance)!.SetValue(actor, state);
        for (int revision = 0; revision < 2; revision++)
        {
            var identity = new CustodyKeyObjectIdentity("tenant", "actor-" + revision, PlatformKeyPurpose.InteractionRootDek, "alias", "key", "lifecycle", "store", "contract");
            var use = new CustodyKeyUse("actor-wrap-" + revision, "actor-delivery-" + revision, "recipient", "transport", identity.ObjectId);
            var registration = await client.WrapAsync(identity, "actor-wrap-" + revision, revision, "kek", identity.ObjectId, use, Fixture.Root)
                ?? throw new Exception("Diverged owner revision failed wrap.");
            originals.Items.Add(identity, registration);
            Check(registration.ExpectedRevision == revision && f.Store.State.Head.Revision != revision, "Original registration revision was rewritten to custody head.");
            Check((await actor.RegisterWrappedAsync(registration)).Status == CustodyKeyLifecycleStatus.Wrapped, "Actual actor rejected original registration with diverged revisions.");
        }
        var registered = originals.Items.Values.First();
        var pin = new CustodyKeyLifecycleRequest(registered.Identity, "actor-pin", CustodyKeyLifecycleAction.Pin, 2, "hold", "registered", 2, "decision");
        byte[] originalBytes = JsonSerializer.SerializeToUtf8Bytes(pin);
        Check((await actor.ApplyAsync(pin)).Status == CustodyKeyLifecycleStatus.Pinned, "Actual actor pin failed after pending-state transition advanced custody head.");
        Check(JsonSerializer.SerializeToUtf8Bytes(pin).AsSpan().SequenceEqual(originalBytes), "Original pin bytes were rewritten.");
        var recovered = await actor.LookupAsync(pin);
        Check(recovered.Status == CustodyKeyLifecycleStatus.Pinned && recovered.RequestDigest == CustodyBindingContract.Hash(originalBytes), "Actual actor original recovery changed request bytes.");
        var unpin = pin with { OperationId = "actor-unpin", Action = CustodyKeyLifecycleAction.Unpin, ExpectedRevision = 4 };
        Check((await actor.ApplyAsync(unpin)).Status == CustodyKeyLifecycleStatus.Unpinned, "Actual actor unpin failed with independent revisions.");

        // A pending pin may be cleared only by an exact retained authenticated never-performed original.
        var notPerformed = pin with { OperationId = "actor-never-performed", ExpectedRevision = 6 };
        gate.DeferNext = true;
        Check((await actor.ApplyAsync(notPerformed)).Status == CustodyKeyLifecycleStatus.Unknown, "Deferred pin did not remain pending.");
        Check(f.Store.State.Checkpoint.Pins.Count == 0, "Deferred fixture issued a native pin.");
        string digest = CustodyBindingContract.Hash(JsonSerializer.SerializeToUtf8Bytes(notPerformed));
        var outcome = new CustodyKeyLifecycleOutcome(registered.Identity, notPerformed.OperationId, digest, CustodyKeyLifecycleStatus.NotPerformed, "retained-never-performed");
        var response = new CustodyBindingResponseV1(CustodyBindingContract.Protocol, outcome.OperationId, digest, Lifecycle: outcome);
        var current = await f.Store.ReadAsync("tenant", default); var next = SemanticProvider.Clone(current);
        var reference = await f.Store.PutImmutableAsync(CustodyJson.Bytes(response), "original", default);
        next.Originals.Add(outcome.OperationId, new(digest, "pin", reference, null)); await f.Store.CommitAsync(current, next, default);
        Check(await authority.VerifyOutcomeAsync(notPerformed, outcome), "Exact retained authenticated NotPerformed was rejected.");
        Check(!await authority.VerifyOutcomeAsync(notPerformed, outcome with { ReceiptId = "invented-absence" }), "Changed never-performed receipt was accepted.");
        Check((await actor.LookupAsync(notPerformed)) == outcome, "Actual actor could not retain never-performed original.");
        var again = pin with { OperationId = "actor-pin-after-cleanup", ExpectedRevision = 8 };
        Check((await actor.ApplyAsync(again)).Status == CustodyKeyLifecycleStatus.Pinned, "Authenticated NotPerformed did not remove the pending pin.");
        Check(f.Native.Encrypts == 2 && f.Native.Schedules == 0, "Actor integration regenerated roots or destroyed native keys.");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private sealed class RegistrationMap : ICustodyKeyRegistrationSource
    {
        internal Dictionary<CustodyKeyObjectIdentity, CustodyKeyRegistration> Items = [];
        public Task<CustodyKeyRegistration?> FindAsync(CustodyKeyObjectIdentity identity, CancellationToken token)
            => Task.FromResult(Items.GetValueOrDefault(identity));
    }
    private sealed class DeferredProvider(ICustodyKeyLifecycleProvider inner) : ICustodyKeyLifecycleProvider
    {
        internal bool DeferNext;
        public Task<CustodyKeyLifecycleOutcome> ExecuteAsync(CustodyKeyRegistration registration, CustodyKeyLifecycleRequest request, string digest, CancellationToken token)
        {
            if (!DeferNext) return inner.ExecuteAsync(registration, request, digest, token);
            DeferNext = false; return Task.FromResult(new CustodyKeyLifecycleOutcome(request.Identity, request.OperationId, digest, CustodyKeyLifecycleStatus.Unknown));
        }
        public Task<CustodyKeyLifecycleOutcome> LookupAsync(CustodyKeyRegistration registration, CustodyKeyLifecycleRequest request, string digest, CancellationToken token)
            => inner.LookupAsync(registration, request, digest, token);
    }
}

// Serialized local actor-state double. All authority/effects still use actual SDK and BindingService calls.
public class SerializedActorState : DispatchProxy
{
    private readonly Dictionary<string, byte[]> _committed = [], _pending = [];
    private readonly HashSet<string> _removed = [];
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        args ??= []; string name = args.Length > 0 && args[0] is string value ? value : "";
        switch (method!.Name)
        {
            case "ClearCacheAsync": _pending.Clear(); _removed.Clear(); return Task.CompletedTask;
            case "SetStateAsync": _pending[name] = JsonSerializer.SerializeToUtf8Bytes(args[1], method.GetGenericArguments()[0]); _removed.Remove(name); return Task.CompletedTask;
            case "TryGetStateAsync": return typeof(SerializedActorState).GetMethod(nameof(ReadAsync), BindingFlags.NonPublic | BindingFlags.Instance)!.MakeGenericMethod(method.GetGenericArguments()).Invoke(this, [name]);
            case "TryRemoveStateAsync": bool exists = _pending.ContainsKey(name) || _committed.ContainsKey(name); _removed.Add(name); _pending.Remove(name); return Task.FromResult(exists);
            case "SaveStateAsync": foreach (string key in _removed) _committed.Remove(key); foreach (var pair in _pending) _committed[pair.Key] = pair.Value; _pending.Clear(); _removed.Clear(); return Task.CompletedTask;
            default: throw new NotSupportedException(method.Name);
        }
    }
    private Task<ConditionalValue<T>> ReadAsync<T>(string name)
    {
        byte[]? bytes = _removed.Contains(name) ? null : _pending.GetValueOrDefault(name) ?? _committed.GetValueOrDefault(name);
        return Task.FromResult(bytes is null ? new ConditionalValue<T>(false, default!) : new(true, JsonSerializer.Deserialize<T>(bytes)!));
    }
}
