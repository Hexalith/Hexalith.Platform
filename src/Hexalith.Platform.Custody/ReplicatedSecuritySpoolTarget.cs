namespace Hexalith.Platform.Custody;

/// <summary>Independently qualified private replicated nonrollback component installation. Metadata strings/configuration alone do not qualify it.</summary>
/// <param name="ComponentName">Exact separately provisioned DAPR state component.</param>
/// <param name="InstallationEpoch">Independent current restore/installation namespace.</param>
/// <param name="AuthorityRevision">Current independent target/credential/replica/failure-model qualification.</param>
/// <param name="ValidUntil">Exclusive current qualification expiry.</param>
/// <param name="SourceVersion">Zero preserves the exact legacy source; two explicitly selects the independently enrolled hashed source.</param>
public sealed record ReplicatedSecuritySpoolTarget(string ComponentName, string InstallationEpoch, string AuthorityRevision, DateTimeOffset ValidUntil, [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] int SourceVersion=0);
