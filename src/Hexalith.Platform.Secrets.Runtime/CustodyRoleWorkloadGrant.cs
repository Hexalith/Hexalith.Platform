namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Exact current independently enrolled workload grant; contains no bearer.</summary>
public sealed record CustodyRoleWorkloadGrant(string Identity,string BearerSha256,string Role,string ScopeReference,string IntentSha256,string GrantSha256,DateTimeOffset ValidUntil);
