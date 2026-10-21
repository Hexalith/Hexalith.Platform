using System.Security.Cryptography;
using System.Text;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Custody.Component;
using Shouldly;

namespace Hexalith.Platform.Custody.Component.Tests;

public sealed class CustodyComponentMatrixTests
{
    private static readonly byte[] Kek = Enumerable.Repeat((byte)9, 32).ToArray();

    [Fact]
    public void PinAndUnpinMatchLookup_DuplicateOrMissingHoldConflicts_MissingWitnessWritesNothing()
    {
        using var store = Store(out var witness, out var key, out _);
        var pin = Request(key, "pin-1", CustodyKeyLifecycleAction.Pin, "hold-a");
        string digest = Digest("pin-1");
        var pinned = store.Pin(pin, digest);
        pinned.Status.ShouldBe(CustodyComponentStatus.Pinned);
        store.Lookup("pin-1", digest).ShouldBe(pinned);
        store.Pin(pin with { OperationId = "pin-2" }, Digest("pin-2")).Status.ShouldBe(CustodyComponentStatus.Conflict);
        store.Unpin(Request(key, "unpin-missing", CustodyKeyLifecycleAction.Unpin, "missing"), Digest("unpin-missing")).Status.ShouldBe(CustodyComponentStatus.Conflict);
        store.Lookup("pin-1", digest).Status.ShouldBe(CustodyComponentStatus.Pinned);

        int writes = store.Mutations;
        using var absent = new CustodyComponentStore(null, Kek);
        absent.Pin(pin, digest).Status.ShouldBe(CustodyComponentStatus.Unavailable);
        absent.Mutations.ShouldBe(0);
        store.Mutations.ShouldBe(writes);
        witness.HasPin(key, "hold-a").ShouldBeTrue();
    }

    [Fact]
    public void PinnedDestroyHasNoEffect_AndAnotherUnpinDoesNotReleaseIt()
    {
        using var store = Store(out _, out var target, out var other);
        var manifest = Original("reservation-1", target);
        store.Pin(Request(target, "pin", CustodyKeyLifecycleAction.Pin, "hold-a"), Digest("pin"));
        store.Pin(Request(target, "pin-b", CustodyKeyLifecycleAction.Pin, "hold-b"), Digest("pin-b"));
        var blocked = store.Consume("destroy-1", manifest, Digest("destroy-1"), target);
        blocked.Status.ShouldBe(CustodyComponentStatus.BlockedByPin);
        store.PhysicalCuts.ShouldBe(0);
        Decrypts(store, target).ShouldBeTrue();
        Decrypts(store, other).ShouldBeTrue();
        store.Unpin(Request(target, "unpin-b", CustodyKeyLifecycleAction.Unpin, "hold-b"), Digest("unpin-b")).Status.ShouldBe(CustodyComponentStatus.Unpinned);
        store.Consume("destroy-2", manifest, Digest("destroy-2"), target).Status.ShouldBe(CustodyComponentStatus.BlockedByPin);
        Decrypts(store, target).ShouldBeTrue();
        store.Unpin(Request(target, "unpin-a", CustodyKeyLifecycleAction.Unpin, "hold-a"), Digest("unpin-a")).Status.ShouldBe(CustodyComponentStatus.Unpinned);
        store.Consume("destroy-3", manifest, Digest("destroy-3"), target).Status.ShouldBe(CustodyComponentStatus.Destroyed);
        Decrypts(store, target).ShouldBeFalse();
        Decrypts(store, other).ShouldBeTrue();
    }

