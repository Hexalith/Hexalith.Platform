namespace Hexalith.Platform.Protection;

/// <summary>Exact ambient owner commit boundary for explicitly installed qualification controls. Async execution contexts keep competing owners separate.</summary>
internal sealed class ErasureRootNativeOperationScope : IDisposable
{
    private static readonly AsyncLocal<string?> Active = new();
    private readonly string? _prior;
    internal static string? Current => Active.Value;
    internal ErasureRootNativeOperationScope(string tenant, string scope, string operation)
    { _prior = Active.Value; Active.Value = "owner:" + tenant + ":" + scope + ":" + operation; }
    public void Dispose() => Active.Value = _prior;
}
