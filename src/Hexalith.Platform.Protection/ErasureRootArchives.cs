using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Protection;

/// <summary>Exact immutable archive access within the same snapshot-excluded ERC. Unreferenced/prewritten nodes confer no completion authority.</summary>
internal static class ErasureRootArchives
{
    internal const int ChunkBytes = 4 * 1024 * 1024;
    internal const int MaximumOriginalBytes = 48 * 1024 * 1024;
    internal static async Task<byte[]?> ReadBytesAsync(IErasureRootStore store, string tenant, string id, CancellationToken token)
    {
        return await ReadBytesAsync(store, tenant, id, token, 0).ConfigureAwait(false);
    }
    private static async Task<byte[]?> ReadBytesAsync(IErasureRootStore store, string tenant, string id, CancellationToken token, int depth)
    {
        byte[]? bytes = await store.ReadArchiveAsync(tenant, id, token).ConfigureAwait(false);
        if (bytes is null) { return null; }
        if (bytes.Length is <= 0 or > MaximumOriginalBytes) { throw new InvalidOperationException("Bounded ERC archive required."); }
        if (Convert.ToHexString(SHA256.HashData(bytes)) == id) { return bytes; }
        using var document = JsonDocument.Parse(bytes);
        if (document.RootElement.TryGetProperty("Format", out var format) && format.GetString() == "erc-transition-v3")
        {
            if (depth != 0) { throw new InvalidOperationException("Recursive ERC transition frame."); }
            var transition = JsonSerializer.Deserialize<ErasureRootTransitionPayloadArchive>(bytes, ErasureRootCustody.Json)!;
            if (string.IsNullOrWhiteSpace(transition.ScopeId) || transition.TargetRevision <= 0
                || transition.ExpectedRevision != transition.TargetRevision - 1 || transition.TargetArchive != transition.TargetDigest)
            { throw new InvalidOperationException("ERC transition framing conflict."); }
            byte[] target = await ReadBytesAsync(store, tenant, transition.TargetArchive, token, depth + 1).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Missing ERC transition payload.");
            var reconstructedTransition = new AnchoredStateTransition(transition.ScopeId, transition.ExpectedRevision, transition.TargetRevision,
                transition.PredecessorDigest, transition.TargetDigest, target);
            byte[] reconstructed = JsonSerializer.SerializeToUtf8Bytes(reconstructedTransition, ErasureRootCustody.Json);
            if (reconstructed.Length > MaximumOriginalBytes || Convert.ToHexString(SHA256.HashData(reconstructed)) != id)
            { throw new InvalidOperationException("ERC transition original identity conflict."); }
            return reconstructed;
        }
        var frame = JsonSerializer.Deserialize<ErasureRootChunkedArchive>(bytes, ErasureRootCustody.Json)
            ?? throw new InvalidOperationException("Missing ERC archive frame.");
        if (frame.Length is <= 0 or > MaximumOriginalBytes
            || frame.Format is not ("erc-chunks-v1" or "erc-shared-v2")
            || frame.Format == "erc-chunks-v1" && (frame.Length <= ChunkBytes || frame.Chunks.Length != (frame.Length + ChunkBytes - 1) / ChunkBytes)
            || frame.Chunks.Length is < 1 or > 3073) { throw new InvalidOperationException("Invalid ERC archive frame."); }
        if (frame.Chunks.Any(pointer => pointer is not { Length: 64 } || pointer.Any(value => !char.IsAsciiHexDigit(value))))
        { throw new InvalidOperationException("Exact archive chunk pointers required."); }
        byte[] original = new byte[frame.Length];
        var retained = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        int offset = 0, retainedBytes = 0;
        // One bounded acquisition at a time: malformed/hash-valid bytes never accumulate in a
        // batch or the cache before their format and cumulative memory bounds are checked.
        foreach (string chunkId in frame.Chunks)
        {
            if (!retained.TryGetValue(chunkId, out var chunk))
            {
                chunk = await store.ReadArchiveAsync(tenant, chunkId, token).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("Missing ERC archive chunk.");
                int maximum = frame.Format == "erc-chunks-v1" ? ChunkBytes : 128 * 1024;
                if (chunk.Length <= 0 || chunk.Length > maximum || chunk.Length > frame.Length - offset
                    || chunk.Length > frame.Length - retainedBytes
                    || Convert.ToHexString(SHA256.HashData(chunk)) != chunkId)
                { throw new InvalidOperationException("Bounded authenticated ERC archive chunk required."); }
                retainedBytes += chunk.Length;
                retained.Add(chunkId, chunk);
            }
            if (chunk.Length > frame.Length - offset
                || frame.Format == "erc-chunks-v1" && chunk.Length != Math.Min(ChunkBytes, frame.Length - offset)
                || frame.Format == "erc-shared-v2" && (chunk.Length > 128 * 1024 || offset + chunk.Length < frame.Length && chunk.Length < 16 * 1024))
            { throw new InvalidOperationException("ERC archive chunk framing conflict."); }
            chunk.CopyTo(original, offset); offset += chunk.Length;
        }
        if (offset != frame.Length || Convert.ToHexString(SHA256.HashData(original)) != id) { throw new InvalidOperationException("ERC original identity conflict."); }
        return original;
    }
    internal static async Task<SortedDictionary<string, AnchoredStateTransition>> ReadTransitionsAsync(IErasureRootStore store,
        string tenant, string scope, ErasureRootAnchor anchor, bool history, CancellationToken token)
    {
        var result = new SortedDictionary<string, AnchoredStateTransition>(StringComparer.Ordinal);
        if (anchor.ArchiveHead is null)
        { foreach (var pair in anchor.Completed) { result.Add(pair.Key, pair.Value); } return result; }
        for (long revision = anchor.ArchiveRevision; revision > 0; revision--)
        {
            var original = await ReadTransitionAtAsync(store, tenant, scope, anchor, revision, token).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Committed transition missing.");
            result.Add(ErasureRootCustody.Hash(original), original);
            if (!history) { break; }
        }
        return result;
    }
    internal static async Task<AnchoredStateTransition?> ReadTransitionAtAsync(IErasureRootStore store, string tenant,
        string scope, ErasureRootAnchor anchor, long revision, CancellationToken token)
    {
        if (revision <= 0 || revision > anchor.Revision) { return null; }
        if (anchor.ArchiveHead is null) { return anchor.Completed.Values.SingleOrDefault(value => value.TargetRevision == revision); }
        string? pointer = anchor.ArchiveHead; long expected = anchor.ArchiveRevision;
        string? expectedDigest = anchor.Digest;
        while (pointer is not null)
        {
            byte[] bytes = await ReadBytesAsync(store, tenant, pointer, token).ConfigureAwait(false) ?? throw new InvalidOperationException("Committed archive unavailable.");
            using var document = JsonDocument.Parse(bytes);
            if (document.RootElement.TryGetProperty("Original", out _))
            {
                var legacy = JsonSerializer.Deserialize<ErasureRootTransitionArchive>(bytes, ErasureRootCustody.Json)!;
                Validate(legacy.ScopeId, legacy.Original, expected, expectedDigest);
                if (expected == revision) { return legacy.Original; }
                pointer = legacy.Previous; expected--; expectedDigest = legacy.Original.PredecessorDigest;
                continue;
            }
            var node = JsonSerializer.Deserialize<ErasureRootIndexedTransitionArchive>(bytes, ErasureRootCustody.Json)!;
            if (node.ScopeId != scope || node.Revision != expected || expectedDigest is not null && node.Digest != expectedDigest
                || node.Ancestors.Length > 63 || node.Ancestors.Length == 0 && expected > 1
                || node.Ancestors.Length > 0 && node.Ancestors[0] != node.Previous)
            { throw new InvalidOperationException("ERC indexed ancestry conflict."); }
            if (expected == revision)
            {
                var payload = await ReadBytesAsync(store, tenant, node.OriginalArchive, token).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("Committed original unavailable.");
                var original = JsonSerializer.Deserialize<AnchoredStateTransition>(payload, ErasureRootCustody.Json)!;
                Validate(scope, original, expected, node.Digest);
                if (ErasureRootCustody.Hash(original) != node.OriginalArchive || original.PredecessorDigest != node.PredecessorDigest)
                { throw new InvalidOperationException("ERC indexed original conflict."); }
                return original;
            }
            int jump = 0;
            while (jump + 1 < node.Ancestors.Length && (1L << (jump + 1)) <= expected - revision) { jump++; }
            if (node.Ancestors.Length == 0) { throw new InvalidOperationException("Missing ancestry."); }
            pointer = node.Ancestors[jump]; expected -= 1L << jump;
            expectedDigest = jump == 0 ? node.PredecessorDigest : null;
        }
        throw new InvalidOperationException("Incomplete committed ancestry.");
        void Validate(string enrolledScope, AnchoredStateTransition original, long target, string? digest)
        {
            if (enrolledScope != scope || original.ScopeId != scope || original.TargetRevision != target
                || original.ExpectedRevision != target - 1 || digest is not null && original.TargetDigest != digest
                || Convert.ToHexString(SHA256.HashData(original.TargetBytes)) != original.TargetDigest)
            { throw new InvalidOperationException("ERC original ancestry conflict."); }
        }
    }
    internal static async Task<string[]> AncestorsAsync(IErasureRootStore store, string tenant, string? previous, long revision, CancellationToken token)
    {
        if (previous is null) { return []; }
        var result = new List<string> { previous };
        for (int level = 1; level < 63 && (1L << level) < revision; level++)
        {
            byte[] bytes = await ReadBytesAsync(store, tenant, result[level - 1], token).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Missing committed ancestry.");
            using var doc = JsonDocument.Parse(bytes);
            if (doc.RootElement.TryGetProperty("Original", out _)) { break; }
            var node = JsonSerializer.Deserialize<ErasureRootIndexedTransitionArchive>(bytes, ErasureRootCustody.Json)!;
            if (node.Ancestors.Length < level) { break; }
            result.Add(node.Ancestors[level - 1]);
        }
        return result.ToArray();
    }
    internal static async Task<string> WriteAsync<T>(IErasureRootStore store, string tenant, T value, CancellationToken token, string? predecessor = null)
    {
        string id = ErasureRootCustody.Hash(value);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, ErasureRootCustody.Json);
        if (bytes.Length > MaximumOriginalBytes) { throw new InvalidOperationException("Bounded ERC original exceeded."); }
        // Existing immutable formats are retained byte-for-byte; no rewrite/migration is inferred from an orphan.
        byte[]? existing = await store.ReadArchiveAsync(tenant, id, token).ConfigureAwait(false);
        if (existing is not null)
        {
            var exact = await ReadBytesAsync(store, tenant, id, token).ConfigureAwait(false);
            return exact is not null && exact.AsSpan().SequenceEqual(bytes) ? id : throw new InvalidOperationException("ERC immutable archive conflict.");
        }
        if (value is AnchoredStateTransition transition)
        {
            string target = Convert.ToHexString(SHA256.HashData(transition.TargetBytes));
            if (target != transition.TargetDigest) { throw new InvalidOperationException("ERC transition payload identity conflict."); }
            string? previousTarget = null;
            if (predecessor is not null)
            {
                byte[] prior = await store.ReadArchiveAsync(tenant, predecessor, token).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("Committed predecessor unavailable.");
                if (Convert.ToHexString(SHA256.HashData(prior)) != predecessor)
                {
                    using var priorDocument = JsonDocument.Parse(prior);
                    if (priorDocument.RootElement.TryGetProperty("Format", out var priorFormat) && priorFormat.GetString() == "erc-transition-v3")
                    {
                        var frame = JsonSerializer.Deserialize<ErasureRootTransitionPayloadArchive>(prior, ErasureRootCustody.Json)!;
                        if (frame.ScopeId != transition.ScopeId || frame.TargetRevision != transition.ExpectedRevision
                            || frame.TargetDigest != transition.PredecessorDigest || frame.TargetArchive != frame.TargetDigest)
                        { throw new InvalidOperationException("ERC predecessor payload conflict."); }
                        previousTarget = frame.TargetArchive;
                    }
                }
            }
            await WriteBytesAsync(store, tenant, target, transition.TargetBytes, token, previousTarget).ConfigureAwait(false);
            byte[] frameBytes = JsonSerializer.SerializeToUtf8Bytes(new ErasureRootTransitionPayloadArchive("erc-transition-v3",
                transition.ScopeId, transition.ExpectedRevision, transition.TargetRevision, transition.PredecessorDigest, transition.TargetDigest, target), ErasureRootCustody.Json);
            if (!await store.CreateArchiveAsync(tenant, id, frameBytes, token).ConfigureAwait(false))
            { throw new InvalidOperationException("ERC immutable transition conflict."); }
            return id;
        }
        await WriteBytesAsync(store, tenant, id, bytes, token, predecessor).ConfigureAwait(false);
        return id;
    }
    private static async Task WriteBytesAsync(IErasureRootStore store, string tenant, string id, byte[] bytes, CancellationToken token, string? predecessor)
    {
        if (bytes.Length > MaximumOriginalBytes) { throw new InvalidOperationException("Bounded ERC payload exceeded."); }
        byte[]? existing = await store.ReadArchiveAsync(tenant, id, token).ConfigureAwait(false);
        if (existing is not null)
        {
            byte[]? exact = await ReadBytesAsync(store, tenant, id, token).ConfigureAwait(false);
            if (exact is null || !exact.AsSpan().SequenceEqual(bytes)) { throw new InvalidOperationException("ERC immutable payload conflict."); }
            return;
        }
        if (bytes.Length >= 64 * 1024)
        {
            var shared = new HashSet<string>(StringComparer.Ordinal);
            if (predecessor is not null)
            {
                byte[]? prior = await store.ReadArchiveAsync(tenant, predecessor, token).ConfigureAwait(false);
                if (prior is null) { throw new InvalidOperationException("Committed predecessor unavailable."); }
                if (Convert.ToHexString(SHA256.HashData(prior)) != predecessor)
                {
                    var frame = JsonSerializer.Deserialize<ErasureRootChunkedArchive>(prior, ErasureRootCustody.Json)!;
                    if (frame.Format == "erc-shared-v2") { shared.UnionWith(frame.Chunks); }
                }
            }
            var chunks = new List<string>(); int offset = 0;
            while (offset < bytes.Length)
            {
                int length = 0; ulong rolling = 0;
                while (offset + length < bytes.Length && length < 128 * 1024)
                {
                    // A 64-byte rolling polynomial forgets the prior cut, so inserts resynchronize boundaries.
                    rolling = unchecked((rolling << 1) + Gear(bytes[offset + length]));
                    length++;
                    if (length >= 16 * 1024 && (rolling & 65535) == 0) { break; }
                }
                byte[] chunk = bytes.AsSpan(offset, length).ToArray();
                string chunkId = Convert.ToHexString(SHA256.HashData(chunk));
                if (!shared.Contains(chunkId) && !await store.CreateArchiveAsync(tenant, chunkId, chunk, token).ConfigureAwait(false))
                { throw new InvalidOperationException("ERC immutable chunk conflict."); }
                shared.Add(chunkId); chunks.Add(chunkId); offset += length;
            }
            if (chunks.Count == 1 && chunks[0] == id) { return; }
            bytes = JsonSerializer.SerializeToUtf8Bytes(new ErasureRootChunkedArchive("erc-shared-v2", bytes.Length, chunks.ToArray()));
        }
        if (!await store.CreateArchiveAsync(tenant, id, bytes, token).ConfigureAwait(false)) { throw new InvalidOperationException("ERC immutable archive conflict."); }
    }
    private static ulong Gear(byte value)
    {
        ulong hash = (ulong)value + 0x9e3779b97f4a7c15UL;
        hash = unchecked((hash ^ (hash >> 30)) * 0xbf58476d1ce4e5b9UL);
        hash = unchecked((hash ^ (hash >> 27)) * 0x94d049bb133111ebUL);
        return hash ^ (hash >> 31);
    }
}