    [Fact]
    public void ManifestDestroysEveryTargetOrNone_RetryMatchesLookup_ChangedDigestConflicts_LossStaysUnknown()
    {
        using var store = Store(out _, out var target, out var other);
        var manifest = Original("reservation-2", target);
        var unsorted = Original("reservation-unsorted", other, target);
        store.Consume("unsorted", unsorted, Digest("unsorted")).Status.ShouldBe(CustodyComponentStatus.Conflict);
        store.PhysicalCuts.ShouldBe(0);
        Decrypts(store, target).ShouldBeTrue();

        string digest = Digest("consume");
        var first = store.Consume("consume", manifest, digest, target);
        first.Status.ShouldBe(CustodyComponentStatus.Destroyed);
        first.TargetReceipts.ShouldNotBeNull();
        first.TargetReceipts.Select(r => r.Target.AgentInteractionId).ShouldBe(["interaction-0001"]);
        store.PhysicalCuts.ShouldBe(1);
        store.Consume("consume", manifest, digest, target).ShouldBe(first with { });
        store.LookupManifest("reservation-2", digest).TargetReceipts.ShouldBe(first.TargetReceipts);
        store.PhysicalCuts.ShouldBe(1);
        Decrypts(store, target).ShouldBeFalse();
        Decrypts(store, other).ShouldBeTrue();

        var changed = Original("reservation-2", other);
        store.Consume("changed", changed, Digest("changed"), other).Status.ShouldBe(CustodyComponentStatus.Conflict);
        store.PhysicalCuts.ShouldBe(1);
        Decrypts(store, other).ShouldBeTrue();

        using var loss = Store(out _, out var lossTarget, out var lossOther);
        var lossManifest = Original("reservation-loss", lossTarget);
        loss.LoseNextEffect = true;
        loss.Consume("loss", lossManifest, Digest("loss"), lossTarget).Status.ShouldBe(CustodyComponentStatus.Unknown);
        loss.PhysicalCuts.ShouldBe(0);
        loss.LookupManifest("reservation-loss", Digest("loss")).Status.ShouldBe(CustodyComponentStatus.Unknown);
        loss.Lookup("loss", Digest("loss")).Status.ShouldBe(CustodyComponentStatus.Unknown);
        loss.PhysicalCuts.ShouldBe(0);
        loss.FindOriginal("reservation-loss").ShouldBeNull();
        Decrypts(loss, lossTarget).ShouldBeTrue();
        Decrypts(loss, lossOther).ShouldBeTrue();
    }

    [Fact]
    public void RestoredVolumeCannotReviveDestroyedKey_AndWitnessRejectsRepromotion()
    {
        using var store = Store(out var witness, out var target, out var other);
        byte[] snapshot = store.CaptureVolume();
        store.VolumeHoldsEpochBytes("g0").ShouldBeTrue();
        var manifest = Original("reservation-restore", target);
        store.Consume("restore", manifest, Digest("restore"), target).Status.ShouldBe(CustodyComponentStatus.Destroyed);
        store.VolumeHoldsEpochBytes("g0").ShouldBeFalse();
        store.EpochIsLive("g0").ShouldBeFalse();
        Decrypts(store, target).ShouldBeFalse();
        Decrypts(store, other).ShouldBeTrue();

        store.RestoreVolume(snapshot);
        store.VolumeHoldsEpochBytes("g0").ShouldBeTrue();
        store.TryRepromoteEpoch("g0").ShouldBeFalse();
        store.EpochIsLive("g0").ShouldBeFalse();
        Decrypts(store, target).ShouldBeFalse();
        Decrypts(store, other).ShouldBeTrue();
        witness.Available = false;
        store.LookupManifest("reservation-restore", Digest("restore")).Status.ShouldBe(CustodyComponentStatus.Unknown);
        store.LookupManifest("never", Digest("never")).Status.ShouldBe(CustodyComponentStatus.Unknown);
        byte[] cleared = Enumerable.Repeat((byte)7, 32).ToArray();
        store.TryDecrypt(target, "kek-v1", cleared).ShouldBeFalse();
        cleared.ShouldAllBe(b => b == 0);
    }

