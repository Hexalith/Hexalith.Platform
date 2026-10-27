namespace Hexalith.Platform.Protection;

/// <summary>Independently enrolled immutable dispatch-original locator and exact logical target mapping for a workload command.</summary>
public sealed record QualificationDispatchOriginalReference(string Role,string MessageId,string TargetId,string Domain,string AggregateId);
