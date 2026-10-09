namespace Hexalith.Platform.Custody;

/// <summary>Private recorder-produced safe intent. Unknown/untrusted fields are represented only by a reserved-system retained-key HMAC.</summary>
/// <param name="ObservationId">Stable pre-spool observation identity.</param>
/// <param name="RoutingTenantId">Independently computed authenticated routing tenant, or system.</param>
/// <param name="ReasonCode">Closed content-free denial code.</param>
/// <param name="UntrustedFieldsHmac">System SecurityObservation HMAC; no raw claims/request/log input.</param>
/// <param name="DigestKeyVersion">Original retained system observation-key version.</param>
public sealed record SecurityObservationIntent(string ObservationId, string RoutingTenantId, string ReasonCode, string UntrustedFieldsHmac, string DigestKeyVersion)
{
    /// <summary>Opaque original retained-server-receipt lookup index, committed atomically with the full observation identity and original authenticated route.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? RetainedServerReceiptKey { get; init; }
    /// <inheritdoc/>
    public override string ToString() => nameof(SecurityObservationIntent);
}
