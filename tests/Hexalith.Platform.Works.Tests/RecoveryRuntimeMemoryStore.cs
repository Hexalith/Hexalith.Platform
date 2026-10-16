using System.Globalization;

using Hexalith.EventStore.Client.Projections;

namespace Hexalith.Platform.Works.Tests;

/// <summary>ETag store that exposes audit failure and committed write order to qualification tests.</summary>
internal sealed class RecoveryRuntimeMemoryStore : IReadModelStore
{
    private readonly Dictionary<string, (object Value, long Version)> _values = new(StringComparer.Ordinal);

    /// <summary>Gets or sets whether only audit writes fail.</summary>
    internal bool AuditUnavailable { get; set; }

    /// <summary>Gets or sets a bounded number of simulated audit CAS conflicts.</summary>
    internal int AuditConflicts { get; set; }

    /// <summary>Gets committed key write order.</summary>
    internal List<string> Writes { get; } = [];

    /// <inheritdoc/>
    public Task<ReadModelEntry<TValue>> GetAsync<TValue>(string storeName, string key, CancellationToken cancellationToken = default) where TValue : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_values.TryGetValue(storeName + ":" + key, out var entry)
            ? new ReadModelEntry<TValue>((TValue)entry.Value, entry.Version.ToString(CultureInfo.InvariantCulture))
            : new ReadModelEntry<TValue>(null, null));
    }

    /// <inheritdoc/>
    public Task SaveAsync<TValue>(string storeName, string key, TValue value, CancellationToken cancellationToken = default) where TValue : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        string address = storeName + ":" + key;
        _values[address] = (value, _values.GetValueOrDefault(address).Version + 1);
        Writes.Add(key);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> TrySaveAsync<TValue>(string storeName, string key, TValue value, string etag, CancellationToken cancellationToken = default) where TValue : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (key.StartsWith("platform:works:process-disposition-audit:", StringComparison.Ordinal))
        {
            if (AuditUnavailable) throw new IOException("Synthetic audit state unavailable.");
            if (AuditConflicts > 0) { AuditConflicts--; return Task.FromResult(false); }
        }
        string address = storeName + ":" + key;
        var prior = _values.GetValueOrDefault(address);
        string expected = prior.Value is null ? string.Empty : prior.Version.ToString(CultureInfo.InvariantCulture);
        if (expected != etag) return Task.FromResult(false);
        _values[address] = (value, prior.Version + 1);
        Writes.Add(key);
        return Task.FromResult(true);
    }
}
