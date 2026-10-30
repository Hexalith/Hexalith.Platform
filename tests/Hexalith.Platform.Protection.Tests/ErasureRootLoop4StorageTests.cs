using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Measured immutable growth, exact originals and independent lifetime-boundary regressions.</summary>
public sealed class ErasureRootLoop4StorageTests
{
    [Theory]
    [InlineData(1024)]
    [InlineData(60 * 1024)]
    [InlineData(96 * 1024)]
    [InlineData(192 * 1024)]
    public async Task ArchiveRawSingleAndMultipleChunkBoundariesKeepExactImmutableOriginals(int size)
    {
        var store = new ErasureRootStoreFixture(); string original = new('x', size);
        string id = await ErasureRootArchives.WriteAsync(store, "tenant-a", original, TestContext.Current.CancellationToken);
        (await ErasureRootArchives.ReadBytesAsync(store, "tenant-a", id, TestContext.Current.CancellationToken))!.ShouldBe(JsonSerializer.SerializeToUtf8Bytes(original));
        (await ErasureRootArchives.WriteAsync(store, "tenant-a", original, TestContext.Current.CancellationToken)).ShouldBe(id);
    }
    [Fact]
    public async Task TransitionFrameReconstructsExactOriginalPreservesLegacyAndRefusesRecursivePayload()
    {
        var token = TestContext.Current.CancellationToken; var store = new ErasureRootStoreFixture();
        byte[] target = JsonSerializer.SerializeToUtf8Bytes(new { Prefix = "p", Records = new string('x', 5 * 1024 * 1024) });
        var original = new AnchoredStateTransition("tenant-a:source:source|state", 0, 1, "genesis", Convert.ToHexString(SHA256.HashData(target)), target);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(original, ErasureRootCustody.Json); string id = ErasureRootCustody.Hash(original);
        (await store.CreateArchiveAsync("tenant-a", id, bytes, token)).ShouldBeTrue();
        int writes = store.ArchiveWrites;
        (await ErasureRootArchives.WriteAsync(store, "tenant-a", original, token)).ShouldBe(id); store.ArchiveWrites.ShouldBe(writes);
        (await ErasureRootArchives.ReadBytesAsync(store, "tenant-a", id, token))!.ShouldBe(bytes);
        string[] chunks = [];
        for (int offset = 0; offset < bytes.Length; offset += ErasureRootArchives.ChunkBytes)
        {
            byte[] chunk = bytes.AsSpan(offset, Math.Min(ErasureRootArchives.ChunkBytes, bytes.Length - offset)).ToArray();
            string chunkId = Convert.ToHexString(SHA256.HashData(chunk));
            (await store.CreateArchiveAsync("tenant-b", chunkId, chunk, token)).ShouldBeTrue(); chunks = [.. chunks, chunkId];
        }
        (await store.CreateArchiveAsync("tenant-b", id, JsonSerializer.SerializeToUtf8Bytes(new ErasureRootChunkedArchive("erc-chunks-v1", bytes.Length, chunks)), token)).ShouldBeTrue();
        (await ErasureRootArchives.WriteAsync(store, "tenant-b", original, token)).ShouldBe(id);
        (await ErasureRootArchives.ReadBytesAsync(store, "tenant-b", id, token))!.ShouldBe(bytes);
        (await ErasureRootArchives.WriteAsync(store, "tenant-c", original, token)).ShouldBe(id);
        var frame = JsonSerializer.Deserialize<ErasureRootTransitionPayloadArchive>((await store.ReadArchiveAsync("tenant-c", id, token))!)!;
        frame.TargetArchive.ShouldBe(original.TargetDigest);
        (await ErasureRootArchives.ReadBytesAsync(store, "tenant-c", id, token))!.ShouldBe(bytes);
        byte[] recursive = JsonSerializer.SerializeToUtf8Bytes(frame);
        (await store.CreateArchiveAsync("tenant-d", id, recursive, token)).ShouldBeTrue();
        (await store.CreateArchiveAsync("tenant-d", frame.TargetArchive, recursive, token)).ShouldBeTrue();
        await Assert.ThrowsAsync<InvalidOperationException>(() => ErasureRootArchives.ReadBytesAsync(store, "tenant-d", id, token));
    }
    [Theory]
    [InlineData(128 * 1024)]
    [InlineData(5 * 1024 * 1024)]
    public async Task CumulativeTransitionsSharePayloadAndBoundMetadataWithExactIndexedHistoricalLookup(int initialBytes)
    {
        var token = TestContext.Current.CancellationToken; var store = new ErasureRootStoreFixture();
        var installation = new ErasureRootInstallation("erc", "enrollment", "transit", "heads", new Dictionary<string, string> { ["tenant-a"] = "epoch" });
        string genesis = Convert.ToHexString(SHA256.HashData("genesis"u8)); const string scope = "tenant-a:source:source|state";
        store.Enroll(installation, "tenant-a", scope, genesis); var auth = new ErasureRootAuthorizationFixture(); var custody = new ErasureRootCustody(store, installation, auth);
        var authority = new ErasureRootTransitionAuthority(custody, auth, "tenant-a", scope);
        var originals = new List<AnchoredStateTransition>(); string accumulated = Convert.ToHexString(RandomNumberGenerator.GetBytes(initialBytes / 2)); string digest = genesis;
        long cumulative = 0;
        for (int index = 0; index < 20; index++)
        {
            if (index > 0)
            {
                int insertion = index % 2 == 0 ? accumulated.Length / 2 : 0;
                accumulated = accumulated.Insert(insertion, Convert.ToHexString(RandomNumberGenerator.GetBytes(16 * 1024)));
            }
            // Each one-byte prefix change shifts the complete transition base64, while raw JSON retains its unchanged ranges.
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new { Prefix = new string('p', index), Records = accumulated }); cumulative += bytes.Length;
            var transition = new AnchoredStateTransition(scope, index, index + 1, digest, Convert.ToHexString(SHA256.HashData(bytes)), bytes);
            (await authority.AdmitTransitionAsync(transition, token)).ShouldBeTrue();
            if (index == 9)
            {
                store.Failure = "cas-before"; (await authority.RecordTransitionAsync(transition, token)).ShouldBeFalse();
                (await authority.VerifyTransitionAsync(transition, token)).ShouldBeFalse();
                (await authority.ReadAdmittedOriginalAsync(scope, token))!.TargetBytes.ShouldBe(bytes);
            }
            (await authority.RecoverTransitionAsync(transition, token)).ShouldBeTrue(); originals.Add(transition); digest = transition.TargetDigest;
        }
        // Includes admitted payloads, chunk frames, ancestry links and failed prewrites. A cumulative full-snapshot archive fails this bound.
        long supported = (long)(initialBytes + 19 * 32 * 1024) * 2 + 20L * (384 * 1024);
        store.ArchiveCreatedBytes.ShouldBeLessThan(supported);
        if (initialBytes > 4 * 1024 * 1024) { store.ArchiveCreatedBytes.ShouldBeLessThan(cumulative / 4); }
        foreach (int index in new[] { 0, 9, 19 })
        {
            int calls = store.ArchiveReads; (await authority.VerifyTransitionAsync(originals[index], token)).ShouldBeTrue();
            (store.ArchiveReads - calls).ShouldBeLessThan(32 + 2 * (JsonSerializer.SerializeToUtf8Bytes(originals[^1]).Length / (16 * 1024) + 2));
        }
        (await authority.ReadCurrentOriginalAsync(scope, token))!.TargetBytes.ShouldBe(originals[^1].TargetBytes);
        var orphan = originals[^1] with { TargetBytes = "orphan"u8.ToArray(), TargetDigest = Convert.ToHexString(SHA256.HashData("orphan"u8)) };
        _ = await ErasureRootArchives.WriteAsync(store, "tenant-a", orphan, token);
        (await authority.VerifyTransitionAsync(orphan, token)).ShouldBeFalse();
        Console.WriteLine(JsonSerializer.Serialize(new { geometry = initialBytes, transitions = 20, cumulative, stored = store.ArchiveCreatedBytes, writes = store.ArchiveWrites, supported }));
    }
    [Fact]
    public async Task IndependentRetentionCrossesLegacyTenThousandWithoutGrowingItsMapAndRemainsReadableAfterErcLoss()
    {
        var token = TestContext.Current.CancellationToken; using var engine = new ErasureRootEngineFixture(); using var durable = new ErasureRootIndependentSourceFixture();
        var original = engine.Enroll() with { Writer = new("proof", "writer", new()) };
        for (ulong index = 1; index <= 10000; index++)
        {
            var enrollment = original with { Binding = original.Binding with { Identity = original.Binding.Identity with { Sequence = index } } };
            durable.Root.Originals.Add(ErasureRootOccurrenceBindings.Id(enrollment.Binding.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(enrollment.Binding.Identity)), enrollment);
        }
        var source = new ErasureRootNativeOccurrenceSource(engine.Custody, engine.Permissions, durable.Native, engine.Digests,
            new Dictionary<string, ProtectionQualificationNativeCell> { ["tenant-a"] = new("independent", "root", new()) });
        var added = new List<ErasureRootOccurrenceEnrollment>();
        for (ulong index = 10001; index <= 10020; index++)
        {
            var enrollment = original with { Binding = original.Binding with { Identity = original.Binding.Identity with { Sequence = index } } };
            (await source.RegisterOriginalAsync(enrollment, token)).ShouldBeTrue(); added.Add(enrollment);
        }
        durable.Root.Originals.Count.ShouldBe(10000); durable.Root.OriginalIndexRoot.ShouldNotBeNull();
        (await source.RegisterOriginalAsync(added[0], token)).ShouldBeTrue();
        await Assert.ThrowsAsync<InvalidOperationException>(() => source.RegisterOriginalAsync(added[0] with { RootOperationId = "substitution" }, token));
        engine.Store.LoseVolume("tenant-a");
        foreach (var enrollment in new[] { original, added[0], added[^1] })
        { ErasureRootCustody.Hash(await source.ResolveAsync(enrollment.Binding.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(enrollment.Binding.Identity), token)).ShouldBe(ErasureRootCustody.Hash(enrollment)); }
        (await source.ResolveAsync(new AggregateIdentity("tenant-b", "conversations", "directory"), ErasureRootOccurrenceBindings.Occurrence(original.Binding.Identity), token)).ShouldBeNull();
    }
    [Fact]
    public async Task IndependentSnapshotFramePreservesNearUpstreamPayloadAndSkeletonWithoutOneOversizedCell()
    {
        using var durable = new ErasureRootIndependentSourceFixture(); var token = TestContext.Current.CancellationToken;
        var index = new ErasureRootSourceIndex(durable.Native, new("independent", "root", new()), "tenant-a", "erc-test", "enrollment");
        var original = new InteractionOccurrenceSealedResult("key", RandomNumberGenerator.GetBytes(16 * 1024 * 1024), "json+pdenc-v2", 1)
            { SnapshotReplaySkeleton = RandomNumberGenerator.GetBytes(16 * 1024 * 1024) };
        string pointer = await index.RetainAsync(null, "key", original, token);
        durable.Chunks.Values.Max(value => value.Length).ShouldBeLessThanOrEqualTo(4 * 1024 * 1024);
        durable.Nodes.Values.Select(value => JsonSerializer.SerializeToUtf8Bytes(value).Length).Max().ShouldBeLessThan(4096);
        var read = (await index.ReadAsync<InteractionOccurrenceSealedResult>(pointer, "key", token))!;
        read.PayloadBytes.ShouldBe(original.PayloadBytes); read.SnapshotReplaySkeleton.ShouldBe(original.SnapshotReplaySkeleton);
        (await index.ReadAsync<InteractionOccurrenceSealedResult>(null, "key", token)).ShouldBeNull();
    }
}
