using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
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
        => AddPlatformErasureRootProtection(services, configuration, null);
    internal static IServiceCollection AddPlatformErasureRootProtection(IServiceCollection services, IConfiguration configuration, Action? acceptedInventoryAcquired)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        if (!configuration.GetValue<bool>("EventStore:PayloadProtection:Enabled"))
        { throw new InvalidOperationException("DependencyNotAvailable:EXT-PROTECTION-1:ExplicitProtectionInstallationRequired"); }
        ErasureRootHostInstallation installation;
        SecretsRuntimeConfiguration inventory;
        try
        {
            byte[] bytes = ReadPinned(configuration["EventStore:PayloadProtection:InstallationFile"]!, configuration["EventStore:PayloadProtection:InstallationSha256"]!, 8 * 1024 * 1024);
            installation = JsonSerializer.Deserialize<ErasureRootHostInstallation>(bytes, ErasureRootCustody.Json) ?? throw new InvalidOperationException();
            _ = ReadPinned(installation.CopyPolicyFile, installation.CopyPolicySha256, 1024 * 1024);
            byte[] inventoryBytes = ReadPinned(installation.InventoryConfigurationFile, installation.InventoryConfigurationSha256, 1024 * 1024);
            acceptedInventoryAcquired?.Invoke();
            if (inventoryBytes.Length > 1024 * 1024) { throw new InvalidOperationException(); }
            inventory = SecretsRuntimeConfiguration.Parse(new UTF8Encoding(false, true).GetString(inventoryBytes)) ?? throw new InvalidOperationException();
            if (installation.Native.NativeKeys.Count == 0 || !installation.Native.NativeKeys.Keys.Order(StringComparer.Ordinal).SequenceEqual(installation.RegistryEpochs.Keys.Order(StringComparer.Ordinal))
                || installation.RegistryEpochs.Values.Any(string.IsNullOrWhiteSpace) || installation.CapabilityTrust.Length == 0
                || string.IsNullOrWhiteSpace(inventory.CurrentInventoryAuthorityReference)
                || !Uri.TryCreate(installation.Endpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme != "https") { throw new InvalidOperationException(); }
            ValidateCapabilityTrust(installation);
            foreach (string tenant in installation.Native.NativeKeys.Keys)
            {
                if (!installation.CapabilityTrust.Any(entry => entry.TenantId == tenant && entry.KeyFamily == "DeletionBatchCapabilitySigningKey")) { throw new InvalidOperationException(); }
            }
            // Parsing every independent public binding validates references and P-256 trust before DI changes; no transport is invoked.
            var tenantBindings = TenantBindings(installation);
            var credentials = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var tenant in tenantBindings)
            {
                foreach (var binding in new[] { tenant.Value.PrivateAuthority, tenant.Value.SourceAuthority, tenant.Value.GuardAuthority, tenant.Value.RegistrarAuthority, tenant.Value.EvidenceAuthority })
                {
                    _ = new IndependentDaprAuthority(new DaprSecretsInvoker(null!), binding, TimeProvider.System);
                    using var validation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    byte[] fingerprint = IndependentAuthorityCredential.ReadFingerprintAsync(binding.CredentialReference, validation.Token).GetAwaiter().GetResult()
                        ?? throw new InvalidOperationException("Current mounted private authority credential required.");
                    string credentialIdentity = Convert.ToHexString(fingerprint);
                    if (credentials.TryGetValue(credentialIdentity, out var owner) && owner != tenant.Key) { throw new InvalidOperationException("Tenant credentials must be independently enrolled."); }
                    credentials[credentialIdentity] = tenant.Key;
                }
            }
            using var nativeValidation = new OpenBaoErasureRootStore(endpoint, installation.Native, installation.CredentialReference);
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(installation.CredentialReference[4..])))
            { throw new InvalidOperationException("Current native bearer required."); }
        }
        catch (Exception)
        {
            throw new InvalidOperationException("DependencyNotAvailable:EXT-PROTECTION-1:CompletePinnedErasureRootInstallationRequired");
        }
        services.AddDaprClient(builder => ErasureRootDaprTransport.Configure(builder));
        services.AddSingleton(installation);
        services.AddSingleton(inventory);
        services.AddSingleton<IDeletionCapabilityRevocationAuthenticator>(provider => new ErasureRootRecordingRevocationAuthenticator(
            new DaprRevocationAuthenticatorAdapter(inventory, new DaprSecretsInvoker(provider.GetRequiredService<DaprClient>()), TimeProvider.System), Authority(provider, installation, value => value.RegistrarAuthority)));
        services.AddHttpClient("hexalith-erasure-root", client => { client.BaseAddress = new(installation.Endpoint); client.Timeout = TimeSpan.FromSeconds(30); })
            .ConfigurePrimaryHttpMessageHandler(() => OpenBaoErasureRootStore.CreatePinnedHandler(installation.Native.TrustedCaCertificatePem));
        bool observer = configuration.GetValue<bool>("EventStore:PayloadProtection:QualificationObserver");
        if (!observer && installation.QualificationNativeControls is { } controls)
        { services.AddSingleton(provider => new ProtectionQualificationFaults(provider.GetRequiredService<DaprClient>(), controls.Cell, controls.EnrollmentId, controls.Operations.ToHashSet(StringComparer.Ordinal))); }
        services.AddSingleton<IErasureRootStore>(provider =>
        {
            IErasureRootStore native = new OpenBaoErasureRootStore(new Uri(installation.Endpoint), installation.Native, installation.CredentialReference);
            return !observer && installation.QualificationNativeControls is not null ? new ProtectionQualificationControlledErasureRootStore(native, provider.GetRequiredService<ProtectionQualificationFaults>()) : native;
        });
        services.AddSingleton<IErasureRootAuthorization>(provider => new DaprErasureRootAuthorization(Authority(provider, installation, value => value.PrivateAuthority), provider.GetRequiredService<IErasureRootStore>(), installation.Native));
        services.AddSingleton<ErasureRootCustody>(provider =>
        {
            var custody = new ErasureRootCustody(provider.GetRequiredService<IErasureRootStore>(), installation.Native, provider.GetRequiredService<IErasureRootAuthorization>());
            custody.RequireInstalledOwners(installation.Native.NativeKeys.Keys.SelectMany(tenant => new[]
            {
                DeletionConsumptionActor.GetOwnerScope(tenant), ErasureRootOccurrenceAuthority.Scope(tenant), ErasureRootTenantLifecycleAuthority.Scope(tenant)
            }));
            return custody;
        });
        services.AddSingleton<IErasureRootOccurrenceSource>(provider => new DaprErasureRootOccurrenceSource(Authority(provider, installation, value => value.SourceAuthority)));
        services.AddSingleton<ErasureRootOccurrenceBindings>();
        services.AddSingleton<IInteractionProtectionBindingProvider>(provider => provider.GetRequiredService<ErasureRootOccurrenceBindings>());
        services.AddSingleton<IInteractionProtectionBindingSource>(provider => provider.GetRequiredService<ErasureRootOccurrenceBindings>());
        services.AddSingleton<IInteractionOccurrenceRegistry>(provider => new DaprInteractionOccurrenceRegistry(provider.GetRequiredService<IActorProxyFactory>(), TimeProvider.System));
        services.AddSingleton<ErasureRootDigestKeyResolver>(provider => new(provider.GetRequiredService<DaprClient>(), new DaprErasureRootInventory(provider.GetRequiredService<IActorProxyFactory>()),
            new DaprInventoryAuthorityAdapter(inventory, new DaprSecretsInvoker(provider.GetRequiredService<DaprClient>()))));
        services.AddSingleton<ErasureRootOccurrenceKeyProvider>();
        if (!observer) { services.AddHostedService<ErasureRootHolderCompletionWorker>(); }
        services.AddSingleton<IInteractionOccurrenceKeyProvider>(provider => provider.GetRequiredService<ErasureRootOccurrenceKeyProvider>());
        services.AddSingleton<IAtomicDeletionManifestProvider, ErasureRootManifestProvider>();
        services.AddSingleton<IInteractionOccurrenceAuthority>(provider => new ErasureRootOccurrenceAuthority(provider.GetRequiredService<ErasureRootCustody>(), provider.GetRequiredService<ErasureRootOccurrenceBindings>(),
            provider.GetRequiredService<IErasureRootOccurrenceSource>(), provider.GetRequiredService<IErasureRootAuthorization>(), installation.RegistryEpochs));
        services.AddSingleton<IDeletionConsumptionAuthority>(provider => new PublishedTrustDeletionConsumptionAuthority(installation.CapabilityTrust,
            Guard(provider), new DaprErasureRootEvidenceJournal(Authority(provider, installation, value => value.EvidenceAuthority)),
            new DaprErasureRootRevocationEvidence(Authority(provider, installation, value => value.RegistrarAuthority)), installation.Native.NativeKeys.Keys.Select(tenant => (IDeletionConsumptionOwnerStateAuthority)
                new ErasureRootDeletionOwnerAuthority(provider.GetRequiredService<ErasureRootCustody>(), provider.GetRequiredService<IErasureRootAuthorization>(), tenant)).ToArray()));
        services.AddSingleton<ICustodyKeyLifecycleProvider>(provider => provider.GetRequiredService<ErasureRootCustody>());
        if (!observer)
        {
            services.AddSingleton<ICustodyKeyLifecycleAuthority, ErasureRootTenantLifecycleAuthority>();
            services.AddActors(options => options.Actors.RegisterActor<CustodyKeyLifecycleActor>());
        }
        return services.AddInteractionPayloadProtection();
        IGovernanceScopeGuard Guard(IServiceProvider provider)
        {
            IGovernanceScopeGuard actual = new DaprErasureRootGovernanceGuard(Authority(provider, installation, value => value.GuardAuthority));
            return !observer && installation.QualificationNativeControls is not null ? new ProtectionQualificationControlledGuard(actual, provider.GetRequiredService<ProtectionQualificationFaults>()) : actual;
        }
    }

    /// <summary>Resolves the exact public tenant bindings before any DI/native effects.</summary>
    public static IReadOnlyDictionary<string, ErasureRootTenantAuthorityBindings> TenantBindings(ErasureRootHostInstallation installation)
    {
        if (installation.TenantAuthorities is { } bindings)
        {
            if (!bindings.Keys.Order(StringComparer.Ordinal).SequenceEqual(installation.Native.NativeKeys.Keys.Order(StringComparer.Ordinal)))
            { throw new InvalidOperationException("Complete exact tenant authority enrollments required."); }
            return bindings;
        }
        if (installation.Native.NativeKeys.Count != 1) { throw new InvalidOperationException("Multitenant installation requires independently enrolled tenant authority bindings."); }
        return new Dictionary<string, ErasureRootTenantAuthorityBindings>(StringComparer.Ordinal)
        { [installation.Native.NativeKeys.Keys.Single()] = new(installation.PrivateAuthority, installation.SourceAuthority, installation.GuardAuthority, installation.RegistrarAuthority, installation.EvidenceAuthority) };
    }
    internal static ErasureRootTenantAuthority Authority(IServiceProvider provider, ErasureRootHostInstallation installation, Func<ErasureRootTenantAuthorityBindings, IndependentDaprAuthorityBinding> select)
        => new(TenantBindings(installation).ToDictionary(entry => entry.Key,
            entry => new IndependentDaprAuthority(new DaprSecretsInvoker(provider.GetRequiredService<DaprClient>()), select(entry.Value), TimeProvider.System), StringComparer.Ordinal));
    internal static void ValidateCapabilityTrust(ErasureRootHostInstallation installation)
    {
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (string tenant in installation.Native.NativeKeys.Keys) { ErasureRootTenantIdentity.Validate(tenant); }
        foreach (var trust in installation.CapabilityTrust)
        {
            if (trust is null) { throw new ArgumentException("Complete capability trust required."); }
            ErasureRootTenantIdentity.Validate(trust.TenantId);
            string[] fields = [trust.CapabilityKeyVersion, trust.Issuer, trust.Audience, trust.PublicAnchorId, trust.PublicAnchorVersion];
            if (!installation.Native.NativeKeys.ContainsKey(trust.TenantId) || trust.KeyFamily != "DeletionBatchCapabilitySigningKey"
                || fields.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 2048 || value.Any(char.IsControl))
                || trust.TrustProfileRevision is <= 0 or > 9007199254740991 || trust.SubjectPublicKeyInfo is not { Length: > 0 and <= 4096 }
                || !identities.Add(ErasureRootCustody.Hash(new { trust.TenantId, trust.KeyFamily, trust.CapabilityKeyVersion, trust.Issuer, trust.Audience })))
            { throw new ArgumentException("Unambiguous complete capability trust required."); }
            using var key = ECDsa.Create(); key.ImportSubjectPublicKeyInfo(trust.SubjectPublicKeyInfo, out int consumed);
            if (consumed != trust.SubjectPublicKeyInfo.Length || key.ExportParameters(false).Curve.Oid.Value != ECCurve.NamedCurves.nistP256.Oid.Value)
            { throw new ArgumentException("Exact P-256 capability anchor required."); }
        }
    }
    internal static byte[] ReadPinned(string path, string digest, int maximumBytes, Action? opened = null)
        => ProtectionPinnedFile.Read(path, maximumBytes, digest, opened);
}
