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
    IReadOnlyList<JsonElement> Tasks);
