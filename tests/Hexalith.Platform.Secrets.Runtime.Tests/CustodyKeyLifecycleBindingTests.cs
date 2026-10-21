using System.Security.Cryptography;
using System.Text;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Custody.Component;
using Hexalith.Platform.Secrets.Runtime;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class CustodyKeyLifecycleBindingTests
{
    private static readonly byte[] Kek = Enumerable.Repeat((byte)9, 32).ToArray();

    [Fact]
    public async Task PinUnpinAndRootDestroyUseBindingOperations_LookupSendsNoEffect()
    {
        using var store = Store(out var witness, out var target, out var other);
        var binding = new CustodyComponentBindingCall(store);
        var provider = new CustodyKeyLifecycleBindingProvider(binding, witness, store);
        var registration = Registration(target);
        var pin = Request(target, "pin-1", CustodyKeyLifecycleAction.Pin, "hold-a");
        string pinDigest = Digest("pin-1");
        (await provider.ExecuteAsync(registration, pin, pinDigest, TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyKeyLifecycleStatus.Pinned);
        (await provider.LookupAsync(registration, pin, pinDigest, TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyKeyLifecycleStatus.Pinned);
        var unpin = Request(target, "unpin-1", CustodyKeyLifecycleAction.Unpin, "hold-a");
        (await provider.ExecuteAsync(registration, unpin, Digest("unpin-1"), TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyKeyLifecycleStatus.Unpinned);
        binding.Operations.ShouldBe(["pin", "lookup-original", "unpin"]);

        var manifest = Original("reservation-root", target);
        witness.TryAdmit(manifest);
        var destroy = new CustodyKeyLifecycleRequest(target, "destroy-1", CustodyKeyLifecycleAction.Destroy, 1, null, "fence-token", 2, "decision", manifest.ReservationId);
        var destroyed = await provider.ExecuteAsync(registration, destroy, Digest("destroy-1"), TestContext.Current.CancellationToken);
        destroyed.Status.ShouldBe(CustodyKeyLifecycleStatus.Destroyed);
        (await provider.LookupAsync(registration, destroy, Digest("destroy-1"), TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyKeyLifecycleStatus.Destroyed);
        binding.Operations.ShouldBe(["pin", "lookup-original", "unpin", "destroy-manifest", "lookup-original"]);
        store.PhysicalCuts.ShouldBe(1);
        Decrypts(store, target).ShouldBeFalse();
        Decrypts(store, other).ShouldBeTrue();
        binding.Operations.ShouldNotContain("wrap");
        binding.Operations.ShouldNotContain("unwrap");
    }

    [Fact]
    public async Task AbsentBindingAndNullWitnessWriteNothing_TransportSuccessIsNotAReceipt()
    {
        using var store = Store(out var witness, out var target, out var other);
        int writes = store.Mutations;
        var absent = new CustodyComponentBindingCall(store, present: false);
        var provider = new CustodyKeyLifecycleBindingProvider(absent, witness, store);
        var request = Request(target, "pin-absent", CustodyKeyLifecycleAction.Pin, "hold-a");
        (await provider.ExecuteAsync(Registration(target), request, Digest("pin-absent"), TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyKeyLifecycleStatus.Unavailable);
        absent.Operations.ShouldBeEmpty();
        store.Mutations.ShouldBe(writes);
        store.PhysicalCuts.ShouldBe(0);

        var unwitnessed = new CustodyKeyLifecycleBindingProvider(new CustodyComponentBindingCall(store), null, store);
        (await unwitnessed.ExecuteAsync(Registration(target), request, Digest("pin-absent"), TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyKeyLifecycleStatus.Unavailable);
        store.Mutations.ShouldBe(writes);

        var recording = new RecordingCustodyBinding();
        var claimed = new CustodyKeyLifecycleBindingProvider(recording, witness, store);
        var destroy = new CustodyKeyLifecycleRequest(target, "claimed", CustodyKeyLifecycleAction.Destroy, 1, null, "fence-token", 2, "decision", "missing-reservation");
        (await claimed.ExecuteAsync(Registration(target), destroy, Digest("claimed"), TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyKeyLifecycleStatus.Unavailable);
        recording.Commands.ShouldBeEmpty();
        Decrypts(store, target).ShouldBeTrue();

        witness.TryAdmit(Original("claimed-reservation", target));
        destroy = destroy with { ProtectionReservationReceiptId = "claimed-reservation" };
        recording.Claim = new CustodyComponentOutcome(CustodyComponentStatus.Destroyed, destroy.OperationId, Digest("claimed"), "fake-receipt", "fake-barrier", Identity: target);
        (await claimed.ExecuteAsync(Registration(target), destroy, Digest("claimed"), TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyKeyLifecycleStatus.Unknown);
        recording.Commands.Single().Operation.ShouldBe("destroy-manifest");
        store.PhysicalCuts.ShouldBe(0);
        Decrypts(store, target).ShouldBeTrue();

        var forged = Request(target, "forged-conflict", CustodyKeyLifecycleAction.Pin, "hold-a");
        recording.Commands.Clear();
        recording.Claim = new CustodyComponentOutcome(CustodyComponentStatus.Conflict, forged.OperationId, Digest("forged-conflict"), Identity: target);
        (await claimed.ExecuteAsync(Registration(target), forged, Digest("forged-conflict"), TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyKeyLifecycleStatus.Unknown);
        store.PhysicalCuts.ShouldBe(0);

        witness.TryAdmit(Original("reservation-forged", target));
        var forgedBlock = new CustodyKeyLifecycleRequest(target, "forged-block", CustodyKeyLifecycleAction.Destroy, 1, null, "fence-token", 2, "decision", "reservation-forged");
        recording.Claim = new CustodyComponentOutcome(CustodyComponentStatus.BlockedByPin, forgedBlock.OperationId, Digest("forged-block"), Identity: target);
        (await claimed.ExecuteAsync(Registration(target), forgedBlock, Digest("forged-block"), TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyKeyLifecycleStatus.Unknown);
        store.PhysicalCuts.ShouldBe(0);
        Decrypts(store, target).ShouldBeTrue();

        witness.TryAdmit(Original("reservation-absent", other));
        var absentKey = new CustodyKeyLifecycleRequest(target, "destroy-absent", CustodyKeyLifecycleAction.Destroy, 1, null, "fence-token", 2, "decision", "reservation-absent");
        recording.Commands.Clear();
        (await claimed.ExecuteAsync(Registration(target), absentKey, Digest("destroy-absent"), TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyKeyLifecycleStatus.Conflict);
        recording.Commands.ShouldBeEmpty();
        store.PhysicalCuts.ShouldBe(0);
    }

    [Fact]
    public async Task NullWitnessDenies_AndSelfReportIsNotDestruction()
    {
        var denied = new CustodyKeyLifecycleBindingAuthority(null);
        var request = Request(Key("interaction-0001"), "op", CustodyKeyLifecycleAction.Destroy, null);
        var forged = new CustodyKeyLifecycleOutcome(request.Identity, request.OperationId, Digest("op"), CustodyKeyLifecycleStatus.Destroyed, "fake-receipt", "fake-barrier");
        var transition = new AnchoredStateTransition("scope", 0, 1, Digest("a"), Digest("b"), new byte[] { 1 });
        (await denied.VerifyOutcomeAsync(request, forged, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await denied.AuthorizeOperationAsync(request.Identity, "op", "ApplyKeyLifecycle", Digest("op"), TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await denied.VerifyRegistrationAsync(Registration(request.Identity), TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await denied.AuthorizeEffectAsync(request, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await denied.ValidateStateAsync("tenant-a", 1, Digest("state"), TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await denied.RecordRevisionAsync("tenant-a", 0, 1, Digest("state"), TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await denied.AdmitTransitionAsync(transition, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await denied.RecoverTransitionAsync(transition, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await denied.RecordTransitionAsync(transition, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await denied.VerifyTransitionAsync(transition, TestContext.Current.CancellationToken)).ShouldBeFalse();

        using var store = Store(out var witness, out var target, out var other);
        var authority = new CustodyKeyLifecycleBindingAuthority(witness);
        var real = request with { Identity = target, ProtectionReservationReceiptId = "reservation-auth" };
        (await authority.VerifyOutcomeAsync(real, forged with { Identity = target }, TestContext.Current.CancellationToken)).ShouldBeFalse();
        witness.TryAdmit(Original("reservation-auth", target));
        var provider = new CustodyKeyLifecycleBindingProvider(new CustodyComponentBindingCall(store), witness, store);
        var destroyed = await provider.ExecuteAsync(Registration(target), real, Digest("op"), TestContext.Current.CancellationToken);
        destroyed.Status.ShouldBe(CustodyKeyLifecycleStatus.Destroyed);
        (await authority.VerifyOutcomeAsync(real, destroyed, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await authority.VerifyOutcomeAsync(real, destroyed with { ReceiptId = "substituted" }, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await authority.AuthorizeEffectAsync(real, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await authority.VerifyRegistrationAsync(Registration(target), TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await authority.AdmitTransitionAsync(new("scope", 0, 1, Digest("a"), Digest("b"), new byte[] { 1 }), TestContext.Current.CancellationToken)).ShouldBeFalse();
        Decrypts(store, other).ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteConflictAndPinnedDestroy_MissingLookupIsUnavailable()
    {
        using var store = Store(out var witness, out var target, out var other);
        var provider = new CustodyKeyLifecycleBindingProvider(new CustodyComponentBindingCall(store), witness, store);
        var registration = Registration(target);
        var pin = Request(target, "pin-1", CustodyKeyLifecycleAction.Pin, "hold-a");
        (await provider.ExecuteAsync(registration, pin, Digest("pin-1"), TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyKeyLifecycleStatus.Pinned);
        var duplicate = Request(target, "pin-2", CustodyKeyLifecycleAction.Pin, "hold-a");
        (await provider.ExecuteAsync(registration, duplicate, Digest("pin-2"), TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyKeyLifecycleStatus.Conflict);
        var missing = Request(target, "unpin-missing", CustodyKeyLifecycleAction.Unpin, "missing");
        (await provider.ExecuteAsync(registration, missing, Digest("unpin-missing"), TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyKeyLifecycleStatus.Conflict);
        (await provider.LookupAsync(registration, pin, Digest("pin-1"), TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyKeyLifecycleStatus.Pinned);

        var manifest = Original("reservation-pinned", target);
        witness.TryAdmit(manifest);
        var destroy = new CustodyKeyLifecycleRequest(target, "destroy-pinned", CustodyKeyLifecycleAction.Destroy, 1, null, "fence-token", 2, "decision", manifest.ReservationId);
        (await provider.ExecuteAsync(registration, destroy, Digest("destroy-pinned"), TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyKeyLifecycleStatus.BlockedByPin);
        var otherHold = Request(target, "unpin-other", CustodyKeyLifecycleAction.Unpin, "hold-b");
        (await provider.ExecuteAsync(registration, otherHold, Digest("unpin-other"), TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyKeyLifecycleStatus.Conflict);
        (await provider.ExecuteAsync(registration, destroy, Digest("destroy-pinned"), TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyKeyLifecycleStatus.BlockedByPin);
        Decrypts(store, target).ShouldBeTrue();
        Decrypts(store, other).ShouldBeTrue();
        store.PhysicalCuts.ShouldBe(0);

        var never = Request(target, "never-stored", CustodyKeyLifecycleAction.Pin, "hold-z");
        (await provider.LookupAsync(registration, never, Digest("never-stored"), TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyKeyLifecycleStatus.Unavailable);
        witness.Available = false;
        (await provider.LookupAsync(registration, never, Digest("never-stored"), TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyKeyLifecycleStatus.Unknown);
    }

    [Fact]
    public async Task RestoredVolumeStillFailsClosedThroughTheProvider()
    {
        using var store = Store(out var witness, out var target, out var other);
        byte[] snapshot = store.CaptureVolume();
        var manifest = Original("reservation-restore", target);
        witness.TryAdmit(manifest);
        var provider = new CustodyKeyLifecycleBindingProvider(new CustodyComponentBindingCall(store), witness, store);
        var destroy = new CustodyKeyLifecycleRequest(target, "destroy-restore", CustodyKeyLifecycleAction.Destroy, 1, null, "fence-token", 2, "decision", manifest.ReservationId);
        (await provider.ExecuteAsync(Registration(target), destroy, Digest("destroy-restore"), TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyKeyLifecycleStatus.Destroyed);
        store.RestoreVolume(snapshot);
        store.TryRepromoteEpoch("g0").ShouldBeFalse();
        Decrypts(store, target).ShouldBeFalse();
        Decrypts(store, other).ShouldBeTrue();
        witness.Available = false;
        (await provider.LookupAsync(Registration(target), destroy, Digest("destroy-restore"), TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyKeyLifecycleStatus.Unknown);
    }

    [Fact]
    public async Task UnconfiguredDaprRoleRefusesBeforeTheClient_AndDoesNotSortAManifest()
    {
        var client = Substitute.For<DaprClient>();
        var call = new CustodyBindingDaprCall(client, new Dictionary<string, string>(), new CustodyBindingFixture.Authority { Allowed = false });
        call.Present.ShouldBeFalse();
        var identity = Key("interaction-0001");
        var pin = Request(identity, "pin-build", CustodyKeyLifecycleAction.Pin, "hold-a");
        var reply = await call.DispatchAsync(new("pin", true, pin, Digest("pin-build"), null), TestContext.Current.CancellationToken);
        reply.Transport.ShouldBe(CustodyBindingTransportState.Refused);
        reply.Outcome.ShouldBeNull();
        client.ReceivedCalls().ShouldBeEmpty();
        var built = CustodyBindingDaprCall.Build(new("pin", true, pin, Digest("pin-build"), null));
        built.ShouldNotBeNull();
        built.Envelope.Operation.ShouldBe("pin");
        CustodyBindingContract.Encode(built);
        var reversed = Original("reservation-order", Key("interaction-0009"), identity);
        CustodyBindingDaprCall.Build(new("destroy-manifest", true, null, Digest("order"), reversed)).ShouldBeNull();
    }

    private static CustodyComponentStore Store(out CustodyOutsideWitness witness, out CustodyKeyObjectIdentity target, out CustodyKeyObjectIdentity other)
    {
        witness = new CustodyOutsideWitness();
        var store = new CustodyComponentStore(witness, Kek);
        target = Key("interaction-0001");
        other = Key("interaction-0009");
        store.TryEnroll(target, "kek-v1", Enumerable.Repeat((byte)1, 32).ToArray()).ShouldBeTrue();
        store.TryEnroll(other, "kek-v1", Enumerable.Repeat((byte)2, 32).ToArray()).ShouldBeTrue();
        return store;
    }

    private static bool Decrypts(CustodyComponentStore store, CustodyKeyObjectIdentity identity)
    {
        byte[] plain = new byte[32];
        bool opened = store.TryDecrypt(identity, "kek-v1", plain);
        byte expected = identity.ObjectId == "interaction-0001" ? (byte)1 : (byte)2;
        bool matches = opened && plain.All(b => b == expected);
        CryptographicOperations.ZeroMemory(plain);
        return matches;
    }

    private static CustodyKeyObjectIdentity Key(string objectId)
        => new("tenant-a", objectId, PlatformKeyPurpose.InteractionRootDek, "alias", "v1", "lifecycle", "store-1", "custody-lifecycle-v1");

    private static CustodyKeyRegistration Registration(CustodyKeyObjectIdentity identity)
        => new(identity, "wrap", 1, "kek-v1", "opaque-wrap", "wrap-receipt");

    private static CustodyKeyLifecycleRequest Request(CustodyKeyObjectIdentity identity, string operation, CustodyKeyLifecycleAction action, string? hold)
        => new(identity, operation, action, 1, hold, "fence-token", 2, "decision");

    private static CustodyOriginalManifest Original(string reservation, params CustodyKeyObjectIdentity[] targets)
    {
        var vector = targets.Select(i => new ProtectionTarget(i.TenantId, i.ObjectId, i.KeyAlias)).ToArray();
        return new(reservation, "batch-" + reservation, DeletionBatchCapabilityIdentity.TargetManifestDigest(vector), targets);
    }

    private static string Digest(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
