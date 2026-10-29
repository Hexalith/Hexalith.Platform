using System.Text.Json.Nodes;

namespace Hexalith.Platform.Protection;

/// <summary>Content-free exact admitted native call and its real retained result; no canary plaintext is retained.</summary>
/// <param name="Call">Exact typed native input.</param><param name="Result">Actual native outcome, absent while unresolved.</param>
public sealed record ProtectionQualificationNativeOriginal(ProtectionQualificationNativeCall Call, JsonNode? Result = null);
