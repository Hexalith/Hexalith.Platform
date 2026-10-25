using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;
using Hexalith.EventStore.Contracts.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Shipped executable composition for the closed local drivers and concrete measured projector. Unselected source/read/control protocols keep admission unavailable.</summary>
public static class QualificationOwnerDriverExecutable
{
    /// <summary>Runs only with explicit independently installed bindings. Missing ordinary source and selected recovery protocols remain named blockers before effects.</summary>
    public static async Task<int> RunAsync(string role,string? installationPath)
    {
        if(string.IsNullOrWhiteSpace(installationPath)) return 2;
        byte[] bytes=await File.ReadAllBytesAsync(installationPath).ConfigureAwait(false);
        if(bytes.Length is 0 or >1024*1024) return 2;
        CustodyBindingContract.RejectDuplicateProperties(bytes);
        var installed=JsonSerializer.Deserialize<QualificationOwnerDriverInstallation>(bytes,CustodyBindingContract.Json);
        if(installed is null || installed.Role.Role!=role || !Path.IsPathFullyQualified(installed.RoleSigningKeyFile) || !Path.IsPathFullyQualified(installed.ReceivingInstallationPath)
            || installed.ExecutorOriginals.AppId==installed.IndependentSource.AppId || installed.Role.Identity==installed.ObserverTrust.Identity && role!="observer") return 2;
        using var signer=ECDsa.Create();signer.ImportFromPem(await File.ReadAllTextAsync(installed.RoleSigningKeyFile).ConfigureAwait(false));
        using var dapr=new Dapr.Client.DaprClientBuilder().Build();
        IndependentDaprAuthority Authority(IndependentDaprAuthorityBinding binding)=>new(new DaprSecretsInvoker(dapr),binding,TimeProvider.System);
        var executorOriginals=new InstalledQualificationOriginalStore(Authority(installed.ExecutorOriginals));
        var roleOriginals=new InstalledQualificationOriginalStore(Authority(installed.RoleOriginals));
        var source=new DaprQualificationObservedOriginalReader(Authority(installed.IndependentSource));
        var recovery=new UnselectedRecoveryController();
        var observerTrust=new QualificationProofBinding(installed.ObserverTrust.Identity,installed.ObserverTrust.Version,Encoding.ASCII.GetBytes(installed.ObserverTrust.PublicKeyPem),installed.ObserverTrust.Sha256);
        var workflows=new List<QualificationSourceWorkflow> { new QualificationMissingConfigurationWorkflow(executorOriginals).Registration };
        var audit=new UnselectedAuditReader();
        var sourceReaders=new Dictionary<string,IQualificationObservedOriginalReader>(StringComparer.Ordinal) { ["S1.reject.missing-config"]=new QualificationMissingConfigurationOriginalReader(executorOriginals,audit) };
        using var services=RuntimeServices(installed,dapr);
        if(installed.RuntimeCaseInputs is { } inputs && services?.GetService<IPlatformKeyInventoryAuthority>() is { } inventory)
        {
            var raw=new QualificationRuntimeSourceDriver(new QualificationInventoryActorRouter(),inventory,services.GetRequiredService<PlatformHmacService>(),services.GetRequiredService<TrustedEnvelopeAuthenticator>(),inputs);
            var envelopes=services.GetService<TrustedEnvelopeReplayVerifier>() is { } replay && installed.EnvelopeCaseIdentities is { } identities
                ?new QualificationEnvelopeCaseWorkflow(services.GetRequiredService<TrustedEnvelopeAuthenticator>(),replay,Authority(installed.ExecutorOriginals),identities):null;
            var capability=services.GetService<IDeletionCapabilitySigningTrustProvider>() is { } trust && installed.CapabilityCasePayloads is { } payloads
                ?new QualificationCapabilityCaseWorkflow(new QualificationSigningActorRouter(),trust,executorOriginals,payloads):null;
            var ids=inputs.Keys.Concat(installed.EnvelopeCaseIdentities?.Keys??Enumerable.Empty<string>()).Concat(installed.CapabilityCasePayloads?.Keys??Enumerable.Empty<string>()).Distinct(StringComparer.Ordinal).ToArray();
            var runtimeWorkflows=QualificationRuntimeWorkflowCatalog.Create(executorOriginals,ids,raw,envelopes,capability);
            workflows.AddRange(runtimeWorkflows);
            var inventoryKeys=inputs.Where(pair=>pair.Key.StartsWith("S1.inventory.",StringComparison.Ordinal)).ToDictionary(pair=>pair.Key,pair=>pair.Value.GetProperty("key").Deserialize<PlatformKeyVersion>(CustodyBindingContract.Json)!,StringComparer.Ordinal);
            var inventoryReader=new QualificationInventoryOriginalReader(new QualificationInventoryActorRouter(),inventory,executorOriginals,audit,inventoryKeys);
            foreach(var workflow in runtimeWorkflows)
            {
                if(workflow.CaseId.StartsWith("S1.inventory.",StringComparison.Ordinal)) sourceReaders[workflow.CaseId]=inventoryReader;
                if(envelopes is not null && (workflow.CaseId.StartsWith("S2.substitution.",StringComparison.Ordinal) || workflow.CaseId.StartsWith("S2.replay-reject.",StringComparison.Ordinal)))
                    sourceReaders[workflow.CaseId]=new QualificationDeniedOriginalReader(executorOriginals,audit,envelopes.ReadVerifyAsync);
                if(capability is not null && workflow.CaseId.StartsWith("S4.capability-substitution.",StringComparison.Ordinal))
                    sourceReaders[workflow.CaseId]=new QualificationDeniedOriginalReader(executorOriginals,audit,capability.ReadVerifyAsync);
            }
        }
        if(services?.GetService<IIndependentDecisionAuthority>() is { } decisionAuthority && installed.DecisionCaseInputs is { } decisionInputs)
        {
            var decisions=new QualificationDecisionCaseWorkflow(decisionAuthority,TimeProvider.System,decisionInputs);
            var registrations=decisions.Registrations(executorOriginals);workflows.AddRange(registrations);
            foreach(var registration in registrations) sourceReaders[registration.CaseId]=new QualificationDecisionOriginalReader(executorOriginals,audit,decisions);
        }
        if(services is not null)
        {
            if(installed.ProviderCredential is { } credential && services.GetService<IPlatformKeyInventoryAuthority>() is { } authority)
            {
                var provider=new QualificationProviderCredentialWorkflow(dapr,new QualificationInventoryActorRouter(),authority,credential);
                workflows.Add(provider.Registration(executorOriginals));sourceReaders["S1.exact-provider-credential"]=new QualificationResolutionOriginalReader(executorOriginals,audit,provider.ResolveAsync);
            }
            if(installed.HmacResolutionCases is { } cases)
            {
                var resolution=new QualificationHmacResolutionWorkflow(services.GetRequiredService<IPlatformHmacKeyProvider>(),services.GetRequiredService<IPlatformSigningProfileProvider>(),cases);
                var registrations=resolution.Registrations(executorOriginals);workflows.AddRange(registrations);
                foreach(var registration in registrations) sourceReaders[registration.CaseId]=new QualificationResolutionOriginalReader(executorOriginals,audit,resolution.ResolveAsync);
            }
            var configuration=SecretsRuntimeConfiguration.Load(installed.RuntimeConfigurationFile!)!;
            var missingReference=new QualificationMissingReferenceWorkflow(configuration,new DaprSecretsInvoker(dapr));
            workflows.Add(missingReference.Registration(executorOriginals));sourceReaders["S1.reject.missing-reference"]=new QualificationResolutionOriginalReader(executorOriginals,audit,missingReference.VerifyAsync);
        }
        if(installed.ExportCases is { } exports)
        {
            IExportQualificationMaterialSource material=new UnselectedExportMaterial();IExportQualificationTrustSource trust=new UnselectedExportTrust();
            if(installed.ExportMaterialProfiles is { } profiles && services?.GetService<IPlatformKeyInventoryAuthority>() is { } authority)
            {
                var actor=new QualificationInventoryActorRouter();trust=new DaprExportQualificationTrustSource(dapr,actor,authority,TimeProvider.System,profiles);
                material=new DaprExportQualificationMaterialSource(dapr,actor,authority,trust,TimeProvider.System,profiles);
            }
            var export=new QualificationExportCaseWorkflow(new ExportQualificationEngine(material,trust),executorOriginals,exports);
            var registrations=export.Registrations();workflows.AddRange(registrations);
            foreach(var registration in registrations) sourceReaders[registration.CaseId]=new QualificationExportOriginalReader(executorOriginals,audit,export);
        }
        var readers=workflows.ToDictionary(value=>value.CaseId,_=>(IReadOnlyList<string>)["Source.SelectedNativeCaseAuditAndLeakageReader"],StringComparer.Ordinal);
        var drivers=QualificationDriverComposition.Create(workflows,executorOriginals,recovery,source,observerTrust,readers);
        return await QualificationOwnerApplication.RunAsync(installed.ReceivingInstallationPath,installed.Role,signer,roleOriginals,drivers,independentSourceReader:new QualificationCompositeOriginalReader(sourceReaders)).ConfigureAwait(false);
    }
    private static ServiceProvider? RuntimeServices(QualificationOwnerDriverInstallation installed,Dapr.Client.DaprClient dapr)
    {
        if(installed.RuntimeConfigurationFile is null) return null;
        var configuration=SecretsRuntimeConfiguration.Load(installed.RuntimeConfigurationFile);
        if(configuration is null) throw new InvalidOperationException("Explicit runtime configuration required.");
        var services=new ServiceCollection();services.AddSingleton(dapr);services.AddSingleton(TimeProvider.System);
        var invoker=new DaprSecretsInvoker(dapr);
        if(services.AddExplicitHmac(configuration,invoker) is { } missing) throw new InvalidOperationException(missing);
        services.AddExplicitSecretsRuntime(configuration,invoker);_=services.AddPrivateReplay(configuration,invoker);
        return services.BuildServiceProvider();
    }
    private sealed class UnselectedAuditReader:IQualificationNativeCaseAuditReader
    {
        public Task<QualificationNativeCaseAudit?> ReadAsync(JsonObject original,CancellationToken cancellationToken)=>Task.FromResult<QualificationNativeCaseAudit?>(null);
    }
    private sealed class UnselectedExportMaterial:IExportQualificationMaterialSource
    {
        public Task<IReadOnlyList<string>> PreflightAsync(ExportQualificationInput input,ExportQualificationMaterialUse use,CancellationToken token)=>Task.FromResult<IReadOnlyList<string>>(["External.ExactDedicatedExportComponentKeyVersionFieldCodecAndCurrentAuthorityProfile","External.NativeExportHolderAdmissionAndCompleteClosureProtocol"]);
        public Task<ExportQualificationMaterial?> ResolveAsync(ExportQualificationInput input,ExportQualificationMaterialUse use,CancellationToken token)=>Task.FromResult<ExportQualificationMaterial?>(null);
    }
    private sealed class UnselectedExportTrust:IExportQualificationTrustSource
    {
        public Task<ExportQualificationTrust?> ResolveAsync(ExportQualificationInput input,CancellationToken token)=>Task.FromResult<ExportQualificationTrust?>(null);
    }
    // The installation has not selected a process controller or a native restore proof protocol. No claimed control can be manufactured from the caller's fields.
    private sealed class UnselectedRecoveryController:IQualificationRecoveryController
    {
        public Task<IReadOnlyList<string>> PreflightAsync(System.Text.Json.Nodes.JsonObject original,CancellationToken token)=>Task.FromResult<IReadOnlyList<string>>(["External.IsolatedProcessRestartControllerProtocol","External.NativeRestoreControllerAndIncarnationProofProtocol"]);
        public Task<QualificationRecoveryControlOriginal?> ExecuteAsync(string operation,System.Text.Json.Nodes.JsonObject original,CancellationToken token)=>Task.FromResult<QualificationRecoveryControlOriginal?>(null);
        public Task<QualificationRecoveryControlOriginal?> LookupAsync(string operation,System.Text.Json.Nodes.JsonObject original,CancellationToken token)=>Task.FromResult<QualificationRecoveryControlOriginal?>(null);
        public bool Verify(QualificationRecoveryControlOriginal original,System.Text.Json.Nodes.JsonObject request)=>false;
    }
}
