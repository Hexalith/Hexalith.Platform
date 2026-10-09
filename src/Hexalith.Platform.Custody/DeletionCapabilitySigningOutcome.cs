using Hexalith.EventStore.Contracts.Security;
using System.Text.Json.Serialization;
namespace Hexalith.Platform.Custody;

/// <summary>Durable exact signer result containing public artifacts only; actual issue revision remains guard-owned separate evidence.</summary>
/// <param name="SigningRequestId">Deterministic hash of the complete closed canonical payload.</param>
/// <param name="Payload">Original immutable fourteen-field V1 request.</param>
/// <param name="State">Closed opaque outcome.</param>
/// <param name="DetachedJws">The exact retained detached ES256 artifact, only after durable confirmation.</param>
/// <param name="PublicAnchorId">Independently recorded public anchor reference.</param>
/// <param name="PublicAnchorVersion">Exact public verifier version retained through referenced batch/outcome lifetime.</param>
public sealed record DeletionCapabilitySigningOutcome(string SigningRequestId, DeletionBatchCapabilityV1 Payload,
    DeletionCapabilitySigningState State, string? DetachedJws = null, string? PublicAnchorId = null, string? PublicAnchorVersion = null)
{
    /// <summary>Original immutable independently authenticated guard no-issue proof; absent for every other state.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DeletionCapabilityNoIssueProof? NoIssueProof { get; init; }
}
