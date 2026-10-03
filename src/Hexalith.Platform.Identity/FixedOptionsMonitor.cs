using Microsoft.Extensions.Options;

namespace Hexalith.Platform.Identity;

/// <summary>Reload-aware named purpose configuration for a keyed identity capability.</summary>
/// <typeparam name="T">The purpose options type.</typeparam>
/// <param name="monitor">The configured options monitor.</param>
/// <param name="purpose">The exact purpose name.</param>
internal sealed class FixedOptionsMonitor<T>(IOptionsMonitor<T> monitor, string purpose) : IOptionsMonitor<T>
{
    /// <inheritdoc/>
    public T CurrentValue => monitor.Get(purpose);
    /// <inheritdoc/>
    public T Get(string? name) => monitor.Get(purpose);
    /// <inheritdoc/>
    public IDisposable? OnChange(Action<T, string?> listener) => monitor.OnChange((value, name) =>
    {
        if (name == purpose)
        {
            listener(value, name);
        }
    });
}
