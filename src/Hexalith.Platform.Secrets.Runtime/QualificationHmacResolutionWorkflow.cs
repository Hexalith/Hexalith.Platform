using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Actual current key/profile resolution for closed rejection scenarios; unavailable/outage results cannot masquerade as an authenticated denial.</summary>
public sealed class QualificationHmacResolutionWorkflow(IPlatformHmacKeyProvider keys,IPlatformSigningProfileProvider profiles,IReadOnlyDictionary<string,QualificationHmacResolutionCaseInput> enrolledInputs)
{
    /// <summary>Registers only implemented closed key-resolution rejection cases.</summary>
    public IReadOnlyList<QualificationSourceWorkflow> Registrations(IQualificationOriginalStore originals)=>enrolledInputs.Keys.Where(Supported).Select(id=>QualificationRuntimeWorkflowCatalog.Retained(id,originals,ResolveAsync)).ToArray();
    /// <summary>Performs the actual provider resolution and disposes all delivered key material before returning value-free facts.</summary>
    public async Task<JsonObject?> ResolveAsync(JsonObject original,CancellationToken token)
    {
        string? id=SecretsProof.Text(original,"caseId");if(id is null || !Supported(id) || !enrolledInputs.TryGetValue(id,out var input)
            || original["enrolledScope"]?["tenantId"]?.GetValue<string>()!=input.Scope.TenantId) return null;
        var scope=input.Scope;string version=input.Version;
        if(id=="S1.reject.cross-tenant") { if(input.ForeignTenantId==scope.TenantId || string.IsNullOrWhiteSpace(input.ForeignTenantId)) return null;scope=scope with { TenantId=input.ForeignTenantId }; }
        if(id=="S1.reject.wrong-purpose") { if(input.ForeignPurpose==scope.Purpose) return null;scope=scope with { Purpose=input.ForeignPurpose }; }
        if(id=="S1.reject.wrong-version") version+="-substituted";
        var profile=profiles.GetCurrent();
        var result=await keys.ResolveAsync(scope,version,token).ConfigureAwait(false);
        try
        {
            bool exactCondition=id switch { "S1.reject.revoked"=>result.Status==CustodyStatus.RevokedKey,"S1.reject.wrong-version"=>result.Status==CustodyStatus.UnknownKey,
                "S1.reject.denied"=>result.Status==CustodyStatus.Denied,_=>result.Status is CustodyStatus.Denied or CustodyStatus.ScopeMismatch };
            if(!exactCondition) return null;
            string? outcome=result.Status switch { CustodyStatus.Succeeded=>"Resolved",CustodyStatus.Unavailable=>null,_=>"Denied" };
            if(outcome is null) return null;
            return new JsonObject { ["caseId"]=id,["requestId"]=original["requestId"]!.DeepClone(),["outcome"]=outcome,["resolutionStatus"]=result.Status.ToString(),
                ["resolvedReferenceCount"]=result.Key is null?0:1,["profileVersion"]=profile?.Version };
        }
        finally { result.Key?.Dispose(); }
    }
    private static bool Supported(string id)=>id is "S1.reject.denied" or "S1.reject.cross-tenant" or "S1.reject.wrong-purpose" or "S1.reject.wrong-version" or "S1.reject.revoked";
}
