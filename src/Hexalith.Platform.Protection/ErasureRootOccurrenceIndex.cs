using System.Text.Json;

namespace Hexalith.Platform.Protection;

/// <summary>Exact bounded-depth persistent occurrence state. Updates share untouched branches and never grow a tenant pointer map.</summary>
internal sealed class ErasureRootOccurrenceIndex(IErasureRootStore store, ErasureRootInstallation installation, string tenant)
{
    internal async Task<T?> ReadAsync<T>(string? root, string identity, CancellationToken token) where T : class
    {
        string key = ErasureRootCustody.Hash(identity); int depth = 0;
        while (root is not null)
        {
            var node = await NodeAsync(root, token).ConfigureAwait(false);
            if (node.Prefix.Length < depth || !key.StartsWith(node.Prefix, StringComparison.Ordinal)) { return null; }
            if (node.Identity is not null)
            {
                if (node.Identity != identity || node.Prefix != key) { throw new InvalidOperationException("Occurrence index collision."); }
                return node.Original!.Value.Deserialize<T>(ErasureRootCustody.Json) ?? throw new InvalidOperationException("Invalid occurrence original.");
            }
            depth = node.Prefix.Length + 1;
            if (!node.Children.TryGetValue(key[node.Prefix.Length].ToString(), out root)) { return null; }
        }
        return null;
    }
    internal Task<string> WriteAsync<T>(string? root, string identity, T value, CancellationToken token)
        => InsertAsync(root, ErasureRootCustody.Hash(identity), identity, JsonSerializer.SerializeToElement(value), 0, token);
    private async Task<string> InsertAsync(string? root, string key, string identity, JsonElement value, int depth, CancellationToken token)
    {
        var leaf = new ErasureRootOccurrenceIndexNode(tenant, installation.StoreId, installation.EnrollmentId, key, new(StringComparer.Ordinal), identity, value);
        if (root is null) { return await SaveAsync(leaf, token).ConfigureAwait(false); }
        var node = await NodeAsync(root, token).ConfigureAwait(false);
        if (node.Prefix.Length < depth) { throw new InvalidOperationException("Invalid occurrence index depth."); }
        int common = depth;
        while (common < node.Prefix.Length && key[common] == node.Prefix[common]) { common++; }
        if (common < node.Prefix.Length)
        {
            var children = new SortedDictionary<string, string>(StringComparer.Ordinal)
            { [node.Prefix[common].ToString()] = root, [key[common].ToString()] = await SaveAsync(leaf, token).ConfigureAwait(false) };
            return await SaveAsync(new(tenant, installation.StoreId, installation.EnrollmentId, key[..common], children), token).ConfigureAwait(false);
        }
        if (node.Identity is not null)
        {
            if (node.Identity != identity || node.Prefix != key) { throw new InvalidOperationException("Occurrence identity conflict."); }
            return ErasureRootCustody.Hash(node.Original) == ErasureRootCustody.Hash(value) ? root : await SaveAsync(leaf, token).ConfigureAwait(false);
        }
        string digit = key[node.Prefix.Length].ToString(); node.Children.TryGetValue(digit, out string? prior);
        string next = await InsertAsync(prior, key, identity, value, node.Prefix.Length + 1, token).ConfigureAwait(false);
        if (next == prior) { return root; }
        var updated = new SortedDictionary<string, string>(node.Children, StringComparer.Ordinal) { [digit] = next };
        return await SaveAsync(node with { Children = updated }, token).ConfigureAwait(false);
    }
    private async Task<ErasureRootOccurrenceIndexNode> NodeAsync(string pointer, CancellationToken token)
    {
        if (pointer.Length != 64 || pointer.Any(value => !char.IsAsciiHexDigit(value))) { throw new InvalidOperationException("Exact occurrence pointer required."); }
        byte[] bytes = await ErasureRootArchives.ReadBytesAsync(store, tenant, pointer, token).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Committed occurrence node unavailable.");
        var node = JsonSerializer.Deserialize<ErasureRootOccurrenceIndexNode>(bytes, ErasureRootCustody.Json);
        if (node is null || ErasureRootCustody.Hash(node) != pointer || node.TenantId != tenant || node.StoreId != installation.StoreId || node.EnrollmentId != installation.EnrollmentId
            || node.Prefix.Length > 64 || node.Prefix.Any(value => !char.IsAsciiHexDigit(value)) || node.Children.Count > 16
            || node.Children.Keys.Any(value => value.Length != 1 || !char.IsAsciiHexDigit(value[0]))
            || node.Identity is null && (node.Original is not null || node.Prefix.Length >= 64 || node.Children.Count < 2)
            || node.Identity is not null && (node.Prefix.Length != 64 || node.Children.Count != 0 || node.Original is null || ErasureRootCustody.Hash(node.Identity) != node.Prefix))
        { throw new InvalidOperationException("Authenticated occurrence node required."); }
        return node;
    }
    private Task<string> SaveAsync(ErasureRootOccurrenceIndexNode node, CancellationToken token)
        => ErasureRootArchives.WriteAsync(store, tenant, node, token);
}
