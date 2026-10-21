using System.Security.Cryptography;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Custody.Component;
using Hexalith.Platform.Protection;
using Hexalith.Platform.Secrets.Runtime;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

public sealed class AtomicDeletionManifestBindingTests
{
    private static readonly byte[] Kek = Enumerable.Repeat((byte)9, 32).ToArray();

    [Fact]
    public async Task ConsumeAndLookupUseTheOriginalManifest_RetryMatches_ChangedDigestDoesNotStartAnotherCut()
    {
        using var store = Store(out var witness, out var target, out var other);
        var binding = new CustodyComponentBindingCall(store);
        var provider = new AtomicDeletionManifestBindingProvider(binding, witness, store);
        var request = Request("reservation-1", target);
        (await provider.LookupAsync(request, "reservation-1", TestContext.Current.CancellationToken)).State.ShouldBe(DeletionManifestProviderState.NotStarted);
        store.PhysicalCuts.ShouldBe(0);
        binding.Operations.ShouldBe(["lookup-original"]);

        var consumed = await provider.ConsumeAsync(request, "reservation-1", TestContext.Current.CancellationToken);
        consumed.State.ShouldBe(DeletionManifestProviderState.Consumed);
        consumed.TargetReceipts.Select(r => r.Target.AgentInteractionId).ShouldBe(["interaction-0001"]);
        var again = await provider.ConsumeAsync(request, "reservation-1", TestContext.Current.CancellationToken);
        again.TargetReceipts.Select(r => r.ReceiptId).ShouldBe(consumed.TargetReceipts.Select(r => r.ReceiptId));
        (await provider.LookupAsync(request, "reservation-1", TestContext.Current.CancellationToken)).TargetReceipts.Select(r => r.ReceiptId)
            .ShouldBe(consumed.TargetReceipts.Select(r => r.ReceiptId));
        store.PhysicalCuts.ShouldBe(1);
        binding.Operations.ShouldBe(["lookup-original", "destroy-manifest", "destroy-manifest", "lookup-original"]);
        Decrypts(store, target).ShouldBeFalse();
        Decrypts(store, other).ShouldBeTrue();

        var changed = Request("reservation-1", other);
        (await provider.ConsumeAsync(changed, "reservation-1", TestContext.Current.CancellationToken)).State.ShouldBe(DeletionManifestProviderState.Unknown);
        store.LookupManifest("reservation-1", AtomicDeletionManifestBindingProvider.RequestDigest(changed)).Status.ShouldBe(CustodyComponentStatus.Conflict);
        store.PhysicalCuts.ShouldBe(1);
        Decrypts(store, other).ShouldBeTrue();
    }

    [Fact]
    public async Task PinnedManifestDoesNotDestroy_LossAndOutageStayUnknown_AbsentBindingWritesNothing()
    {
        using var store = Store(out var witness, out var target, out var other);
        store.Pin(new(target, "pin", CustodyKeyLifecycleAction.Pin, 1, "hold-a", "fence-token", 2, "decision"), Digest("pin"));
        var binding = new CustodyComponentBindingCall(store);
        var provider = new AtomicDeletionManifestBindingProvider(binding, witness, store);
        var request = Request("reservation-pin", target, other);
        (await provider.ConsumeAsync(request, "reservation-pin", TestContext.Current.CancellationToken)).State.ShouldBe(DeletionManifestProviderState.Unknown);
        store.LookupManifest("reservation-pin", AtomicDeletionManifestBindingProvider.RequestDigest(request)).Status.ShouldBe(CustodyComponentStatus.BlockedByPin);
        store.PhysicalCuts.ShouldBe(0);
        Decrypts(store, target).ShouldBeTrue();
        Decrypts(store, other).ShouldBeTrue();
        store.Unpin(new(target, "unpin-other", CustodyKeyLifecycleAction.Unpin, 1, "other-hold", "fence-token", 2, "decision"), Digest("other")).Status.ShouldBe(CustodyComponentStatus.Conflict);
        witness.HasPin(target, "hold-a").ShouldBeTrue();

        using var lossStore = Store(out var lossWitness, out var lossTarget, out var lossOther);
        lossStore.LoseNextEffect = true;
        var lossBinding = new CustodyComponentBindingCall(lossStore);
        var lossProvider = new AtomicDeletionManifestBindingProvider(lossBinding, lossWitness, lossStore);
        var lossRequest = Request("reservation-loss", lossTarget);
        (await lossProvider.ConsumeAsync(lossRequest, "reservation-loss", TestContext.Current.CancellationToken)).State.ShouldBe(DeletionManifestProviderState.Unknown);
        (await lossProvider.LookupAsync(lossRequest, "reservation-loss", TestContext.Current.CancellationToken)).State.ShouldBe(DeletionManifestProviderState.Unknown);
        lossStore.PhysicalCuts.ShouldBe(0);
        lossBinding.Operations.ShouldBe(["destroy-manifest", "lookup-original"]);
        Decrypts(lossStore, lossTarget).ShouldBeTrue();

        lossWitness.Available = false;
        (await lossProvider.LookupAsync(lossRequest, "never", TestContext.Current.CancellationToken)).State.ShouldBe(DeletionManifestProviderState.Unknown);
        lossBinding.Operations.Count.ShouldBe(2);

        int writes = store.Mutations;
        var absent = new AtomicDeletionManifestBindingProvider(new CustodyComponentBindingCall(store, present: false), witness, store);
        (await absent.ConsumeAsync(request, "reservation-pin", TestContext.Current.CancellationToken)).State.ShouldBe(DeletionManifestProviderState.Unknown);
        store.Mutations.ShouldBe(writes);
        store.PhysicalCuts.ShouldBe(0);
    }

