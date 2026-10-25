using System.Security.Cryptography;
using Hexalith.Platform.Protection;
using Hexalith.EventStore.Server.Security;
using Dapr.Actors;
using Dapr.Actors.Client;
using Hexalith.EventStore.Client.Gateway;
using Microsoft.Extensions.Options;
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
        var protectionInstallation=installed.ProtectedCases?.Deserialize<QualificationProtectionInstallation>(CustodyBindingContract.Json);
        ICustodyOwnerStateAuthority Owner(CustodyLifecycleInstallation value)=>new CustodyOwnerAuthority(dapr,value.BindingName,value.TenantId,value.OwnerScope,value.EnrollmentJson,value.PublicKeyPem,new DaprCustodyOwnerCallSource(Authority(value.OwnerAuthority)));
        using var gatewayHttp=new HttpClient(new SocketsHttpHandler { AllowAutoRedirect=false,UseProxy=false }) { Timeout=TimeSpan.FromSeconds(30),BaseAddress=protectionInstallation?.EventStoreEndpoint };
        IEventStoreGatewayClient? gateway=null;
        if(protectionInstallation is { EventStoreEndpoint:{ IsAbsoluteUri:true,Scheme:"https" },GatewayCredentials:{ } credentials,ReadAuthority:{ } readAuthority,AuditScopes:{ } auditScopes })
        {
            var proofs=new QualificationGatewayReadProofs(credentials,Owner(readAuthority),auditScopes.Values.SelectMany(value=>value.NativeStreams).Distinct().ToArray(),TimeProvider.System);
            gateway=new EventStoreGatewayClient(gatewayHttp,Options.Create(new EventStoreGatewayClientOptions { BaseAddress=protectionInstallation.EventStoreEndpoint,UseWorkloadTransport=true,MaxStreamReadResponseBytes=1024*1024 }),proofs,proofs);
        }
        var audit=new QualificationEventStoreCaseAuditReader(gateway is null || protectionInstallation?.AuditScopes is null?null:new QualificationEventStoreAuditReader(Authority(installed.ExecutorOriginals),gateway,TimeProvider.System,protectionInstallation.AuditScopes));
        var sourceReaders=new Dictionary<string,IQualificationObservedOriginalReader>(StringComparer.Ordinal) { ["S1.reject.missing-config"]=new QualificationMissingConfigurationOriginalReader(executorOriginals,audit) };
        using var services=RuntimeServices(installed,dapr);
        if(installed.RuntimeCaseInputs is { } inputs && services?.GetService<IPlatformKeyInventoryAuthority>() is { } inventory)
        {
            var raw=new QualificationRuntimeSourceDriver(new QualificationInventoryActorRouter(),inventory,services.GetRequiredService<PlatformHmacService>(),services.GetRequiredService<TrustedEnvelopeAuthenticator>(),inputs);
            var runtimeConfiguration=SecretsRuntimeConfiguration.Load(installed.RuntimeConfigurationFile!)!;
            var replayCredential=PrivateReplayCredentialBinding.TryCapture(runtimeConfiguration.ReplayRegistrarReference,runtimeConfiguration.ReplayRegistrarCredentialReference,revision:1);
            var lookupRegistrar=replayCredential is null?null:new DaprReplayRegistrarAdapter(runtimeConfiguration,new DaprSecretsInvoker(dapr),TimeProvider.System,replayCredential);
            var envelopes=services.GetService<TrustedEnvelopeReplayVerifier>() is { } replay && installed.EnvelopeCaseIdentities is { } identities
                ?new QualificationEnvelopeCaseWorkflow(services.GetRequiredService<TrustedEnvelopeAuthenticator>(),replay,Authority(installed.ExecutorOriginals),identities,lookupRegistrar):null;
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
                if(envelopes is not null && workflow.CaseId is "S2.canonical-hmac" or "S2.byte-identical-replay")
                    sourceReaders[workflow.CaseId]=new QualificationEnvelopeOriginalReader(executorOriginals,audit,envelopes);
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
        if(services is not null && installed.TemporalCases is { } temporalInputs)
        {
            var temporal=new QualificationTemporalCaseWorkflow(services.GetRequiredService<TrustedEnvelopeAuthenticator>(),services.GetRequiredService<IPlatformHmacKeyProvider>(),services.GetRequiredService<IPlatformSigningProfileProvider>(),TimeProvider.System,executorOriginals,temporalInputs);
            var registrations=temporal.Registrations();workflows.AddRange(registrations);
            foreach(var registration in registrations) sourceReaders[registration.CaseId]=new QualificationTemporalOriginalReader(executorOriginals,audit);
        }
        if(services is not null && installed.InventoryRotation is { } rotation && services.GetService<IPlatformKeyInventoryAuthority>() is { } rotationAuthority)
        {
            var rotating=new QualificationInventoryRotationWorkflow(new QualificationInventoryActorRouter(),rotationAuthority,services.GetRequiredService<PlatformHmacService>(),executorOriginals,rotation,null);
            workflows.Add(rotating.Registration);sourceReaders[rotating.Registration.CaseId]=new QualificationInventoryRotationOriginalReader(executorOriginals,audit,rotating);
        }
        if(installed.LifecycleInstallation is { } lifecycleInstallation && installed.LifecycleCases is { } lifecycleInputs)
        {
            var callSource=new DaprCustodyAuthorizerClient(dapr,new Dictionary<string,string> { [DaprCustodyAuthorizerClient.LogicalRole]=lifecycleInstallation.AuthorizerAppId },lifecycleInstallation.AuthorizerCredentialReference);
            var lifecycleClient=new CustodyLifecycleClient(dapr,new Dictionary<string,string> { [CustodyLifecycleClient.LogicalRole]=lifecycleInstallation.BindingName },lifecycleInstallation.OwnerScope,lifecycleInstallation.EnrollmentJson,lifecycleInstallation.PublicKeyPem,callSource);
            var lifecycle=new QualificationLifecycleCaseWorkflow(lifecycleClient,executorOriginals,lifecycleInputs,installed.LifecycleRoots is { } rootReferences && installed.RootReadAuthority is { } rootAuthority?new DaprQualificationLifecycleRootSource(dapr,Owner(rootAuthority),rootReferences):null);
            var registrations=lifecycle.Registrations();workflows.AddRange(registrations);
            foreach(var registration in registrations) sourceReaders[registration.CaseId]=new QualificationLifecycleOriginalReader(executorOriginals,audit,lifecycle,lifecycleInputs);
        }
        if(services is not null && protectionInstallation?.ReplayCases is { } replayInputs)
        {
            var config=SecretsRuntimeConfiguration.Load(installed.RuntimeConfigurationFile!)!;
            var credential=PrivateReplayCredentialBinding.TryCapture(config.ReplayRegistrarReference,config.ReplayRegistrarCredentialReference,revision:1);
            if(credential is not null)
            {
                var registrar=new DaprReplayRegistrarAdapter(config,new DaprSecretsInvoker(dapr),TimeProvider.System,credential);
                var replayCases=new QualificationReplayCaseWorkflow(registrar,executorOriginals,replayInputs,TimeProvider.System);
                var registrations=replayCases.Registrations();workflows.AddRange(registrations);
                foreach(var registration in registrations) sourceReaders[registration.CaseId]=new QualificationReplayOriginalReader(executorOriginals,audit,replayCases);
            }
        }
        if(installed.DeliveryCases is { } deliveryInputs)
        {
            var delivery=new QualificationExportDeliveryWorkflow(executorOriginals,deliveryInputs);var registrations=delivery.Registrations();workflows.AddRange(registrations);
            foreach(var registration in registrations) sourceReaders[registration.CaseId]=new QualificationExportDeliveryOriginalReader(executorOriginals,audit,delivery);
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
        if(protectionInstallation is { GuardTarget:{ } target,GuardTargetAuthority:{ } targetOwner,GuardCellAuthorities:{ } cells,GuardReceiptAuthorities:{ } receipts,
            IndependentEvidenceComponent:{ } evidenceComponent,EvidenceResources:{ } resources,EvidenceAuthorities:{ } evidenceOwners,GuardArguments:{ } arguments,GuardDefinitions:{ } definitions,RevocationSubscription:{ } subscription }
            && services?.GetService<IDeletionCapabilityRevocationAuthenticator>() is { } revocation)
        {
            var installedCells=new CustodyGuardedStateInstallation(target,protectionInstallation.GuardTargetRevision,Owner(targetOwner),cells.ToDictionary(pair=>pair.Key,pair=>Owner(pair.Value)),receipts.ToDictionary(pair=>pair.Key,pair=>Owner(pair.Value)),TimeProvider.System);
            var evidence=new CustodyGovernanceGuardInstallation(dapr,evidenceComponent,resources,evidenceOwners.ToDictionary(pair=>pair.Key,pair=>Owner(pair.Value)),TimeProvider.System);
            var guarded=new DaprGuardedStateTransaction(dapr,TimeProvider.System,new GuardedStateTransactionAuthority(installedCells));
            var guard=new GovernanceScopeGuardOwner(guarded,TimeProvider.System,new GovernanceGuardAuthority(evidence));
            var proxies=ActorProxy.DefaultProxyFactory;
            var protection=new DaprDeletionProtectionOwner(target.TenantId,proxies,TimeProvider.System);
            var consumption=ActorProxy.Create<IDeletionConsumptionActor>(new ActorId(DeletionConsumptionActor.GetActorId(target.TenantId)),DeletionConsumptionActor.ActorTypeName);
            var signerActor=new QualificationSigningActorRouter();
            var subscriber=new DeletionCapabilityRevocationSubscriber(subscription,revocation,new DaprDeletionCapabilityCompromiseRegistrar(proxies),new EventStoreDeletionCapabilityGuardRevocationMirror(guard,TimeProvider.System),TimeProvider.System);
            var compiled=definitions.ToDictionary(pair=>pair.Key,pair=>QualificationGuardRecipeCatalog.Compile(pair.Key,pair.Value.TenantId,pair.Value.BatchId,pair.Value.EnrolledScopeSha256,arguments[pair.Key]),StringComparer.Ordinal);
            var guardedWorkflow=new QualificationGuardCaseWorkflow(guard,protection,consumption,signerActor,executorOriginals,compiled,subscriber);
            var registrations=guardedWorkflow.Registrations();workflows.AddRange(registrations);
            foreach(var registration in registrations) sourceReaders[registration.CaseId]=new QualificationGuardOriginalReader(guard,protection,signerActor,executorOriginals,audit,compiled);
        }
        var readers=workflows.ToDictionary(value=>value.CaseId,_=>(IReadOnlyList<string>)["External.SelectedNativeRecoveryControlReceiptHistory","External.CompleteInstalledLogAndTraceAccessAndCensus","External.NativeOutboxSnapshotAndAllHolderPhysicalCopyReadProtocols"],StringComparer.Ordinal);
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
