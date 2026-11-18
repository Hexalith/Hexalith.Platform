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
    /// <summary>Gets the resolved immutable image digest for a released package workload.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? ImageDigest { get; init; }

    /// <summary>Gets the expected configuration digest from the immutable released manifest.</summary>
    internal string? ImageConfigDigest { get; init; }

    /// <summary>Gets the immutable image reference supplied to the runtime.</summary>
    internal string? ImmutableImage => ImageDigest is null || Image is not { } image ? null : image[..image.LastIndexOf(':')] + "@sha256:" + ImageDigest;

    /// <summary>Gets the Platform-composed eventstore host binding; absent unless a module declares an EventStore extension.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public PlatformEventStoreHostBinding? EventStoreHost { get; init; }
}
