using System.Security.Cryptography;
using Hexalith.EventStore.Contracts.Security;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Custody.Tests;

/// <summary>Executable gate observations with synthetic independent target/storage/destruction ports and real byte encryption; no production qualification is inferred.</summary>
public sealed class Fr34ProtectionGateTests
{
    /// <summary>No-op/wrapper, target substitution, inconsistent bytes, nonterminal destruction and readable replay fail the full gate.</summary>
    [Theory]
    [InlineData("valid", true)]
    [InlineData("no-op", false)]
    [InlineData("base64-wrapper", false)]
    [InlineData("rejected-format", false)]
    [InlineData("rejected-carrier-key", false)]
    [InlineData("tampered-carrier", false)]
    [InlineData("wrong-target", false)]
    [InlineData("foreign-canary", false)]
    [InlineData("foreign-key", false)]
    [InlineData("malformed-metadata", false)]
    [InlineData("wrong-unseal", false)]
    [InlineData("missing-destruction", false)]
    [InlineData("readable-replay", false)]
    [InlineData("wrong-erasure-kind", false)]
    [InlineData("expiry", false)]
    [InlineData("authority-change", false)]
    public async Task FreshFullObservationRequiresEveryIndependentExactProof(string vector, bool expected)
    {
        var fixture = Arrange(vector);
        (await fixture.Gate.EvaluateAsync(TestContext.Current.CancellationToken)).ShouldBe(expected);
        if (vector is "no-op" or "base64-wrapper" or "rejected-format" or "rejected-carrier-key" or "tampered-carrier") { await fixture.Engine.DidNotReceiveWithAnyArgs().UnsealAsync(default!, TestContext.Current.CancellationToken); }
        if (vector is "wrong-target" or "foreign-canary" or "foreign-key") { await fixture.Engine.DidNotReceiveWithAnyArgs().DestroyAsync(default!, TestContext.Current.CancellationToken); }
    }

