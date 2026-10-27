namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Exact workload proof and permission ordered with dispatch under the surviving revocation head.</summary>
public sealed record IndependentCommandDispatchBinding(string Caller,string Principal,string Domain,string CommandSha256,string GrantSha256,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IndependentCommandDispatchFacts? Facts=null);
