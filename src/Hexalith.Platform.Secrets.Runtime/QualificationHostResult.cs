using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Public endpoint result. A lost provider result retains the original and requires exact lookup.</summary>
public sealed record QualificationHostResult(int StatusCode, JsonObject Body);