    [Fact]
    public async Task RestoredPreDestructionVolumeCannotDecryptTheDestroyedKey()
    {
        using var store = Store(out var witness, out var target, out var other);
        byte[] snapshot = store.CaptureVolume();
        var provider = new AtomicDeletionManifestBindingProvider(new CustodyComponentBindingCall(store), witness, store);
        var request = Request("reservation-restore", target);
        (await provider.ConsumeAsync(request, "reservation-restore", TestContext.Current.CancellationToken)).State.ShouldBe(DeletionManifestProviderState.Consumed);
        store.RestoreVolume(snapshot);
        store.VolumeHoldsEpochBytes("g0").ShouldBeTrue();
        store.TryRepromoteEpoch("g0").ShouldBeFalse();
        Decrypts(store, target).ShouldBeFalse();
        Decrypts(store, other).ShouldBeTrue();
        witness.Available = false;
        (await provider.LookupAsync(request, "reservation-restore", TestContext.Current.CancellationToken)).State.ShouldBe(DeletionManifestProviderState.Unknown);
    }

    [Fact]
    public async Task FalseDestructionUnresolvedTargetAndNullWitnessStayUnknown_ReleasedPinRetriesTheSameBatch()
    {
        using var store = Store(out var witness, out var target, out var other);
        var request = Request("reservation-false", target);
        var claim = new DestroyedClaimBinding(new CustodyComponentOutcome(CustodyComponentStatus.Destroyed, "batch-reservation-false", Digest("false"), "fake-receipt", "fake-barrier", Identity: target));
        var falseProvider = new AtomicDeletionManifestBindingProvider(claim, witness, store);
        (await falseProvider.ConsumeAsync(request, "reservation-false", TestContext.Current.CancellationToken)).State.ShouldBe(DeletionManifestProviderState.Unknown);
        store.PhysicalCuts.ShouldBe(0);
        Decrypts(store, target).ShouldBeTrue();

        var unresolved = new CustodyComponentBindingCall(store);
        var unresolvedProvider = new AtomicDeletionManifestBindingProvider(unresolved, witness, store);
        (await unresolvedProvider.ConsumeAsync(Request("reservation-missing", Key("interaction-missing")), "reservation-missing", TestContext.Current.CancellationToken)).State.ShouldBe(DeletionManifestProviderState.Unknown);
        unresolved.Operations.ShouldBeEmpty();
        store.PhysicalCuts.ShouldBe(0);

        var silent = new CustodyComponentBindingCall(store);
        var nullWitness = new AtomicDeletionManifestBindingProvider(silent, null, store);
        (await nullWitness.ConsumeAsync(request, "reservation-false", TestContext.Current.CancellationToken)).State.ShouldBe(DeletionManifestProviderState.Unknown);
        silent.Operations.ShouldBeEmpty();
        store.PhysicalCuts.ShouldBe(0);

        store.Pin(new(target, "pin", CustodyKeyLifecycleAction.Pin, 1, "hold-a", "fence-token", 2, "decision"), Digest("pin"));
        var binding = new CustodyComponentBindingCall(store);
        var provider = new AtomicDeletionManifestBindingProvider(binding, witness, store);
        var blocked = Request("reservation-release", target);
        (await provider.ConsumeAsync(blocked, "reservation-release", TestContext.Current.CancellationToken)).State.ShouldBe(DeletionManifestProviderState.Unknown);
        store.PhysicalCuts.ShouldBe(0);
        store.Unpin(new(target, "unpin", CustodyKeyLifecycleAction.Unpin, 1, "hold-a", "fence-token", 2, "decision"), Digest("unpin")).Status.ShouldBe(CustodyComponentStatus.Unpinned);
        var destroyed = await provider.ConsumeAsync(blocked, "reservation-release", TestContext.Current.CancellationToken);
        destroyed.State.ShouldBe(DeletionManifestProviderState.Consumed);
        Decrypts(store, target).ShouldBeFalse();
        Decrypts(store, other).ShouldBeTrue();
        store.PhysicalCuts.ShouldBe(1);
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

    private static DeletionBatchConsumptionRequest Request(string reservation, params CustodyKeyObjectIdentity[] identities)
    {
        var targets = identities.Select(i => new ProtectionTarget(i.TenantId, i.ObjectId, i.KeyAlias)).ToArray();
        string digest = DeletionBatchCapabilityIdentity.TargetManifestDigest(targets);
        var capability = new DeletionBatchCapabilityV1("issuer", "audience", targets[0].TenantId, "delete-request", "seal", "manifest", 1,
            "batch-" + reservation, digest, "guard", 1, 1, 1, "capability-v1");
        return new(capability, "detached-jws", 2, "dispatch-receipt", 3, targets);
    }

    private static string Digest(string text) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));

    private sealed class DestroyedClaimBinding(CustodyComponentOutcome claim) : ICustodyLifecycleBinding
    {
        public bool Present => true;

        public Task<CustodyLifecycleBindingReply> DispatchAsync(CustodyLifecycleBindingCommand command, CancellationToken cancellationToken)
            => Task.FromResult(new CustodyLifecycleBindingReply(CustodyBindingTransportState.ReceivedUnverified, claim, false));
    }
}
