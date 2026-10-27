using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Independently installed dispatch authority and exact native stream to logical target mappings.</summary>
public sealed record QualificationMutationTrust(IndependentDaprAuthorityBinding Authority,IReadOnlyDictionary<string,string> TargetMappings)
{
    /// <summary>Canonical exact native stream identity used by the installed map.</summary>
    public static string TargetKey(string tenant,string domain,string aggregate)=>QualificationProtocolSchema.Digest(new JsonObject { ["tenantId"]=tenant,["domain"]=domain,["aggregateId"]=aggregate });
}
