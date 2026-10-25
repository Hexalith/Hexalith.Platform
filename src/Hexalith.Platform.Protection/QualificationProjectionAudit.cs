using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.Platform.Protection;

/// <summary>Actual projection response identity, byte digest and authoritative route/lifecycle metadata without retaining application content.</summary>
public sealed record QualificationProjectionAudit(string TenantId,string Domain,string AggregateId,string QueryType,string PayloadSha256,QueryResponseMetadata Metadata);
