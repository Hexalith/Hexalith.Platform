using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Protection;

/// <summary>Exact immutable archive access within the same snapshot-excluded ERC. Unreferenced/prewritten nodes confer no completion authority.</summary>
internal static class ErasureRootArchives
{
    internal static async Task<SortedDictionary<string, AnchoredStateTransition>> ReadTransitionsAsync(IErasureRootStore store,
        string tenant, string scope, ErasureRootAnchor anchor, bool history, CancellationToken token)
    {
        var result = new SortedDictionary<string, AnchoredStateTransition>(StringComparer.Ordinal);
        if (anchor.ArchiveHead is null)
        {
            foreach (var pair in anchor.Completed) { result.Add(pair.Key, pair.Value); }
            return result;
        }
        long expected = anchor.ArchiveRevision;
        string? pointer = anchor.ArchiveHead;
        string? digest = anchor.ArchiveRevision == anchor.Revision ? anchor.Digest : null;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (pointer is not null)
        {
            if (!visited.Add(pointer)) { throw new InvalidOperationException("Cyclic ERC archive."); }
            var bytes = await store.ReadArchiveAsync(tenant, pointer, token).ConfigureAwait(false) ?? throw new InvalidOperationException("Committed ERC archive unavailable.");
            var node = JsonSerializer.Deserialize<ErasureRootTransitionArchive>(bytes, ErasureRootCustody.Json) ?? throw new InvalidOperationException("Malformed ERC archive.");
            var original = node.Original;
            if (pointer != ErasureRootCustody.Hash(node) || node.ScopeId != scope || original.ScopeId != scope
                || original.TargetRevision != expected || original.ExpectedRevision != expected - 1
                || Convert.ToHexString(SHA256.HashData(original.TargetBytes)) != original.TargetDigest
                || digest is not null && original.TargetDigest != digest) { throw new InvalidOperationException("ERC archive ancestry conflict."); }
            result.Add(ErasureRootCustody.Hash(original), original);
            if (!history) { break; }
            expected--; digest = original.PredecessorDigest; pointer = node.Previous;
        }
        if (history && expected != 0) { throw new InvalidOperationException("Incomplete ERC archive ancestry."); }
        return result;
    }
    internal static async Task<string> WriteAsync<T>(IErasureRootStore store, string tenant, T value, CancellationToken token)
    {
        string id = ErasureRootCustody.Hash(value);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, ErasureRootCustody.Json);
        if (!await store.CreateArchiveAsync(tenant, id, bytes, token).ConfigureAwait(false)) { throw new InvalidOperationException("ERC immutable archive conflict."); }
        return id;
    }
}