    [Fact]
    public void SecondManifestAfterCutDestroysTheRemainingKey_AndTheOtherTenantStillDecrypts()
    {
        using var store = Store(out var witness, out var target, out var other);
        var foreign = new CustodyKeyObjectIdentity("tenant-b", "interaction-0003", PlatformKeyPurpose.InteractionRootDek, "alias", "v1", "lifecycle", "store-1", "custody-lifecycle-v1");
        store.TryEnroll(foreign, "kek-v1", Enumerable.Repeat((byte)3, 32).ToArray()).ShouldBeTrue();
        store.FindEnrolled(new ProtectionTarget(foreign.TenantId, foreign.ObjectId, foreign.KeyAlias)).ShouldBe(foreign);

        var first = Original("reservation-second", target);
        store.Consume("cut-1", first, Digest("cut-1"), target).Status.ShouldBe(CustodyComponentStatus.Destroyed);
        store.FindOriginal("reservation-second").ShouldNotBeNull();
        store.RetainsPlainDek(target).ShouldBeFalse();
        Decrypts(store, other).ShouldBeTrue();
        Decrypts(store, foreign, 3).ShouldBeTrue();
        store.PhysicalCuts.ShouldBe(1);

        var second = Original("reservation-third", other);
        store.Consume("cut-2", second, Digest("cut-2"), other).Status.ShouldBe(CustodyComponentStatus.Destroyed);
        store.PhysicalCuts.ShouldBe(2);
        Decrypts(store, target).ShouldBeFalse();
        Decrypts(store, other).ShouldBeFalse();
        store.RetainsPlainDek(other).ShouldBeFalse();
        Decrypts(store, foreign, 3).ShouldBeTrue();
        witness.TryRepromote("g0").ShouldBeFalse();
    }

    [Fact]
    public void FindEnrolledReturnsNullForZeroOrSeveralMatches()
    {
        using var store = Store(out _, out var target, out _);
        store.FindEnrolled(new ProtectionTarget("missing", "missing", "missing")).ShouldBeNull();
        var version = target with { KeyVersion = "v2" };
        store.TryEnroll(version, "kek-v1", Enumerable.Repeat((byte)4, 32).ToArray()).ShouldBeTrue();
        store.FindEnrolled(new ProtectionTarget(target.TenantId, target.ObjectId, target.KeyAlias)).ShouldBeNull();
        Decrypts(store, target).ShouldBeTrue();
        Decrypts(store, version, 4).ShouldBeTrue();
    }

    [Fact]
    public void RestoredVolumeStillAllowsASecondCut_AndABlockedCutKeepsNoAdmission()
    {
        using var store = Store(out var witness, out var target, out var other);
        var foreign = new CustodyKeyObjectIdentity("tenant-b", "interaction-0004", PlatformKeyPurpose.InteractionRootDek, "alias", "v1", "lifecycle", "store-1", "custody-lifecycle-v1");
        store.TryEnroll(foreign, "kek-v1", Enumerable.Repeat((byte)5, 32).ToArray()).ShouldBeTrue();
        byte[] snapshot = store.CaptureVolume();
        store.Pin(Request(target, "pin", CustodyKeyLifecycleAction.Pin, "hold-a"), Digest("pin"));
        var blockedManifest = Original("reservation-blocked", target);
        store.Consume("blocked", blockedManifest, Digest("blocked"), target).Status.ShouldBe(CustodyComponentStatus.BlockedByPin);
        store.FindOriginal("reservation-blocked").ShouldBeNull();
        store.Consume("blocked", blockedManifest, Digest("blocked-other"), target).Status.ShouldBe(CustodyComponentStatus.Conflict);
        store.Unpin(Request(target, "unpin", CustodyKeyLifecycleAction.Unpin, "hold-a"), Digest("unpin")).Status.ShouldBe(CustodyComponentStatus.Unpinned);
        store.PhysicalCuts.ShouldBe(0);

        store.Consume("cut-1", Original("reservation-live", target), Digest("cut-1"), target).Status.ShouldBe(CustodyComponentStatus.Destroyed);
        store.RestoreVolume(snapshot);
        store.TryRepromoteEpoch("g0").ShouldBeFalse();
        Decrypts(store, target).ShouldBeFalse();
        Decrypts(store, other).ShouldBeTrue();
        Decrypts(store, foreign, 5).ShouldBeTrue();
        store.Consume("cut-2", Original("reservation-after-restore", other), Digest("cut-2"), other).Status.ShouldBe(CustodyComponentStatus.Destroyed);
        Decrypts(store, other).ShouldBeFalse();
        Decrypts(store, target).ShouldBeFalse();
        Decrypts(store, foreign, 5).ShouldBeTrue();
        witness.Available.ShouldBeTrue();

        int writes = store.Mutations;
        store.Dispose();
        store.Pin(Request(other, "pin-late", CustodyKeyLifecycleAction.Pin, "hold-z"), Digest("pin-late")).Status.ShouldBe(CustodyComponentStatus.Unavailable);
        store.Consume("cut-3", Original("reservation-disposed", foreign), Digest("cut-3"), foreign).Status.ShouldBe(CustodyComponentStatus.Unavailable);
        store.RestoreVolume(snapshot);
        store.Mutations.ShouldBe(writes);
        Decrypts(store, foreign, 5).ShouldBeFalse();
    }

