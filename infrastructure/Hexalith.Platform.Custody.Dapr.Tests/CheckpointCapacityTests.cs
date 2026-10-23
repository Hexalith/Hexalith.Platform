using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Custody.Dapr;
using Hexalith.Platform.Secrets.Runtime;

internal static class CheckpointCapacityTests
{
    internal static async Task Run()
    {
        using var fixture = new Fixture();
        var registrations = Enumerable.Range(0, 1000).Select(i => new CustodyKeyRegistration(
            new("tenant", i.ToString("D4") + new string('<', 2040), PlatformKeyPurpose.InteractionRootDek,
                new string('>', 2048), "v1", "lifecycle", "store", "contract"),
            "wrap-" + i, 0, "kek", "opaque-" + i, "receipt-" + i)).ToArray();
        var manifest = fixture.Manifest(registrations);
        if (!CustodyBindingContract.Validate(manifest) || CustodyBindingContract.Encode(manifest).Length < 23 * 1024 * 1024)
            throw new Exception("Maximum legal manifest fixture lost the original public bounds.");
        var checkpoint = fixture.Store.State.Checkpoint with {
            Cycle = new("destroy", manifest, "e1", null, CyclePhase.Reserved, ["e2"], [], null, null),
            Originals = new() { ["destroy"] = new(manifest.Envelope.RequestDigest, "destroy-manifest", null, manifest) },
            Registrations = registrations.ToDictionary(r => r.OpaqueWrappedKeyReference, r => new StoredRegistration(r,
                new(r.Identity, r.KekVersion, r.OpaqueWrappedKeyReference, "generation", new byte[12], [1], new byte[16]), [1], "e1")),
        };
        if (CustodyJson.Bytes(checkpoint).Length <= 32 * 1024 * 1024)
            throw new Exception("Manifest fixture does not reproduce the duplicate-payload capacity defect.");
        int before = fixture.Store.Objects.Count;
        byte[] index = await CheckpointArchive.EncodeAsync(checkpoint, fixture.Store.PutImmutableAsync, default);
        if (index.Length >= 32 * 1024 * 1024 - fixture.Profile.CompletionReserveBytes || fixture.Store.Objects.Count != before + 1001)
            throw new Exception("Complete manifest was duplicated or did not fit the unchanged current-index cap.");
        var restored = await CheckpointArchive.DecodeAsync(index, fixture.Store.ReadImmutableAsync, default);
        if (restored.Cycle!.Manifest.Manifest!.Original.Targets.Count != 1000
            || !CustodyJson.Bytes(restored.Cycle.Manifest).AsSpan().SequenceEqual(CustodyJson.Bytes(manifest)))
            throw new Exception("Complete original manifest changed across archive recovery.");
        byte[] target = new byte[RecoverableAnchoredState.MaximumPendingBytes];
        target[0] = 1; target[^1] = 2;
        var transition = new AnchoredStateTransition(fixture.OwnerScope, 0, 1, Fixture.Digest, CustodyJson.Hash(target), target);
        var transitionRequest = fixture.Request("admit-transition", transition: transition);
        if (!CustodyBindingContract.Validate(transitionRequest)) throw new Exception("Maximum legal transition rejected.");
        checkpoint = fixture.Store.State.Checkpoint with { Anchors = new() { [fixture.OwnerScope] = new(0, Fixture.Digest, transition, "admit") } };
        index = await CheckpointArchive.EncodeAsync(checkpoint, fixture.Store.PutImmutableAsync, default);
        if (index.Length >= 32 * 1024 * 1024 - fixture.Profile.CompletionReserveBytes)
            throw new Exception("Maximum transition cannot fit the unchanged current-index cap.");
        restored = await CheckpointArchive.DecodeAsync(index, fixture.Store.ReadImmutableAsync, default);
        if (!restored.Anchors[fixture.OwnerScope].Admitted!.TargetBytes.AsSpan().SequenceEqual(target))
            throw new Exception("Exact admitted transition bytes changed across archive recovery.");
        var archived = CustodyJson.Read<CheckpointArchiveV1>(index);
        var original = archived.Payloads.Single().Object;
        fixture.Store.Objects[original.Key][0] ^= 1;
        try { await CheckpointArchive.DecodeAsync(index, fixture.Store.ReadImmutableAsync, default); }
        catch (AuthorityUnavailableException) { return; }
        throw new Exception("Changed immutable payload revived a checkpoint.");
    }
}
