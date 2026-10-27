namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Versioned exact target and method facts captured by the real dispatch authorization CAS.</summary>
public sealed record IndependentCommandDispatchFacts(string Version,string EnrollmentId,string TrustRevision,long AuthorityRevision,long AuthorizedAt,
    string MessageId,string TenantId,string Domain,string AggregateId,string CommandType,string SourcePermission);
