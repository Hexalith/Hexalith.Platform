using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Authenticated exact original read. Unknown transport is distinct from a signed absent original.</summary>
public sealed record QualificationOriginalLookup(bool Known,JsonObject? Request=null,JsonObject? Result=null);
