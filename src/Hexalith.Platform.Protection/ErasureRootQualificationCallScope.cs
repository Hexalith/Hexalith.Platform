namespace Hexalith.Platform.Protection;

/// <summary>Exact internal admitted provider-call context; its terminal result identity commits atomically with the original manifest.</summary>
internal sealed class ErasureRootQualificationCallScope : IDisposable
{
    private static readonly AsyncLocal<string?> Active = new();
    private readonly string? _prior;
    internal static string? Current => Active.Value;
    internal ErasureRootQualificationCallScope(string digest) { _prior = Active.Value; Active.Value = digest; }
    public void Dispose() => Active.Value = _prior;
}
