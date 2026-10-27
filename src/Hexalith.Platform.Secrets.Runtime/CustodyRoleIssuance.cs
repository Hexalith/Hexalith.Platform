namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Current workload issuance ordered at the independent conditional authority head.</summary>
public sealed record CustodyRoleIssuance(CustodyRoleWorkloadGrant Grant,string Method,string ResponseSha256);