    [Fact]
    public void HealthyAbsenceIsNotStarted_AndPartialMembershipDestroysNone()
    {
        using var store = Store(out _, out var target, out var other);
        store.LookupManifest("missing", Digest("missing")).Status.ShouldBe(CustodyComponentStatus.NotStarted);
        var foreign = new CustodyKeyObjectIdentity("other-tenant", "interaction-0001", PlatformKeyPurpose.InteractionRootDek, "alias", "v1", "lifecycle", "store-1", "custody-lifecycle-v1");
        var manifest = Original("reservation-foreign", target, foreign);
        store.Consume("foreign", manifest, Digest("foreign")).Status.ShouldBe(CustodyComponentStatus.Conflict);
        store.PhysicalCuts.ShouldBe(0);
        Decrypts(store, target).ShouldBeTrue();
        Decrypts(store, other).ShouldBeTrue();
    }

    private static CustodyComponentStore Store(out CustodyOutsideWitness witness, out CustodyKeyObjectIdentity target, out CustodyKeyObjectIdentity other)
    {
        witness = new CustodyOutsideWitness();
        var store = new CustodyComponentStore(witness, Kek);
        target = Key("interaction-0001");
        other = Key("interaction-0009");
        store.TryEnroll(target, "kek-v1", Enumerable.Repeat((byte)1, 32).ToArray()).ShouldBeTrue();
        store.TryEnroll(other, "kek-v1", Enumerable.Repeat((byte)2, 32).ToArray()).ShouldBeTrue();
        Decrypts(store, target).ShouldBeTrue();
        Decrypts(store, other).ShouldBeTrue();
        return store;
    }

    private static bool Decrypts(CustodyComponentStore store, CustodyKeyObjectIdentity identity, byte expected = 0)
    {
        byte[] plain = new byte[32];
        bool opened = store.TryDecrypt(identity, "kek-v1", plain);
        if (expected == 0) { expected = identity.ObjectId == "interaction-0001" ? (byte)1 : (byte)2; }
        bool matches = opened && plain.All(b => b == expected);
        CryptographicOperations.ZeroMemory(plain);
        return matches;
    }

    private static CustodyKeyObjectIdentity Key(string objectId)
        => new("tenant-a", objectId, PlatformKeyPurpose.InteractionRootDek, "alias", "v1", "lifecycle", "store-1", "custody-lifecycle-v1");

    private static CustodyKeyLifecycleRequest Request(CustodyKeyObjectIdentity identity, string operation, CustodyKeyLifecycleAction action, string? hold)
        => new(identity, operation, action, 1, hold, "fence-token", 2, "decision");

    private static CustodyOriginalManifest Original(string reservation, params CustodyKeyObjectIdentity[] targets)
    {
        var vector = targets.Select(i => new ProtectionTarget(i.TenantId, i.ObjectId, i.KeyAlias)).ToArray();
        return new(reservation, "batch-" + reservation, DeletionBatchCapabilityIdentity.TargetManifestDigest(vector), targets);
    }

    private static string Digest(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
