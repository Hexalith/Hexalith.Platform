using System.Text.Json.Nodes;

namespace Hexalith.Platform.Protection;

/// <summary>One exact installed native fixture request. Payloads contain real owner inputs and result references, never expected observations.</summary>
/// <param name="Id">Stable native step identity.</param><param name="Kind">Actual API operation.</param><param name="Payload">Closed native API input.</param>
public sealed record ProtectionQualificationNativeCall(string Id, ProtectionQualificationNativeCallKind Kind, JsonObject Payload);
