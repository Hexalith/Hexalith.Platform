using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Actors.Client;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Platform.Protection;

/// <summary>Explicit configuration-bound production host composition. Missing installed inputs refuse before any native or Dapr effect.</summary>
public static class ErasureRootHostComposition
{
    /// <summary>Activates the actual engine only for EventStore:PayloadProtection:Enabled with a complete hash-bound public installation.</summary>
    public static IServiceCollection AddPlatformErasureRootProtection(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        if (!configuration.GetValue<bool>("EventStore:PayloadProtection:Enabled"))
        { throw new InvalidOperationException("DependencyNotAvailable:EXT-PROTECTION-1:ExplicitProtectionInstallationRequired"); }
        ErasureRootHostInstallation installation;
        SecretsRuntimeConfiguration inventory;
        try
        {
            byte[] bytes = ReadPinned(configuration["EventStore:PayloadProtection:InstallationFile"]!, configuration["EventStore:PayloadProtection:InstallationSha256"]!);
            installation = JsonSerializer.Deserialize<ErasureRootHostInstallation>(bytes, ErasureRootCustody.Json) ?? throw new InvalidOperationException();
            _ = ReadPinned(installation.CopyPolicyFile, installation.CopyPolicySha256);
            _ = ReadPinned(installation.InventoryConfigurationFile, installation.InventoryConfigurationSha256);
            inventory = SecretsRuntimeConfiguration.Load(installation.InventoryConfigurationFile) ?? throw new InvalidOperationException();
            if (installation.Native.NativeKeys.Count == 0 || !installation.Native.NativeKeys.Keys.Order(StringComparer.Ordinal).SequenceEqual(installation.RegistryEpochs.Keys.Order(StringComparer.Ordinal))
                || installation.RegistryEpochs.Values.Any(string.IsNullOrWhiteSpace) || installation.CapabilityTrust.Length == 0
                || string.IsNullOrWhiteSpace(inventory.CurrentInventoryAuthorityReference)
                || !Uri.TryCreate(installation.Endpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme != "https") { throw new InvalidOperationException(); }
            foreach (string tenant in installation.Native.NativeKeys.Keys)
            {
                if (!installation.CapabilityTrust.Any(entry => entry.TenantId == tenant && entry.KeyFamily == "DeletionBatchCapabilitySigningKey")) { throw new InvalidOperationException(); }
            }
            // Parsing every independent public binding validates references and P-256 trust before DI changes; no transport is invoked.
            foreach (var binding in new[] { installation.PrivateAuthority, installation.SourceAuthority, installation.GuardAuthority,
                installation.RegistrarAuthority, installation.EvidenceAuthority })
            {
                _ = new IndependentDaprAuthority(new DaprSecretsInvoker(null!), binding, TimeProvider.System);
            }
            using var nativeHttp = new HttpClient { BaseAddress = endpoint, Timeout = TimeSpan.FromSeconds(30) };
            _ = new OpenBaoErasureRootStore(nativeHttp, installation.Native, installation.CredentialReference);
        }
        catch (Exception)
        {
            throw new InvalidOperationException("DependencyNotAvailable:EXT-PROTECTION-1:CompletePinnedErasureRootInstallationRequired");
        }
        services.AddSingleton(installation);
        services.AddSingleton(inventory);
        services.AddSingleton<IDeletionCapabilityRevocationAuthenticator>(provider => new ErasureRootRecordingRevocationAuthenticator(
            new DaprRevocationAuthenticatorAdapter(inventory, new DaprSecretsInvoker(provider.GetRequiredService<DaprClient>()), TimeProvider.System), Authority(provider, installation.RegistrarAuthority)));
        services.AddHttpClient("hexalith-erasure-root", client => { client.BaseAddress = new(installation.Endpoint); client.Timeout = TimeSpan.FromSeconds(30); })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false });
        bool observer = configuration.GetValue<bool>("EventStore:PayloadProtection:QualificationObserver");
        if (!observer && installation.QualificationNativeControls is { } controls)
        { services.AddSingleton(provider => new ProtectionQualificationFaults(provider.GetRequiredService<DaprClient>(), controls.Cell, controls.EnrollmentId, controls.Operations.ToHashSet(StringComparer.Ordinal))); }
        services.AddSingleton<IErasureRootStore>(provider =>
        {
            IErasureRootStore native = new OpenBaoErasureRootStore(provider.GetRequiredService<IHttpClientFactory>().CreateClient("hexalith-erasure-root"), installation.Native, installation.CredentialReference);
            return !observer && installation.QualificationNativeControls is not null ? new ProtectionQualificationControlledErasureRootStore(native, provider.GetRequiredService<ProtectionQualificationFaults>()) : native;
        });
        services.AddSingleton<IErasureRootAuthorization>(provider => new DaprErasureRootAuthorization(Authority(provider, installation.PrivateAuthority), provider.GetRequiredService<IErasureRootStore>(), installation.Native));
        services.AddSingleton<ErasureRootCustody>(provider => new(provider.GetRequiredService<IErasureRootStore>(), installation.Native, provider.GetRequiredService<IErasureRootAuthorization>()));
        services.AddSingleton<IErasureRootOccurrenceSource>(provider => new DaprErasureRootOccurrenceSource(Authority(provider, installation.SourceAuthority)));
        services.AddSingleton<ErasureRootOccurrenceBindings>();
        services.AddSingleton<IInteractionProtectionBindingProvider>(provider => provider.GetRequiredService<ErasureRootOccurrenceBindings>());
        services.AddSingleton<IInteractionProtectionBindingSource>(provider => provider.GetRequiredService<ErasureRootOccurrenceBindings>());
        services.AddSingleton<IInteractionOccurrenceRegistry>(provider => new DaprInteractionOccurrenceRegistry(provider.GetRequiredService<IActorProxyFactory>(), TimeProvider.System));
        services.AddSingleton<ErasureRootDigestKeyResolver>(provider => new(provider.GetRequiredService<DaprClient>(), new DaprErasureRootInventory(provider.GetRequiredService<IActorProxyFactory>()),
            new DaprInventoryAuthorityAdapter(inventory, new DaprSecretsInvoker(provider.GetRequiredService<DaprClient>()))));
        services.AddSingleton<ErasureRootOccurrenceKeyProvider>();
        services.AddHostedService<ErasureRootHolderCompletionWorker>();
        services.AddSingleton<IInteractionOccurrenceKeyProvider>(provider => provider.GetRequiredService<ErasureRootOccurrenceKeyProvider>());
        services.AddSingleton<IAtomicDeletionManifestProvider, ErasureRootManifestProvider>();
        services.AddSingleton<IInteractionOccurrenceAuthority>(provider => new ErasureRootOccurrenceAuthority(provider.GetRequiredService<ErasureRootCustody>(), provider.GetRequiredService<ErasureRootOccurrenceBindings>(),
            provider.GetRequiredService<IErasureRootOccurrenceSource>(), provider.GetRequiredService<IErasureRootAuthorization>(), installation.RegistryEpochs));
        services.AddSingleton<IDeletionConsumptionAuthority>(provider => new PublishedTrustDeletionConsumptionAuthority(installation.CapabilityTrust,
            Guard(provider), new DaprErasureRootEvidenceJournal(Authority(provider, installation.EvidenceAuthority)),
            new DaprErasureRootRevocationEvidence(Authority(provider, installation.RegistrarAuthority)), installation.Native.NativeKeys.Keys.Select(tenant => (IDeletionConsumptionOwnerStateAuthority)
                new ErasureRootDeletionOwnerAuthority(provider.GetRequiredService<ErasureRootCustody>(), provider.GetRequiredService<IErasureRootAuthorization>(), tenant)).ToArray()));
        services.AddSingleton<ICustodyKeyLifecycleProvider>(provider => provider.GetRequiredService<ErasureRootCustody>());
        return services.AddInteractionPayloadProtection();
        IGovernanceScopeGuard Guard(IServiceProvider provider)
        {
            IGovernanceScopeGuard actual = new DaprErasureRootGovernanceGuard(Authority(provider, installation.GuardAuthority));
            return !observer && installation.QualificationNativeControls is not null ? new ProtectionQualificationControlledGuard(actual, provider.GetRequiredService<ProtectionQualificationFaults>()) : actual;
        }
    }

    private static IndependentDaprAuthority Authority(IServiceProvider provider, IndependentDaprAuthorityBinding binding)
        => new(new DaprSecretsInvoker(provider.GetRequiredService<DaprClient>()), binding, TimeProvider.System);
    private static byte[] ReadPinned(string path, string digest)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || digest is not { Length: 64 } || !File.Exists(path)
            || new FileInfo(path).LinkTarget is not null) { throw new InvalidOperationException(); }
        byte[] bytes = File.ReadAllBytes(path);
        return Convert.ToHexString(SHA256.HashData(bytes)).Equals(digest, StringComparison.OrdinalIgnoreCase) ? bytes : throw new InvalidOperationException();
    }
}
