using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Actual published runtime admission with a deliberately absent private credential reference; validation cannot invoke the provider.</summary>
public sealed class QualificationMissingReferenceWorkflow(SecretsRuntimeConfiguration independentlyInstalledConfiguration,ISecretsDaprInvoker transport)
{
    /// <summary>Gets the exact executable source registration.</summary>
    public QualificationSourceWorkflow Registration(IQualificationOriginalStore originals)=>QualificationRuntimeWorkflowCatalog.Retained("S1.reject.missing-reference",originals,VerifyAsync);
    /// <summary>Repeats the pure admission check; only its actual named missing-reference refusal can yield a denial original.</summary>
    public Task<JsonObject?> VerifyAsync(JsonObject original,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(SecretsProof.Text(original,"caseId")!="S1.reject.missing-reference") return Task.FromResult<JsonObject?>(null);
        var services=new ServiceCollection();string? missing=services.AddExplicitHmac(independentlyInstalledConfiguration with { HmacCredentialReference=null },transport);
        return Task.FromResult<JsonObject?>(missing=="runtime.hmacCredentialReference"?new JsonObject { ["caseId"]="S1.reject.missing-reference",["requestId"]=original["requestId"]!.DeepClone(),["outcome"]="Denied",["missingReference"]=missing }:null);
    }
}
