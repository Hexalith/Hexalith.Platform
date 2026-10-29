using System.Text.Json.Serialization;

namespace Hexalith.Platform.Protection;

/// <summary>Exact native fixture cell reference. Only independently enrolled non-ERC source components can be restored.</summary>
/// <param name="Component">Actual source component.</param><param name="Key">Actual native key.</param><param name="Metadata">Exact partition metadata.</param>
/// <param name="AnchorScope">Independent ERC anchor when the cell is governed.</param>
public sealed record ProtectionQualificationNativeCell(string Component, string Key, Dictionary<string, string> Metadata, string? AnchorScope = null)
{
    /// <summary>Explicit native original composite-completion journal cell, required for mixed-root snapshot evidence.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CompletionKey { get; init; }
    /// <summary>Closed source-copy purpose for independent projection; references select actual cells, never supplied observation values.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CopyKind { get; init; }
}
