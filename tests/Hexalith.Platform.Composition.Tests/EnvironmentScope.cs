namespace Hexalith.Platform.Composition.Tests;

/// <summary>Sets process environment variables for one serialized test and restores the previous values.</summary>
internal sealed class EnvironmentScope : IDisposable
{
    private readonly Dictionary<string, string?> _previous = new(StringComparer.Ordinal);

    /// <summary>Applies the variables; a null value removes one.</summary>
    internal EnvironmentScope(IReadOnlyDictionary<string, string?> variables)
    {
        foreach (var (name, value) in variables)
        {
            _previous[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var (name, value) in _previous) Environment.SetEnvironmentVariable(name, value);
    }
}
