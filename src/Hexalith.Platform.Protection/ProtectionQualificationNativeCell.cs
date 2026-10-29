using System.Text.Json.Serialization;

namespace Hexalith.Platform.Protection;

/// <summary>Exact native fixture cell reference. Only independently enrolled non-ERC source components can be restored.</summary>
/// <param name="Component">Actual source component.</param><param name="Key">Actual native key.</param><param name="Metadata">Exact partition metadata.</param>
/// <param name="AnchorScope">Independent ERC anchor when the cell is governed.</param>
public sealed record ProtectionQualificationNativeCell(string Component, string Key, Dictionary<string, string> Metadata, string? AnchorScope = null)
{
    /// <summary>Complete native cell/partition identity, independent of observer purpose and completion metadata.</summary>
    internal static string LocationId(ProtectionQualificationNativeCell cell) => ErasureRootCustody.Hash(new
    { cell.Component, cell.Key, Metadata = cell.Metadata.OrderBy(value => value.Key, StringComparer.Ordinal).ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal) });
    /// <summary>Explicit native original composite-completion journal cell, required for mixed-root snapshot evidence.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CompletionKey { get; init; }
    /// <summary>Closed source-copy purpose for independent projection; references select actual cells, never supplied observation values.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CopyKind { get; init; }
}
