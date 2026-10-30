using System.Text.Json;
using System.Security.Cryptography;
using Hexalith.EventStore.Contracts.Security;
using Dapr.Client;

namespace Hexalith.Platform.Protection;

/// <summary>Bounded (64 radix levels) exact immutable independent source retention, with no lifetime-sized index or reconstruction chain.</summary>
internal sealed class ErasureRootSourceIndex(DaprClient native, ProtectionQualificationNativeCell location, string tenant, string store, string enrollment)
{
    internal const int TransportChunkBytes = 1024 * 1024;
    internal const string TransportChunkFormat = "erc-source-chunks-v3-1m";
    internal async Task<T?> ReadAsync<T>(string? root, string identity, CancellationToken token) where T : class
    {
        string key = ErasureRootCustody.Hash(identity); string? pointer = root; int depth = 0; string expectedPrefix = "";
        while (pointer is not null)
        {
            var node = await ReadNodeAsync(pointer, token).ConfigureAwait(false);
            if (node.Prefix.Length < depth || !node.Prefix.StartsWith(expectedPrefix, StringComparison.Ordinal)) { throw new InvalidOperationException("Misplaced independent source node."); }
            if (!key.StartsWith(node.Prefix, StringComparison.Ordinal)) { return null; }
            if (node.Identity is not null)
            {
                if (node.Prefix != key || node.Identity != identity || node.Original is null && node.OriginalChunks is null) { return null; }
                return (await ReadOriginalAsync(node, token).ConfigureAwait(false)).Deserialize<T>(ErasureRootCustody.Json);
            }
            if (node.Prefix.Length >= 64 || !node.Children.TryGetValue(key[node.Prefix.Length].ToString(), out pointer)) { return null; }
            depth = node.Prefix.Length + 1; expectedPrefix = key[..depth];
        }
        return null;
    }
    internal Task<string> RetainAsync<T>(string? root, string identity, T original, CancellationToken token)
        => InsertAsync(root, ErasureRootCustody.Hash(identity), identity, JsonSerializer.SerializeToElement(original), 0, token);
    private async Task<string> InsertAsync(string? pointer, string key, string identity, JsonElement value, int depth, CancellationToken token)
    {
        var leaf = new ErasureRootSourceIndexNode(tenant, store, enrollment, key, new(StringComparer.Ordinal), identity, value);
        if (pointer is null) { return await WriteNodeAsync(leaf, token).ConfigureAwait(false); }
        var node = await ReadNodeAsync(pointer, token).ConfigureAwait(false);
        if (node.Prefix.Length < depth || !node.Prefix.StartsWith(key[..depth], StringComparison.Ordinal)) { throw new InvalidOperationException("Invalid source index depth."); }
        int common = depth;
        while (common < Math.Min(key.Length, node.Prefix.Length) && key[common] == node.Prefix[common]) { common++; }
        if (common < node.Prefix.Length)
        {
            var children = new SortedDictionary<string, string>(StringComparer.Ordinal)
            { [node.Prefix[common].ToString()] = pointer, [key[common].ToString()] = await WriteNodeAsync(leaf, token).ConfigureAwait(false) };
            return await WriteNodeAsync(new(tenant, store, enrollment, key[..common], children), token).ConfigureAwait(false);
        }
        if (node.Identity is not null)
        {
            if (node.Identity != identity || node.Prefix != key || ErasureRootCustody.Hash(await ReadOriginalAsync(node, token).ConfigureAwait(false)) != ErasureRootCustody.Hash(value))
            { throw new InvalidOperationException("Immutable source identity conflict."); }
            return pointer;
        }
        if (node.Prefix.Length >= 64) { throw new InvalidOperationException("Invalid source branch."); }
        string digit = key[node.Prefix.Length].ToString(); node.Children.TryGetValue(digit, out var prior);
        string next = await InsertAsync(prior, key, identity, value, node.Prefix.Length + 1, token).ConfigureAwait(false);
        if (next == prior) { return pointer; }
        var updated = new SortedDictionary<string, string>(node.Children, StringComparer.Ordinal) { [digit] = next };
        return await WriteNodeAsync(node with { Children = updated }, token).ConfigureAwait(false);
    }
    private async Task<ErasureRootSourceIndexNode> ReadNodeAsync(string pointer, CancellationToken token)
    {
        if (pointer is not { Length: 64 } || pointer.Any(value => !char.IsAsciiHexDigit(value))) { throw new InvalidOperationException("Invalid source pointer."); }
        var node = await native.GetStateAsync<ErasureRootSourceIndexNode>(location.Component, NodeKey(pointer), ConsistencyMode.Strong, location.Metadata, token).ConfigureAwait(false);
        if (node is null || ErasureRootCustody.Hash(node) != pointer || node.TenantId != tenant || node.StoreId != store || node.EnrollmentId != enrollment
            || node.Prefix.Length > 64 || node.Prefix.Any(value => !char.IsAsciiHexDigit(value)) || node.Children.Count > 16
            || node.Children.Keys.Any(value => value.Length != 1 || !char.IsAsciiHexDigit(value[0]))
            || node.Identity is null && (node.Original is not null || node.OriginalChunks is not null || node.OriginalLength != 0 || node.OriginalSha256 is not null
                || node.OriginalChunkFormat is not null || node.Prefix.Length >= 64 || node.Children.Count < 2)
            || node.Identity is not null && (node.Prefix.Length != 64 || node.Children.Count != 0 || ErasureRootCustody.Hash(node.Identity) != node.Prefix
                || node.Original is null && node.OriginalChunks is null || node.Original is not null && node.OriginalChunks is not null
                || node.Original is not null && (node.OriginalLength != 0 || node.OriginalSha256 is not null || node.OriginalChunkFormat is not null)))
        { throw new InvalidOperationException("Exact authenticated source index required."); }
        return node;
    }
    private async Task<string> WriteNodeAsync(ErasureRootSourceIndexNode node, CancellationToken token)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(node);
        if (bytes.Length > 3 * RecoverableAnchoredState.MaximumPendingBytes) { throw new InvalidOperationException("Upstream source envelope exceeded."); }
        if (node.Original is { } original && bytes.Length > TransportChunkBytes)
        {
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(original);
            var chunks = new List<string>();
            for (int offset = 0; offset < payload.Length; offset += TransportChunkBytes)
            {
                byte[] chunk = payload.AsSpan(offset, Math.Min(TransportChunkBytes, payload.Length - offset)).ToArray();
                string chunkId = Convert.ToHexString(SHA256.HashData(chunk));
                await native.SaveStateAsync(location.Component, ChunkKey(chunkId), chunk, new StateOptions { Consistency = ConsistencyMode.Strong }, location.Metadata, token).ConfigureAwait(false);
                chunks.Add(chunkId);
            }
            node = node with { Original = null, OriginalChunks = chunks.ToArray(), OriginalLength = payload.Length, OriginalSha256 = Convert.ToHexString(SHA256.HashData(payload)), OriginalChunkFormat = TransportChunkFormat };
        }
        string digest = ErasureRootCustody.Hash(node);
        // Identical content-addressed writes are safe; an orphan is never returned without the CAS root.
        await native.SaveStateAsync(location.Component, NodeKey(digest), node,
            new StateOptions { Consistency = ConsistencyMode.Strong }, location.Metadata, token).ConfigureAwait(false);
        return digest;
    }
    private async Task<JsonElement> ReadOriginalAsync(ErasureRootSourceIndexNode node, CancellationToken token)
    {
        if (node.Original is { } value) { return value; }
        int chunkBytes = node.OriginalChunkFormat switch { null => 4 * 1024 * 1024, TransportChunkFormat => TransportChunkBytes,
            _ => throw new InvalidOperationException("Unsupported independent original framing.") };
        if (node.OriginalChunks is not { Length: > 0 and <= 96 } chunks || node.OriginalLength is <= 0 or > 3 * RecoverableAnchoredState.MaximumPendingBytes
            || chunks.Length != (node.OriginalLength + chunkBytes - 1) / chunkBytes)
        { throw new InvalidOperationException("Invalid independent original frame."); }
        byte[] bytes = new byte[node.OriginalLength]; int offset = 0;
        foreach (string pointer in chunks)
        {
            byte[] chunk = await native.GetStateAsync<byte[]>(location.Component, ChunkKey(pointer), ConsistencyMode.Strong, location.Metadata, token).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Missing independent original chunk.");
            if (chunk.Length != Math.Min(chunkBytes, bytes.Length - offset) || Convert.ToHexString(SHA256.HashData(chunk)) != pointer)
            { throw new InvalidOperationException("Independent original chunk conflict."); }
            chunk.CopyTo(bytes, offset); offset += chunk.Length;
        }
        if (Convert.ToHexString(SHA256.HashData(bytes)) != node.OriginalSha256) { throw new InvalidOperationException("Independent original identity conflict."); }
        using var document = JsonDocument.Parse(bytes); return document.RootElement.Clone();
    }
    private string ChunkKey(string pointer) => location.Key + ":source-blob-v2:" + pointer;
    private string NodeKey(string pointer) => location.Key + ":source-original-v2:" + pointer;
}
