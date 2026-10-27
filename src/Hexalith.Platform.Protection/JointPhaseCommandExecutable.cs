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
    public static async Task<int> RunAsync(string? installationPath,CancellationToken token=default,bool finalizeOnly=false)
        =>await RunAsync(installationPath,Console.OpenStandardInput(),token,finalizeOnly).ConfigureAwait(false);
    internal static async Task<int> RunAsync(string? installationPath,Stream input,CancellationToken token,bool finalizeOnly,
        TimeSpan? startupTimeout=null,ISecretsCredentialBoundDaprInvoker? fixtureTransport=null,
        Func<JointPhaseCommandInstallation,JointPhaseOriginalStores,ECDsa,Func<JsonObject,CancellationToken,Task<bool>>,JointPhaseCommand>? fixtureCommand=null)
    {
        CancellationToken supervisor=token;long started=System.Diagnostics.Stopwatch.GetTimestamp();
        using var wholePhase=CancellationTokenSource.CreateLinkedTokenSource(supervisor);
        using var budget=CancellationTokenSource.CreateLinkedTokenSource(supervisor);budget.CancelAfter(startupTimeout??TimeSpan.FromSeconds(30));token=budget.Token;
        if(installationPath is null || !Path.IsPathFullyQualified(installationPath)) return 2;
        byte[] bytes=await BoundedRegularFile.ReadAsync(installationPath,1024*1024,token).ConfigureAwait(false);if(bytes.Length is 0 or >1024*1024) return 2;
        CustodyBindingContract.RejectDuplicateProperties(bytes);var installed=JsonSerializer.Deserialize<JointPhaseCommandInstallation>(bytes,CustodyBindingContract.Json);if(installed is null) return 2;
        if(installed.JointCommandOriginals is null || installed.JointSecretsOriginals is null) return 2;
        if(!finalizeOnly)
        {
            if(installed.CompletionTimeoutSeconds is not (>=1 and <=86400)) return 2;
            TimeSpan remaining=TimeSpan.FromSeconds(installed.CompletionTimeoutSeconds.Value)-System.Diagnostics.Stopwatch.GetElapsedTime(started);
            if(remaining<=TimeSpan.Zero) wholePhase.Cancel();else wholePhase.CancelAfter(remaining);
        }
        using var acquisition=CancellationTokenSource.CreateLinkedTokenSource(token,wholePhase.Token);token=acquisition.Token;
        using var owner=await OpenSignerAsync(installed.OwnerSigningKeyFile,installed.OwnerPublicKeyFile,token).ConfigureAwait(false);
        using var stdin=new MemoryStream();byte[] buffer=new byte[8192];int length;
        while((length=await input.ReadAsync(buffer,token).ConfigureAwait(false))>0) { if(stdin.Length+length>1024*1024) return 2;await stdin.WriteAsync(buffer.AsMemory(0,length),token).ConfigureAwait(false); }
        var request=SecretsProof.Parse(System.Text.Encoding.UTF8.GetString(stdin.ToArray()));if(request is null) return 2;
        installed=BindAcceptedRequest(installed,request);if(installed is null || installed.JointCommandOriginals is null || installed.JointSecretsOriginals is null) return 2;
        using var dapr=new DaprClientBuilder().Build();ISecretsCredentialBoundDaprInvoker transport=fixtureTransport??new DaprSecretsInvoker(dapr);var clock=TimeProvider.System;
        var stores=JointPhaseOriginalStores.Create(transport,installed.ExecutorOriginals,installed.JointSecretsOriginals,installed.JointCommandOriginals,clock);var originals=stores.Executor;
        if(installed.CohortAttempt is null || installed.CohortAttempt.BindingDigest!=installed.Scope.BindingDigest) return 2;
        var phaseAuthority=new DaprQualificationAttemptAuthority(transport,installed.CohortAttempt,clock);
        if(!await phaseAuthority.ValidatePhaseAsync(request,finalizeOnly,token).ConfigureAwait(false)) return 2;
        if(finalizeOnly)
        {
            var recovery=new JointPhaseMetadataFinalizer(installed.Scope,stores.Command,installed.EvidenceDirectory,owner,(value,ct)=>phaseAuthority.ValidatePhaseAsync(value,true,ct));
            try { return await recovery.FinalizeAsync(request,token).ConfigureAwait(false) is null?3:0; }
            catch(IOException) { return 3; } // Missing/unsafe partial case files remain lookup-required; this mode has no case-effect ports.
        }
        JointPhaseCommand command;
        if(fixtureCommand is not null) command=fixtureCommand(installed,stores,owner,(value,ct)=>phaseAuthority.ValidatePhaseAsync(value,false,ct));
        else
        {
        var runtime=SecretsRuntimeConfiguration.Parse(System.Text.Encoding.UTF8.GetString(await BoundedRegularFile.ReadAsync(installed.RuntimeConfigurationFile,1024*1024,token).ConfigureAwait(false)));if(runtime is null) return 2;
        var workflows=CreateSelectedWorkflows(installed.Scope,dapr,runtime,installed.Protection,originals,clock);
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
        IJointPhaseNativeSourcePublisher? publisher=null;
        if(installed.Scope.Cases.Any(id=>!id.StartsWith("secrets:",StringComparison.Ordinal)))
        {
            if(installed.IndependentSource is null) return 2;
            publisher=new DaprJointPhaseNativeSourcePublisher(new IndependentDaprAuthority(transport,installed.IndependentSource,clock));
        }
        IJointSecretsCaseExecutor? secrets=null;
        if(installed.SecretsRoles is { } roles && installed.SecretsCaller is { } caller && installed.SecretsOriginals is { } secretOriginals)
        {
            IndependentDaprAuthority Authority(IndependentDaprAuthorityBinding value)=>new(transport,value,clock);
            var execute=roles["executor"];var observe=roles["observer"];var enroll=roles["enrollment"];
            var trust=enroll.PublicAnchors.ToDictionary(pair=>pair.Key,pair=>new QualificationProofBinding(pair.Value.Identity,pair.Value.Version,System.Text.Encoding.ASCII.GetBytes(pair.Value.PublicKeyPem),pair.Value.Sha256));
            secrets=new JointSecretsCaseExecutor(installed.Scope,new InstalledQualificationExecutor(Authority(execute.ScopeAuthority),Authority(execute.Owner)),
                new InstalledQualificationObserver(Authority(observe.ScopeAuthority),Authority(observe.Owner)),new InstalledQualificationEnrollment(Authority(enroll.ScopeAuthority),Authority(enroll.Owner)),
                new QualificationProofVerifier(trust),stores.Secrets,caller,secretOriginals);
        }
        command=new JointPhaseCommand(installed.Scope,stores.Command,mapped,publisher,installed.EvidenceDirectory,owner,secrets,stores.Executor,(value,ct)=>phaseAuthority.ValidatePhaseAsync(value,false,ct));
        }
        token.ThrowIfCancellationRequested();
        return await command.ExecuteAsync(request,wholePhase.Token).ConfigureAwait(false) is null?3:0;
    }
    // The v2 installation is pinned before the profile and accepted binding exist. Its complete immutable
    // pre-binding facts are retained; only the final digest comes from the request, then the independent
    // current authority authenticates that exact retained phase before any normal/finalization workflow.
    internal static JointPhaseCommandInstallation? BindAcceptedRequest(JointPhaseCommandInstallation installed,JsonObject request)
    {
        if(installed.BindingSchema=="Platform.JointPhaseInstallation.v1")
            return installed.CohortAttempt?.BindingDigest==installed.Scope.BindingDigest && request["bindingDigest"]?.GetValue<string>()==installed.Scope.BindingDigest?installed:null;
        if(installed.BindingSchema!="Platform.JointPhaseInstallation.v2" || installed.Scope.BindingDigest!="" || installed.CohortAttempt is not { BindingDigest:"" } authority
            || request["bindingDigest"]?.GetValue<string>() is not { Length:64 } final || !final.All(char.IsAsciiHexDigitLower)
            || JointPhaseRequestRecipe.Phase(request)!=installed.Scope.Phase
            || request["cases"] is not JsonArray cases || !cases.Select(value=>value!.GetValue<string>()).SequenceEqual(installed.Scope.Cases)) return null;
        return installed with { Scope=installed.Scope with { BindingDigest=final },CohortAttempt=authority with { BindingDigest=final,PlatformCommit=installed.Scope.PlatformCommit } };
    }
    internal static IReadOnlyDictionary<string,QualificationSourceWorkflow> CreateSelectedWorkflows(JointPhaseCommandScope scope,DaprClient dapr,SecretsRuntimeConfiguration runtime,QualificationProtectionInstallation? protection,IQualificationOriginalStore originals,TimeProvider clock)
    {
        if(scope.Cases.All(id=>id.StartsWith("secrets:",StringComparison.Ordinal))) return new Dictionary<string,QualificationSourceWorkflow>(StringComparer.Ordinal);
        if(protection is null) throw new InvalidOperationException("SelectedNativeGuardWorkflowInstallation");
        return QualificationGuardProductionComposition.Create(dapr,runtime,protection,originals,clock).Workflow.Registrations().ToDictionary(value=>value.CaseId,StringComparer.Ordinal);
    }
    internal static async Task<ECDsa> OpenSignerAsync(string privateFile,string publicFile,CancellationToken token)
    {
        if(!Path.IsPathFullyQualified(privateFile) || !Path.IsPathFullyQualified(publicFile)) throw new InvalidOperationException("ExactInstalledPhaseSignerLocators");
        var key=ECDsa.Create();try
        {
            using var budget=CancellationTokenSource.CreateLinkedTokenSource(token);budget.CancelAfter(TimeSpan.FromSeconds(30));token=budget.Token;
            await BoundedRegularFile.UseUtf8SpanAsync(privateFile,16384,value=>{ key.ImportFromPem(value);return true; },token).ConfigureAwait(false);
            using var anchor=ECDsa.Create();await BoundedRegularFile.UseUtf8SpanAsync(publicFile,16384,value=>{ anchor.ImportFromPem(value);return true; },token).ConfigureAwait(false);
            if(key.ExportParameters(false).Curve.Oid.Value!="1.2.840.10045.3.1.7" || !key.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(anchor.ExportSubjectPublicKeyInfo())) throw new InvalidOperationException("ExactIndependentlyEnrolledPhaseSigner");return key;
        }
        catch { key.Dispose();throw; }
    }
}
