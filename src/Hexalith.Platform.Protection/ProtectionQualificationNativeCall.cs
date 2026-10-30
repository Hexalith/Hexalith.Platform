using System.Text.Json.Nodes;

namespace Hexalith.Platform.Protection;

/// <summary>One exact installed native fixture request. Payloads contain real owner inputs and result references, never expected observations.</summary>
/// <param name="Id">Stable native step identity.</param><param name="Kind">Actual API operation.</param><param name="Payload">Closed native API input.</param>
public sealed record ProtectionQualificationNativeCall(string Id, ProtectionQualificationNativeCallKind Kind, JsonObject Payload)
{
    /// <summary>Exact enclosing admitted role operation; absent on source templates.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? OperationId { get; init; }
    /// <summary>Exact complete wire request digest, excluding no authority-bearing member.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? RequestSha256 { get; init; }
    /// <summary>Captured persisted source/copy fingerprint before native-call admission.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? SourceDigest { get; init; }
    /// <summary>Actual hold-winning guard original captured before conditional owner-call admission.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Hexalith.EventStore.Contracts.Security.TenantGovernanceGuardState? HoldBlockedOriginal { get; init; }
}
