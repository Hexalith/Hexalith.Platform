using System.Text.Json;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Measured cumulative authoritative source/binding/lease traffic across the former head limit, followed by real engine protection and erasure.</summary>
public sealed class ErasureRootLoop5GrowthTests
{
    [Fact]
    public async Task AuthoritativeOccurrenceTrafficCrossesFormerHeadCapacityAndKeepsExactOldNewLeasesProtectionAndErasure()
    {
        var token = TestContext.Current.CancellationToken; using var fixture = new ErasureRootEngineFixture(); using var independent = new ErasureRootIndependentSourceFixture();
        var source = new ErasureRootNativeOccurrenceSource(fixture.Custody, fixture.Permissions, independent.Native, fixture.Digests,
            new Dictionary<string, ProtectionQualificationNativeCell> { ["tenant-a"] = new("independent", "root", new()) });
        var bindings = new ErasureRootOccurrenceBindings(fixture.Custody, fixture.Permissions, source, fixture.Digests);
        var seed = fixture.Enroll("seed");
        var originals = new List<ErasureRootOccurrenceEnrollment>();
        var legacySources = new SortedDictionary<string, ErasureRootOccurrenceEnrollment>(StringComparer.Ordinal);
        var legacyOccurrences = new SortedDictionary<string, ErasureRootOccurrence>(StringComparer.Ordinal);
        string Ref(int number)
        {
            char[] value = new string('0', 26).ToCharArray(); const string alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
            for (int index = 25; number > 0; index--) { value[index] = alphabet[number & 31]; number >>= 5; } return new string(value);
        }
        for (int index = 1; index <= 2048; index++)
        {
            // Actual independently enrolled metadata remains bounded per operation; 2,048 real owners
            // retain about 8 KiB of exact location metadata each, exceeding the former aggregate head cap.
            var identity = seed.Binding.Identity with { Owner = new AggregateIdentity("tenant-a", "conversations", "owner-" + index), Sequence = (ulong)index };
            var original = seed with { Binding = seed.Binding with { Identity = identity, KeyReference = Ref(index), OperationId = Ref(index) },
                Writer = new("source", "writer-" + index, new() { ["partitionKey"] = "tenant-a", ["sourceBinding"] = new string('p', 8192) }) };
            (await source.RegisterOriginalAsync(original, token)).ShouldBeTrue();
            (await bindings.ResolveAsync(identity.Owner, ErasureRootOccurrenceBindings.Occurrence(identity), identity.Target, true, token)).ShouldBe(original.Binding);
            var request = new InteractionOccurrenceRequest(identity, original.Binding.DigestKeyVersion, new string('A', 64), 1, original.Binding.KeyReference);
            (await source.VerifyReservationAsync(request, token)).ShouldBeTrue();
            var record = new InteractionOccurrenceRecord(request, original.Binding.KeyReference, 1, InteractionOccurrenceWriterState.Reserved, null, null);
            (await bindings.AcquireEncryptionLeaseAsync(original.Binding, record, token)).ShouldBeTrue();
            (await bindings.AcquireCompletionLeaseAsync(original.Binding, token)).ShouldBeTrue();
            string id = ErasureRootOccurrenceBindings.Id(identity.Owner, ErasureRootOccurrenceBindings.Occurrence(identity));
            legacySources.Add(id, original); legacyOccurrences.Add(id, new(original, (await bindings.FindAsync(identity.Owner, ErasureRootOccurrenceBindings.Occurrence(identity), token))!.Registration,
                ErasureRootCustody.Hash(request), true)); originals.Add(original);
        }
        var current = (await fixture.Custody.ReadAsync("tenant-a", token))!.Value;
        current.Head.SourceOriginals.ShouldBeEmpty(); current.Head.Occurrences.ShouldBeEmpty(); current.Head.SourceReservations.ShouldBeEmpty(); current.Head.SealedOriginals.ShouldBeEmpty();
        int headBytes = (await fixture.Store.ReadHeadAsync("tenant-a", token))!.Bytes.Length;
        int oldBytes = JsonSerializer.SerializeToUtf8Bytes(current.Head with { SourceOriginals = legacySources, Occurrences = legacyOccurrences }).Length;
        oldBytes.ShouldBeGreaterThan(OpenBaoErasureRootStore.MaximumHeadBytes); headBytes.ShouldBeLessThan(16384);
        foreach (int selected in new[] { 0, 1023, 2047 })
        {
            var original = originals[selected]; int before = fixture.Store.ArchiveReads;
            var retained = await bindings.FindAsync(original.Binding.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(original.Binding.Identity), token);
            retained.ShouldNotBeNull(); ErasureRootCustody.Hash(retained.Enrollment).ShouldBe(ErasureRootCustody.Hash(original)); retained.CompletionLease.ShouldBeTrue(); retained.EncryptionRequestDigest.ShouldNotBeNull();
            (fixture.Store.ArchiveReads - before).ShouldBeLessThan(20);
        }
        var first = originals[0]; var indexed = (await fixture.Custody.ReadAsync("tenant-a", token))!.Value;
        ErasureRootCustody.Hash(await fixture.Custody.ReadSourceReferenceAsync(indexed.Head, first.Binding.KeyReference, token)).ShouldBe(ErasureRootCustody.Hash(first));
        var stale = await fixture.Store.ReadHeadAsync("tenant-a", token);
        var left = (await fixture.Custody.ReadAsync("tenant-a", token))!.Value; var right = (await fixture.Custody.ReadAsync("tenant-a", token))!.Value;
        var leftValue = (await fixture.Custody.ReadOccurrenceAsync(left.Head, ErasureRootOccurrenceBindings.Id(first.Binding.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(first.Binding.Identity)), token))!;
        var rightValue = (await fixture.Custody.ReadOccurrenceAsync(right.Head, ErasureRootOccurrenceBindings.Id(first.Binding.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(first.Binding.Identity)), token))!;
        string firstId = ErasureRootOccurrenceBindings.Id(first.Binding.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(first.Binding.Identity));
        left.Head.Occurrences[firstId] = leftValue with { ReadOrdinal = 1 }; right.Head.Occurrences[firstId] = rightValue with { ReadOrdinal = 2 };
        (await fixture.Custody.SaveAsync("tenant-a", left.Version, left.Head, token)).ShouldBeTrue();
        (await fixture.Custody.SaveAsync("tenant-a", right.Version, right.Head, token)).ShouldBeFalse();
        (await fixture.Store.CompareExchangeHeadAsync("tenant-a", stale!.Version, stale.Bytes, token)).ShouldBeFalse();
        var final = fixture.Enroll("after-growth", "01ARZ3NDEKTSV4RRFFQ69G5FAZ", 4096);
        byte[] plain = JsonSerializer.SerializeToUtf8Bytes(new { ProtectedContent = new { Target = final.Binding.Identity.Target, Value = "after-growth-canary" } });
        var write = await fixture.Engine.ProtectEventPayloadAsync(final.Binding.Identity.Owner, new ProtectionQualificationEventPayload(), final.Binding.Identity.PayloadTypeId,
            plain, "json", ErasureRootOccurrenceBindings.Occurrence(final.Binding.Identity), token);
        await fixture.Engine.AcquirePayloadProtectionCompletionLeaseAsync(write.CompletionContext!, token); fixture.Source.Persisted[final.Binding.OperationId] = write.ProtectionResult.PayloadBytes;
        await fixture.Engine.CompletePayloadProtectionAsync(write.CompletionContext!, PayloadProtectionPersistenceOutcome.Persisted, token); await fixture.Keys.CompleteDisposedUsesAsync(token);
        var target = final.Binding.Identity.Target;
        var manifest = new DeletionBatchConsumptionRequest(new("issuer", "audience", "tenant-a", "request", "seal", "AcceptedSet", 0, "growth-batch", DeletionBatchCapabilityIdentity.TargetManifestDigest([target]), "guard", 1, 1, 1, "key"), "component", 1, "dispatch", 2, [target]);
        (await fixture.Custody.ConsumeAsync(manifest, "growth-reservation", token)).State.ShouldBe(DeletionManifestProviderState.Consumed);
        var replay = await fixture.Engine.TryReplayEventPayloadAsync(final.Binding.Identity.Owner, final.Binding.Identity.PayloadTypeId, write.ProtectionResult.PayloadBytes, write.ProtectionResult.SerializationFormat,
            write.ProtectionResult.Metadata, ErasureRootOccurrenceBindings.Occurrence(final.Binding.Identity), token);
        replay.HasErasedContent.ShouldBeTrue(); fixture.Store.TrimCount.ShouldBe(1);
        Console.WriteLine(JsonSerializer.Serialize(new { occurrences = 2048, owners = 2048, oldHeadBytes = oldBytes, headBytes, archiveBytes = fixture.Store.ArchiveCreatedBytes,
            archiveWrites = fixture.Store.ArchiveWrites, archiveReads = fixture.Store.ArchiveReads, perLookupBound = 20, typedErased = replay.HasErasedContent }));
    }
    [Fact]
    public async Task OneUpdateReusesVerifiedImmutableNodesButFreshReadersStillValidateTheCommittedTree()
    {
        var token = TestContext.Current.CancellationToken; using var cached = new ErasureRootEngineFixture(); using var uncached = new ErasureRootEngineFixture();
        var index = new ErasureRootOccurrenceIndex(cached.Store, cached.Custody.Installation, "tenant-a"); string? sharedRoot = null, separateRoot = null;
        for (int item = 0; item < 256; item++)
        {
            var value = new ErasureRootOccurrenceReference("value-" + item, false);
            sharedRoot = await index.WriteAsync(sharedRoot, "cache:" + item, value, token);
            separateRoot = await new ErasureRootOccurrenceIndex(uncached.Store, uncached.Custody.Installation, "tenant-a").WriteAsync(separateRoot, "cache:" + item, value, token);
        }
        sharedRoot.ShouldBe(separateRoot); cached.Store.ArchiveCreatedBytes.ShouldBe(uncached.Store.ArchiveCreatedBytes); cached.Store.ArchiveWrites.ShouldBe(uncached.Store.ArchiveWrites);
        cached.Store.ArchiveReads.ShouldBeLessThan(uncached.Store.ArchiveReads / 4);
        var uncommitted = (await cached.Custody.ReadAsync("tenant-a", token))!.Value;
        (await cached.Custody.ReadSourceAsync(uncommitted.Head, "cache:0", token)).ShouldBeNull();
        (await cached.Custody.SaveAsync("tenant-a", uncommitted.Version, uncommitted.Head with { OccurrenceIndexRoot = sharedRoot }, token)).ShouldBeTrue();
        var committed = (await cached.Custody.ReadAsync("tenant-a", token))!.Value; committed.Head.OccurrenceIndexRoot.ShouldBe(sharedRoot);
        int reads = cached.Store.ArchiveReads;
        foreach (int item in new[] { 0, 127, 255 })
        {
            var fresh = new ErasureRootOccurrenceIndex(cached.Store, cached.Custody.Installation, "tenant-a");
            (await fresh.ReadAsync<ErasureRootOccurrenceReference>(committed.Head.OccurrenceIndexRoot, "cache:" + item, token))!.OccurrenceId.ShouldBe("value-" + item);
        }
        cached.Store.ArchiveReads.ShouldBeGreaterThan(reads);
        var head = (await cached.Custody.ReadAsync("tenant-a", token))!.Value;
        (await cached.Custody.ReadSourceAsync(head.Head, "cache:0", token)).ShouldBeNull();
        await Should.ThrowAsync<InvalidOperationException>(() => new ErasureRootOccurrenceIndex(cached.Store, cached.Custody.Installation, "tenant-b").ReadAsync<ErasureRootOccurrenceReference>(sharedRoot, "cache:0", token));
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => index.ReadAsync<ErasureRootOccurrenceReference>(sharedRoot, "cache:0", canceled.Token));
        Console.WriteLine(JsonSerializer.Serialize(new { immutableBatchEntries = 256, cachedArchiveReads = reads, freshInstanceArchiveReads = uncached.Store.ArchiveReads,
            identicalArchiveBytes = cached.Store.ArchiveCreatedBytes, identicalCommittedTree = sharedRoot == separateRoot }));
    }

}
