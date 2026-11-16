namespace Hexalith.Platform.Custody;

/// <summary>Independent authoritative exact SecurityEventRecorded proof; gateway acceptance/mock is insufficient.</summary>
/// <param name="ObservationId">Original observation.</param>
/// <param name="RoutingTenantId">Original routing tenant.</param>
/// <param name="UtcDay">Exact source day.</param>
/// <param name="OriginalIntentDigest">Digest of content-free original intent only.</param>
/// <param name="SourceStreamId">Exact SecurityEventLog actor/stream.</param>
/// <param name="SourceRevision">Original durable event position.</param>
/// <param name="EventId">Original stable persisted event identity.</param>
public sealed record SecurityEventRecordReceipt(string ObservationId, string RoutingTenantId, string UtcDay,
    string OriginalIntentDigest, string SourceStreamId, long SourceRevision, string EventId);