    /// <summary>Startup, every readiness evaluation and content admission generate distinct canaries; later failure is never cached away.</summary>
    [Fact]
    public async Task StartupEveryReadinessAndContentAdmissionRunFreshCanaries()
    {
        var fixture = Arrange("valid"); var canaries = new HashSet<string>();
        fixture.Engine.When(engine => engine.SealAndPersistAsync(Arg.Any<Fr34ProtectionTarget>(), Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>()))
            .Do(call => canaries.Add(call.ArgAt<string>(1)).ShouldBeTrue());
        await new Fr34ProtectionStartupGate(fixture.Gate).StartAsync(TestContext.Current.CancellationToken);
        var readiness = new Fr34ProtectionReadinessCheck(fixture.Gate);
        (await readiness.CheckHealthAsync(new(), TestContext.Current.CancellationToken)).Status.ShouldBe(HealthStatus.Healthy);
        (await readiness.CheckHealthAsync(new(), TestContext.Current.CancellationToken)).Status.ShouldBe(HealthStatus.Healthy);
        await fixture.Gate.RequireForContentAsync(TestContext.Current.CancellationToken);
        canaries.Count.ShouldBe(4);
        fixture.Authority.ObserveAsync(Arg.Any<Fr34ProtectionTarget>(), Arg.Any<CancellationToken>()).Returns((Fr34CanaryAuthorization?)null);
        (await readiness.CheckHealthAsync(new(), TestContext.Current.CancellationToken)).Status.ShouldBe(HealthStatus.Unhealthy);
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.Gate.RequireForContentAsync(TestContext.Current.CancellationToken));
        canaries.Count.ShouldBe(4);
    }

    /// <summary>A caller cancellation stops a suspended canary dependency promptly and never releases readiness.</summary>
    [Fact]
    public async Task CancelledNoncooperativeSealCannotReleaseReadiness()
    {
        var fixture = Arrange("valid"); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<Fr34CanaryReference?>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Engine.SealAndPersistAsync(Arg.Any<Fr34ProtectionTarget>(), Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(_ => { entered.TrySetResult(); return pending.Task; });
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken); var operation = fixture.Gate.EvaluateAsync(cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        pending.SetResult(null);
        await fixture.Engine.DidNotReceiveWithAnyArgs().UnsealAsync(default!, TestContext.Current.CancellationToken);
    }

    /// <summary>A seal completing after caller cancellation cleans up only its original owned canary and cannot resume the gate.</summary>
    [Fact]
    public async Task LateOwnedSealIsCleanedWithoutResumingReadiness()
    {
        var fixture = Arrange("valid"); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var destroyed = new TaskCompletionSource<Fr34CanaryReference>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<Fr34CanaryReference?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Fr34CanaryReference? original = null;
        fixture.Engine.SealAndPersistAsync(Arg.Any<Fr34ProtectionTarget>(), Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(call => { original = new(call.Arg<Fr34ProtectionTarget>(), call.Arg<string>(), "owned-record", "owned-key"); entered.TrySetResult(); return pending.Task; });
        fixture.Engine.DestroyAsync(Arg.Any<Fr34CanaryReference>(), Arg.Any<CancellationToken>())
            .Returns(call => { destroyed.TrySetResult(call.Arg<Fr34CanaryReference>()); return true; });
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var operation = fixture.Gate.EvaluateAsync(cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        fixture.Authority.VerifyOwnershipAsync(Arg.Any<Fr34ProtectionTarget>(), Arg.Any<string>(), Arg.Any<Fr34CanaryReference>(), Arg.Any<Fr34CanaryAuthorization>(), Arg.Any<CancellationToken>()).Returns(call => call.Arg<Fr34CanaryReference>() == original && call.Arg<string>() == original!.CanaryId);
        var input = (byte[])fixture.Engine.ReceivedCalls().Single(c => c.GetMethodInfo().Name == nameof(IFr34ProtectionCanaryEngine.SealAndPersistAsync)).GetArguments()[2]!;
        input.Any(value => value != 0).ShouldBeTrue();
        pending.SetResult(original);
        (await destroyed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).ShouldBe(original);
        await fixture.Engine.DidNotReceiveWithAnyArgs().UnsealAsync(default!, TestContext.Current.CancellationToken);
        input!.All(value => value == 0).ShouldBeTrue();
    }

    /// <summary>The actual seal input and accepted or invalid returned storage arrays are cleared at their completed ownership boundary.</summary>
    [Theory]
    [InlineData("valid")][InlineData("malformed-metadata")][InlineData("wrong-unseal")][InlineData("readable-replay")]
    public async Task CompletedCanaryInputAndObservationBuffersAreCleared(string vector)
    {
        var f = Arrange(vector); _ = await f.Gate.EvaluateAsync(TestContext.Current.CancellationToken);
        var input = (byte[])f.Engine.ReceivedCalls().Single(c => c.GetMethodInfo().Name == nameof(IFr34ProtectionCanaryEngine.SealAndPersistAsync)).GetArguments()[2]!;
        input.Length.ShouldBeGreaterThan(0); input!.All(value => value == 0).ShouldBeTrue();
        f.ReturnedBytes()!.Length.ShouldBeGreaterThan(0); f.ReturnedBytes()!.All(value => value == 0).ShouldBeTrue();
        f.ReturnedOwnedBuffers().All(bytes => bytes.Length > 0 && bytes.All(value => value == 0)).ShouldBeTrue();
        f.ReturnedOwnedBuffers().Count.ShouldBe(vector == "readable-replay" ? 2 : vector == "malformed-metadata" ? 0 : 1);
    }
    /// <summary>An original canary echo with a foreign dedicated key is independently denied before late cleanup and clears its completed provider input.</summary>
    [Fact]
    public async Task LateForeignKeyNeverDestroysEvenWithTheExactOriginalCanary()
    {
        var f = Arrange("valid"); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var proved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<Fr34CanaryReference?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Fr34CanaryReference? original = null; byte[]? input = null;
        f.Engine.SealAndPersistAsync(Arg.Any<Fr34ProtectionTarget>(), Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns(call =>
        { original = new(call.Arg<Fr34ProtectionTarget>(), call.Arg<string>(), "persisted-canary-" + call.Arg<string>(), "foreign-key"); input = call.Arg<byte[]>(); entered.TrySetResult(); return pending.Task; });
        f.Authority.VerifyOwnershipAsync(Arg.Any<Fr34ProtectionTarget>(), Arg.Any<string>(), Arg.Any<Fr34CanaryReference>(), Arg.Any<Fr34CanaryAuthorization>(), Arg.Any<CancellationToken>()).Returns(_ => { proved.TrySetResult(); return false; });
        using var caller = new CancellationTokenSource(); var operation = f.Gate.EvaluateAsync(caller.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); caller.Cancel();
        (await Should.ThrowAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken))).CancellationToken.ShouldBe(caller.Token);
        input!.Any(value => value != 0).ShouldBeTrue(); pending.SetResult(original);
        await proved.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await f.Gate.CleanupCompletion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        input!.All(value => value == 0).ShouldBeTrue(); await f.Engine.DidNotReceiveWithAnyArgs().DestroyAsync(default!, TestContext.Current.CancellationToken);
    }

    /// <summary>A definitive seal failure clears the exact provider input before denying readiness.</summary>
    [Fact]
    public async Task FailedSealClearsItsCompletedOwnedInput()
    {
        var f = Arrange("valid"); byte[]? input = null;
        f.Engine.SealAndPersistAsync(Arg.Any<Fr34ProtectionTarget>(), Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns(call =>
        { input = call.Arg<byte[]>(); return Task.FromException<Fr34CanaryReference?>(new InvalidOperationException("Controlled provider failure.")); });
        (await f.Gate.EvaluateAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
        input!.Length.ShouldBeGreaterThan(0); input.All(value => value == 0).ShouldBeTrue();
        await f.Engine.DidNotReceiveWithAnyArgs().DestroyAsync(default!, TestContext.Current.CancellationToken);
    }

    /// <summary>Actual storage/opened/replay arrays returned after cancellation are cleared without allowing the abandoned gate to continue.</summary>
    [Theory]
    [InlineData("storage")][InlineData("opened")][InlineData("replay")]
    public async Task CancelledLateObservationClearsTheActualReturnedBuffer(string phase)
    {
        var f = Arrange("valid"); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var storage = new TaskCompletionSource<Fr34PersistedCanary?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var opened = new TaskCompletionSource<byte[]?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var replay = new TaskCompletionSource<PayloadUnprotectionOutcome?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Fr34CanaryReference? original = null;
        if (phase == "storage") { f.Storage.ReadAsync(Arg.Any<Fr34CanaryReference>(), Arg.Any<CancellationToken>()).Returns(call => { original = call.Arg<Fr34CanaryReference>(); entered.TrySetResult(); return storage.Task; }); }
        else if (phase == "opened") { f.Engine.UnsealAsync(Arg.Any<Fr34CanaryReference>(), Arg.Any<CancellationToken>()).Returns(_ => { entered.TrySetResult(); return opened.Task; }); }
        else { f.Engine.ReplayAsync(Arg.Any<Fr34CanaryReference>(), Arg.Any<CancellationToken>()).Returns(_ => { entered.TrySetResult(); return replay.Task; }); }
        using var caller = new CancellationTokenSource(); var operation = f.Gate.EvaluateAsync(caller.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); caller.Cancel();
        (await Should.ThrowAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken))).CancellationToken.ShouldBe(caller.Token);
        var bytes = Enumerable.Repeat((byte)123, 64).ToArray();
        if (phase == "storage") { storage.SetResult(new(original!, bytes, EventStorePayloadProtectionMetadata.Unprotected())); }
        else if (phase == "opened") { opened.SetResult(bytes); }
        else { replay.SetResult(PayloadUnprotectionOutcome.Readable(bytes, "json", EventStorePayloadProtectionMetadata.Unprotected())); }
        using var clearing = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken); clearing.CancelAfter(TimeSpan.FromSeconds(5));
        while (bytes.Any(value => value != 0)) { await Task.Delay(1, clearing.Token); }
        bytes.All(value => value == 0).ShouldBeTrue(); operation.IsCanceled.ShouldBeTrue();
        if (phase == "storage") { await f.Engine.DidNotReceiveWithAnyArgs().UnsealAsync(default!, TestContext.Current.CancellationToken); }
        if (phase != "replay") { await f.Engine.DidNotReceiveWithAnyArgs().ReplayAsync(default!, TestContext.Current.CancellationToken); }
    }

    /// <summary>A suspended independent carrier proof retains its owned bytes only until completion and cannot release readiness after cancellation.</summary>
    [Fact]
    public async Task CancelledCarrierProofPreservesAndClearsItsExactOwnedInput()
    {
        var f = Arrange("valid"); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); byte[]? input = null;
        f.Authority.VerifyCarrierAsync(Arg.Any<Fr34ProtectionTarget>(), Arg.Any<string>(), Arg.Any<Fr34CanaryReference>(), Arg.Any<Fr34PersistedCanary>(), Arg.Any<Fr34CanaryAuthorization>(), Arg.Any<CancellationToken>()).Returns(call =>
        { input = call.Arg<Fr34PersistedCanary>().Bytes; entered.TrySetResult(); return pending.Task; });
        using var caller = new CancellationTokenSource(); var operation = f.Gate.EvaluateAsync(caller.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); caller.Cancel();
        (await Should.ThrowAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken))).CancellationToken.ShouldBe(caller.Token);
        input!.Any(value => value != 0).ShouldBeTrue(); pending.SetResult(true);
        using var clearing = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken); clearing.CancelAfter(TimeSpan.FromSeconds(5));
        while (input!.Any(value => value != 0)) { await Task.Delay(1, clearing.Token); }
        await f.Engine.DidNotReceiveWithAnyArgs().UnsealAsync(default!, TestContext.Current.CancellationToken); operation.IsCanceled.ShouldBeTrue();
    }

    /// <summary>The independent proof receives one detached scan snapshot after the reader transfers and clears its byte array.</summary>
    [Fact]
    public async Task CarrierProofUsesDetachedReaderSnapshot()
    {
        var f = Arrange("valid"); bool checkedSnapshot = false;
        f.Authority.VerifyCarrierAsync(Arg.Any<Fr34ProtectionTarget>(), Arg.Any<string>(), Arg.Any<Fr34CanaryReference>(),
            Arg.Any<Fr34PersistedCanary>(), Arg.Any<Fr34CanaryAuthorization>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            byte[] supplied = f.ReturnedBytes()!; byte[] owned = call.Arg<Fr34PersistedCanary>().Bytes;
            ReferenceEquals(supplied, owned).ShouldBeFalse(); supplied.All(value => value == 0).ShouldBeTrue();
            owned.Any(value => value != 0).ShouldBeTrue(); checkedSnapshot = true; return false;
        });
        (await f.Gate.EvaluateAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
        checkedSnapshot.ShouldBeTrue();
    }

    /// <summary>Actually suspended provider metadata Count/traversal releases cancellation/deadline and cannot reach independent proof or content admission after late completion.</summary>
    [Theory]
    [InlineData(false, false)][InlineData(false, true)][InlineData(true, false)][InlineData(true, true)]
    public async Task SuspendedPersistedMetadataIsBoundedAndNeverReleasesReadiness(bool traversal, bool cancelCaller)
    {
        var clock = Substitute.For<TimeProvider>(); long ticks = 0; var callbacks = new List<Action>();
        clock.GetUtcNow().Returns(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        clock.TimestampFrequency.Returns(TimeSpan.TicksPerSecond); clock.GetTimestamp().Returns(_ => ticks);
        clock.CreateTimer(Arg.Any<TimerCallback>(), Arg.Any<object?>(), Arg.Any<TimeSpan>(), Arg.Any<TimeSpan>()).Returns(call =>
        { callbacks.Add(() => call.Arg<TimerCallback>()(call.ArgAt<object?>(1))); return Substitute.For<ITimer>(); });
        var f = Arrange("valid", clock); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); using var release = new ManualResetEventSlim();
        void Suspend() { entered.TrySetResult(); release.Wait(); completed.TrySetResult(); }
        var flags = Substitute.For<IReadOnlyDictionary<string, string>>();
        flags.Count.Returns(_ => { if (!traversal) { Suspend(); } return 1; });
        IEnumerable<KeyValuePair<string, string>> Enumerate() { Suspend(); yield return new("format", "enrolled-v1"); }
        flags.GetEnumerator().Returns(_ => Enumerate().GetEnumerator());
        byte[] returned = [1, 2, 3];
        f.Storage.ReadAsync(Arg.Any<Fr34CanaryReference>(), Arg.Any<CancellationToken>()).Returns(call => new Fr34PersistedCanary(call.Arg<Fr34CanaryReference>(), returned,
            new(PayloadProtectionState.Protected, 1, "actual-fixture", "key-v1", null, flags)));
        using var caller = new CancellationTokenSource(); var pending = f.Gate.EvaluateAsync(caller.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        try
        {
            if (cancelCaller) { caller.Cancel(); (await Should.ThrowAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken))).CancellationToken.ShouldBe(caller.Token); }
            else { ticks = TimeSpan.FromSeconds(30).Ticks; foreach (var callback in callbacks.ToArray()) { callback(); } (await pending.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken)).ShouldBeFalse(); }
            returned.All(value => value == 0).ShouldBeTrue(); // Caller release retires the transferred array while capture remains blocked.
        }
        finally { release.Set(); }
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        using (var clearing = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken))
        {
            clearing.CancelAfter(TimeSpan.FromSeconds(5));
            while (returned.Any(value => value != 0)) { await Task.Delay(1, clearing.Token); }
        }
        await f.Authority.DidNotReceiveWithAnyArgs().VerifyCarrierAsync(default!, default!, default!, default!, default!, TestContext.Current.CancellationToken);
        await f.Engine.DidNotReceiveWithAnyArgs().UnsealAsync(default!, TestContext.Current.CancellationToken);
    }

    private static (Fr34ProtectionGate Gate, IFr34ProtectionCanaryEngine Engine, IFr34CanaryAuthority Authority, IFr34PersistedCanaryReader Storage, Func<byte[]?> ReturnedBytes, Func<IReadOnlyList<byte[]>> ReturnedOwnedBuffers) Arrange(string vector, TimeProvider? clockOverride = null)
    {
        var clock = new CustodyFixtureClock(); var target = new Fr34ProtectionTarget("engine", "immutable-v1", "custody-a", "canary-tenant");
        TimeProvider operationClock = clockOverride ?? clock;
        var engine = Substitute.For<IFr34ProtectionCanaryEngine>(); var storage = Substitute.For<IFr34PersistedCanaryReader>();
        var authority = Substitute.For<IFr34CanaryAuthority>();
        var authorization = new Fr34CanaryAuthorization(target, "current-authority", operationClock.GetUtcNow(), operationClock.GetUtcNow().AddMinutes(1));
        int observations = 0;
        authority.ObserveAsync(target, Arg.Any<CancellationToken>()).Returns(_ =>
            vector == "authority-change" && ++observations > 1 ? authorization with { AuthorityRevision = "changed-authority" } : authorization);
        byte[]? plaintext = null; byte[]? verificationKey = null; string? originalCanary = null; byte[]? sealedBytes = null; byte[]? returnedBytes = null; var returnedOwnedBuffers = new List<byte[]>();
        engine.SealAndPersistAsync(target, Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            plaintext = call.ArgAt<byte[]>(2).ToArray(); originalCanary = call.ArgAt<string>(1);
            byte[] key = RandomNumberGenerator.GetBytes(32), nonce = RandomNumberGenerator.GetBytes(12), tag = new byte[16], cipher = new byte[plaintext.Length];
            using var aes = new AesGcm(key, 16); aes.Encrypt(nonce, plaintext, cipher, tag);
            if (verificationKey is not null) { CryptographicOperations.ZeroMemory(verificationKey); }
            verificationKey = key.ToArray();
            sealedBytes = vector == "no-op" ? plaintext.ToArray() : vector == "base64-wrapper" ? System.Text.Encoding.UTF8.GetBytes(Convert.ToBase64String(plaintext)) : nonce.Concat(cipher).Concat(tag).ToArray();
            if (vector == "tampered-carrier") { sealedBytes[^1] ^= 1; }
            CryptographicOperations.ZeroMemory(key);
            return new Fr34CanaryReference(vector == "wrong-target" ? target with { EngineVersion = "uncommitted-version" } : target,
                vector == "foreign-canary" ? "foreign-canary" : call.ArgAt<string>(1), "persisted-canary-" + call.ArgAt<string>(1), vector == "foreign-key" ? "foreign-key" : "dedicated-canary-dek");
        });
        storage.ReadAsync(Arg.Any<Fr34CanaryReference>(), Arg.Any<CancellationToken>()).Returns(call =>
            new Fr34PersistedCanary(call.Arg<Fr34CanaryReference>(), returnedBytes = sealedBytes!.ToArray(), new(PayloadProtectionState.Protected, 1,
                vector == "malformed-metadata" ? "malformed\ncarrier" : vector == "rejected-format" ? "foreign-format" : "actual-fixture", vector == "rejected-carrier-key" ? "foreign-key-alias" : "key-v1", null, null)));
        engine.UnsealAsync(Arg.Any<Fr34CanaryReference>(), Arg.Any<CancellationToken>()).Returns(_ =>
        { var opened = vector == "wrong-unseal" ? new byte[plaintext!.Length] : plaintext!.ToArray(); returnedOwnedBuffers.Add(opened); return opened; });
        engine.DestroyAsync(Arg.Any<Fr34CanaryReference>(), Arg.Any<CancellationToken>()).Returns(true);
        authority.VerifyOwnershipAsync(target, Arg.Any<string>(), Arg.Any<Fr34CanaryReference>(), Arg.Any<Fr34CanaryAuthorization>(), Arg.Any<CancellationToken>()).Returns(call =>
            call.Arg<Fr34CanaryReference>() is { } value && value.Target == target && value.CanaryId == call.Arg<string>()
            && value.RecordId == "persisted-canary-" + value.CanaryId && value.KeyReference == "dedicated-canary-dek");
        authority.VerifyCarrierAsync(target, Arg.Any<string>(), Arg.Any<Fr34CanaryReference>(), Arg.Any<Fr34PersistedCanary>(), Arg.Any<Fr34CanaryAuthorization>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var reference = call.Arg<Fr34CanaryReference>(); var observed = call.Arg<Fr34PersistedCanary>(); var admitted = call.Arg<Fr34CanaryAuthorization>();
            if (admitted != authorization || admitted.ValidUntil <= operationClock.GetUtcNow() || reference.Target != target || reference.CanaryId != originalCanary
                || originalCanary != call.Arg<string>() || reference.KeyReference != "dedicated-canary-dek" || observed.Reference != reference
                || observed.Metadata.Scheme != "actual-fixture" || observed.Metadata.KeyAlias != "key-v1" || observed.Bytes.Length < 28) { return false; }
            var independentlyOpened = new byte[observed.Bytes.Length - 28];
            try
            {
                using var enrolledAes = new AesGcm(verificationKey!, 16);
                enrolledAes.Decrypt(observed.Bytes.AsSpan(0, 12), observed.Bytes.AsSpan(12, independentlyOpened.Length), observed.Bytes.AsSpan(observed.Bytes.Length - 16), independentlyOpened);
                return CryptographicOperations.FixedTimeEquals(independentlyOpened, plaintext!);
            }
            catch (CryptographicException) { return false; }
            finally { CryptographicOperations.ZeroMemory(independentlyOpened); }
        });
        authority.ConfirmDestroyedAsync(Arg.Any<Fr34CanaryReference>(), Arg.Any<CancellationToken>()).Returns(vector != "missing-destruction");
        engine.ReplayAsync(Arg.Any<Fr34CanaryReference>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            if (vector == "expiry") { clock.Now = authorization.ValidUntil; }
            if (vector == "readable-replay")
            { var bytes = plaintext!.ToArray(); returnedOwnedBuffers.Add(bytes); return PayloadUnprotectionOutcome.Readable(bytes, "json", EventStorePayloadProtectionMetadata.Unprotected()); }
            return PayloadUnprotectionOutcome.Unreadable(vector == "wrong-erasure-kind" ? UnreadableProtectedDataReason.MissingKey : UnreadableProtectedDataReason.KeyInvalidatedOrDeleted);
        });
        return (new(target, engine, storage, authority, operationClock), engine, authority, storage, () => returnedBytes, () => returnedOwnedBuffers);
    }
}
