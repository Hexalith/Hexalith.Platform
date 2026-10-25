using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Client;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.Platform.Secrets.Runtime;
using Microsoft.Extensions.Options;

namespace Hexalith.Platform.Protection;

/// <summary>Shipped stdin phase-command entrypoint over closed actual guard workflows and independent native publication. This is source composition; it creates no enrollment and cannot fill absent case groups or native read grants.</summary>
public static class JointPhaseCommandExecutable
{
    /// <summary>Executes only the exact installed partition. Missing ordinary adapters or selected protocols return nonzero before starting that partition.</summary>
    public static async Task<int> RunAsync(string? installationPath,CancellationToken token=default)
    {
        if(installationPath is null || !Path.IsPathFullyQualified(installationPath)) return 2;
        byte[] bytes=await File.ReadAllBytesAsync(installationPath,token).ConfigureAwait(false);if(bytes.Length is 0 or >1024*1024) return 2;
        CustodyBindingContract.RejectDuplicateProperties(bytes);var installed=JsonSerializer.Deserialize<JointPhaseCommandInstallation>(bytes,CustodyBindingContract.Json);if(installed is null) return 2;
        if(installed.JointCommandOriginals is null || installed.Scope.Cases.Any(id=>id.StartsWith("secrets:",StringComparison.Ordinal)) && installed.JointSecretsOriginals is null) return 2;
        using var owner=await OpenSignerAsync(installed.OwnerSigningKeyFile,installed.OwnerPublicKeyFile,token).ConfigureAwait(false);
        using var stdin=new MemoryStream();byte[] buffer=new byte[8192];int length;
        while((length=await Console.OpenStandardInput().ReadAsync(buffer,token).ConfigureAwait(false))>0) { if(stdin.Length+length>1024*1024) return 2;await stdin.WriteAsync(buffer.AsMemory(0,length),token).ConfigureAwait(false); }
        var request=SecretsProof.Parse(System.Text.Encoding.UTF8.GetString(stdin.ToArray()));if(request is null) return 2;
        using var dapr=new DaprClientBuilder().Build();var transport=new DaprSecretsInvoker(dapr);var clock=TimeProvider.System;
        var originalAuthority=new IndependentDaprAuthority(transport,installed.ExecutorOriginals,clock);var originals=new InstalledQualificationOriginalStore(originalAuthority);
        var runtime=SecretsRuntimeConfiguration.Load(installed.RuntimeConfigurationFile);if(runtime is null) return 2;
        var ports=QualificationGuardProductionComposition.Create(dapr,runtime,installed.Protection,originals,clock);
        var workflows=ports.Workflow.Registrations().ToDictionary(value=>value.CaseId,StringComparer.Ordinal);
        var mapped=new Dictionary<string,IReadOnlyList<(QualificationSourceWorkflow Workflow,JsonObject Original)>>(StringComparer.Ordinal);
        foreach(string caseId in installed.Scope.Cases)
        {
            if(caseId.StartsWith("secrets:",StringComparison.Ordinal)) continue;
            if(!installed.CaseOriginals.TryGetValue(caseId,out var operations)) return 2;
            var result=new List<(QualificationSourceWorkflow,JsonObject)>();
            foreach(var recipe in operations)
            {
                var original=JointPhaseRequestRecipe.Bind(recipe,request,installed.Scope);if(original is null) return 2;
                if(original["caseId"]?.GetValue<string>() is not { } id || !workflows.TryGetValue(id,out var workflow)) return 2;
                result.Add((workflow,original));
            }
            mapped.Add(caseId,result);
        }
        var publisher=new DaprJointPhaseNativeSourcePublisher(new IndependentDaprAuthority(transport,installed.IndependentSource,clock));
        IJointSecretsCaseExecutor? secrets=null;
        if(installed.SecretsRoles is { } roles && installed.SecretsCaller is { } caller && installed.SecretsOriginals is { } secretOriginals)
        {
            IndependentDaprAuthority Authority(IndependentDaprAuthorityBinding value)=>new(transport,value,clock);
            var execute=roles["executor"];var observe=roles["observer"];var enroll=roles["enrollment"];
            var trust=enroll.PublicAnchors.ToDictionary(pair=>pair.Key,pair=>new QualificationProofBinding(pair.Value.Identity,pair.Value.Version,System.Text.Encoding.ASCII.GetBytes(pair.Value.PublicKeyPem),pair.Value.Sha256));
            secrets=new JointSecretsCaseExecutor(installed.Scope,new InstalledQualificationExecutor(Authority(execute.ScopeAuthority),Authority(execute.Owner)),
                new InstalledQualificationObserver(Authority(observe.ScopeAuthority),Authority(observe.Owner)),new InstalledQualificationEnrollment(Authority(enroll.ScopeAuthority),Authority(enroll.Owner)),
                new QualificationProofVerifier(trust),new InstalledQualificationOriginalStore(Authority(installed.JointSecretsOriginals!)),caller,secretOriginals);
        }
        var command=new JointPhaseCommand(installed.Scope,new InstalledQualificationOriginalStore(new(transport,installed.JointCommandOriginals,clock)),mapped,publisher,installed.EvidenceDirectory,owner,secrets);
        return await command.ExecuteAsync(request,token).ConfigureAwait(false) is null?3:0;
    }
    internal static async Task<ECDsa> OpenSignerAsync(string privateFile,string publicFile,CancellationToken token)
    {
        if(!Path.IsPathFullyQualified(privateFile) || !Path.IsPathFullyQualified(publicFile)) throw new InvalidOperationException("ExactInstalledPhaseSignerLocators");
        foreach(string file in new[]{privateFile,publicFile}) { var info=new FileInfo(file);if(!info.Exists || info.LinkTarget is not null || info.Length is <=0 or >16384) throw new InvalidOperationException("BoundedExactPhaseSignerFile"); }
        var key=ECDsa.Create();try
        {
            key.ImportFromPem(await File.ReadAllTextAsync(privateFile,token).ConfigureAwait(false));using var anchor=ECDsa.Create();anchor.ImportFromPem(await File.ReadAllTextAsync(publicFile,token).ConfigureAwait(false));
            if(key.ExportParameters(false).Curve.Oid.Value!="1.2.840.10045.3.1.7" || !key.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(anchor.ExportSubjectPublicKeyInfo())) throw new InvalidOperationException("ExactIndependentlyEnrolledPhaseSigner");return key;
        }
        catch { key.Dispose();throw; }
    }
}
