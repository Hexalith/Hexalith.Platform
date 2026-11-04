namespace Hexalith.Platform.Composition;

using System.Text.Json;
using Hexalith.Builds.Tooling.Manifest;

/// <summary>A completely resolved executable server, with declaration metadata.</summary>
public sealed record PlatformServerBinding(
    PlatformModuleDeclaration Module,
    string ServerId,
    string ResourceId,
    string AppId,
    string? ProjectPath,
    string? Image,
    string ReadinessEndpoint,
    IReadOnlyDictionary<string, string> Environment,
    IReadOnlyList<JsonElement> Tasks)
{
    /// <summary>Gets the independently qualified immutable image digest, absent for unchanged default/source bindings.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? ImageDigest { get; init; }
}
