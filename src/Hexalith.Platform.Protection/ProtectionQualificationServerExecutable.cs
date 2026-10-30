using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;
using Hexalith.EventStore.Server.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Hexalith.Platform.Protection;

/// <summary>Owned native role server entrypoint. It composes the shipped engine, native ERC, actual Dapr owner/signing actors and independent receiver clients.</summary>
public static class ProtectionQualificationServerExecutable
{
    /// <summary>Runs only a fully pinned explicit installation. Startup refuses missing inputs; it never performs provisioning or native genesis.</summary>
    public static Task<int> RunAsync(string role, string installationFile, string installationSha256, CancellationToken token = default)
        => RunCoreAsync(role, installationFile, installationSha256, null, token);
    internal static async Task<int> RunCoreAsync(string role, string installationFile, string installationSha256, Action? signerValidated, CancellationToken token = default)
    {
        try
        {
            var installed = JsonSerializer.Deserialize<ProtectionQualificationServerInstallation>(Read(installationFile, installationSha256), ErasureRootCustody.Json)!;
            var scope = JsonSerializer.Deserialize<ProtectionQualificationServiceScope>(Read(installed.ScopeFile, installed.ScopeSha256), ErasureRootCustody.Json)!;
            if (scope.NativeArguments is null) { return 2; }
            scope = scope with { Recipes = ProtectionQualificationSourceCatalog.Compile(scope.NativeArguments) };
            if (!scope.Observations.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(scope.Recipes.Keys)) { return 2; }
            scope = scope with { Observations = scope.Observations.ToDictionary(entry => entry.Key,
                entry => entry.Value with { CaseId = entry.Key, Fields = new(StringComparer.Ordinal) }, StringComparer.Ordinal) };
            if (scope.Role != role || installed.SourceComponents.Intersect(installed.ReadComponents, StringComparer.Ordinal).Any() && role == "observer"
                || role == "observer" && installed.SourceComponents.Length != 0) { return 2; }
            byte[] publicBytes = Read(installed.PublicAnchorFile, installed.PublicAnchorSha256);
            if (!Path.IsPathFullyQualified(installed.SignerFile) || new FileInfo(installed.SignerFile).LinkTarget is not null || new FileInfo(installed.SignerFile).Length > 16384) { return 2; }
            using var signer = ECDsa.Create(); byte[] secret = await File.ReadAllBytesAsync(installed.SignerFile, token).ConfigureAwait(false);
            char[] chars = System.Text.Encoding.UTF8.GetChars(secret);
            try { signer.ImportFromPem(chars); } finally { CryptographicOperations.ZeroMemory(secret); Array.Clear(chars); }
            using var anchor = ECDsa.Create(); anchor.ImportFromPem(System.Text.Encoding.UTF8.GetString(publicBytes));
            if (!HasPrivateSigner(signer, anchor)) { return 2; }
            signerValidated?.Invoke();
            ProtectionQualificationInputSchema.ValidateInstallation(scope, (role == "observer" ? installed.ReadComponents : installed.SourceComponents).ToHashSet(StringComparer.Ordinal), installed.FaultOperations.ToHashSet(StringComparer.Ordinal));
            using var dapr = new DaprClientBuilder().Build();
            var services = new ServiceCollection(); services.AddLogging();
            // Only bounded content-free owning-worker events reach this private server's console.
            services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ErasureRootHolderCompletionWorker>>(_ => ErasureRootCompletionLogger.Create<ErasureRootHolderCompletionWorker>()); services.AddSingleton(dapr); services.AddSingleton(ActorProxy.DefaultProxyFactory);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EventStore:PayloadProtection:Enabled"] = "true", ["EventStore:PayloadProtection:InstallationFile"] = installed.HostInstallationFile,
                ["EventStore:PayloadProtection:InstallationSha256"] = installed.HostInstallationSha256,
                ["EventStore:PayloadProtection:QualificationObserver"] = (role == "observer").ToString(System.Globalization.CultureInfo.InvariantCulture),
            }).Build();
            services.AddPlatformErasureRootProtection(configuration);
            var controls = new ProtectionQualificationFaults(dapr, installed.FaultControl, installed.FaultEnrollmentId, installed.FaultOperations.ToHashSet(StringComparer.Ordinal));
            if (role == "executor")
            {
                services.Replace(ServiceDescriptor.Singleton<IErasureRootStore>(provider =>
                {
                    var host = provider.GetRequiredService<ErasureRootHostInstallation>();
                    return new ProtectionQualificationControlledErasureRootStore(new OpenBaoErasureRootStore(provider.GetRequiredService<IHttpClientFactory>().CreateClient("hexalith-erasure-root"), host.Native, host.CredentialReference), controls);
                }));
            }
            await using var provider = services.BuildServiceProvider();
            var custody = provider.GetRequiredService<ErasureRootCustody>(); var permission = provider.GetRequiredService<IErasureRootAuthorization>();
            var journal = new ProtectionQualificationOperationJournal(custody, permission);
            var engine = provider.GetRequiredService<InteractionPayloadProtectionService>();
            ProtectionQualificationActionRunner? runner = null;
            if (role == "executor")
            {
                var host = provider.GetRequiredService<ErasureRootHostInstallation>(); var transport = new DaprSecretsInvoker(dapr);
                var guard = new ProtectionQualificationControlledGuard(new DaprErasureRootGovernanceGuard(ErasureRootHostComposition.Authority(provider, host, value => value.GuardAuthority)), controls);
                var consumption = ActorProxy.Create<IDeletionConsumptionActor>(new ActorId(DeletionConsumptionActor.GetActorId(scope.TenantId)), DeletionConsumptionActor.ActorTypeName);
                var subscriber = new DeletionCapabilityRevocationSubscriber(installed.RevocationSubscription,
                    provider.GetRequiredService<IDeletionCapabilityRevocationAuthenticator>(),
                    new DaprDeletionCapabilityCompromiseRegistrar(ActorProxy.DefaultProxyFactory), new EventStoreDeletionCapabilityGuardRevocationMirror(guard, TimeProvider.System), TimeProvider.System);
                runner = new(new(engine, custody, provider.GetRequiredService<ErasureRootOccurrenceKeyProvider>(),
                    provider.GetRequiredService<IErasureRootOccurrenceSource>(), dapr, guard,
                    new DaprDeletionProtectionOwner(scope.TenantId, ActorProxy.DefaultProxyFactory, TimeProvider.System), consumption,
                    new QualificationSigningActorRouter(), subscriber, controls, installed.SourceComponents.ToHashSet(StringComparer.Ordinal), permission));
            }
            var observer = role == "observer" ? new ProtectionQualificationPersistedObserver(dapr, custody, engine, installed.ReadComponents.ToHashSet(StringComparer.Ordinal)) : null;
            var service = new ProtectionQualificationRoleService(scope, journal, runner, observer, dapr, signer, async cancellation =>
            {
                var references = new Dictionary<string, CustodyInteractionDigestReference>(StringComparer.Ordinal);
                void Visit(System.Text.Json.Nodes.JsonNode? value)
                {
                    if (value is System.Text.Json.Nodes.JsonObject obj)
                    {
                        if (obj["Digest"] is { } digest)
                        {
                            var reference = digest.Deserialize<CustodyInteractionDigestReference>(ErasureRootCustody.Json)
                                ?? throw new InvalidOperationException("Complete native digest reference required.");
                            references.TryAdd(ErasureRootCustody.Hash(reference), reference);
                        }
                        foreach (var item in obj) { Visit(item.Value); }
                    }
                    else if (value is System.Text.Json.Nodes.JsonArray array) { foreach (var item in array) { Visit(item); } }
                }
                foreach (var value in scope.NativeArguments.Values) { Visit(value); }
                foreach (var fault in scope.Recipes.Values.SelectMany(value => value.Values).SelectMany(value => value.Before.Concat([value.Effect]).Concat(value.Race is { } race ? [race] : []).Concat(value.After)).Where(value => value.Kind == ProtectionQualificationNativeCallKind.Fault))
                {
                    if (!await controls.PreflightAsync(fault.Payload["stepId"]!.GetValue<string>(), fault.Payload["boundary"]!.GetValue<string>(), cancellation).ConfigureAwait(false)) { return false; }
                }
                if (references.Count == 0) { return false; }
                var resolver = provider.GetRequiredService<ErasureRootDigestKeyResolver>();
                foreach (var reference in references.Values)
                { if (!await resolver.IsAvailableAsync(reference, cancellation).ConfigureAwait(false)) { return false; } }
                foreach (string required in new[] { "RetainQualificationSeedOriginal", "RetainQualificationProducerOriginal", "RetainReserveRefusalOriginal",
                    "WrapDek", "EnrollOccurrence", "OccurrenceWrite", "OccurrenceRead", "RegisterIndependentSourceOriginal", "ReadIndependentSourceOriginal", "VerifyCommittedSourceEnrollment", "ReadOccurrenceBinding", "ReadInstalledRegistryEpoch",
                    "Reserve", "RetainSealed", "CompleteWriter", "AcquireEncryptionLease", "AcquireCompletionLease", "UnwrapDek", "CompleteRootHolder", "AdmitIndependentSourceReservation", "VerifyNativeEngineSealedOriginal", "ConfirmNativePersistedWriter",
                    "RetainOccurrenceIndex", "MigrateOccurrenceIndex", "VerifySnapshotEntryManifest", "AcquireSnapshotCompletionLeases", "RetainNativeCallOriginal", "ReadNativeCallOriginal", "CaptureEffectBoundary" })
                {
                    if (!await permission.AuthorizeAsync(scope.TenantId, scope.BindingDigest, required, ErasureRootCustody.Hash(scope.NativeArguments), cancellation).ConfigureAwait(false)) { return false; }
                }
                return await permission.AuthorizeAsync(scope.TenantId, scope.BindingDigest, "QualificationEnrollmentPreflight", ErasureRootCustody.Hash(references.Keys.Order(StringComparer.Ordinal).ToArray()), cancellation).ConfigureAwait(false);
            }, async cancellation =>
            {
                if (role != "observer") { return null; }
                var host = provider.GetRequiredService<ErasureRootHostInstallation>();
                string Resolve(string? reference) => reference is not null && reference.StartsWith("env:", StringComparison.Ordinal) ? Environment.GetEnvironmentVariable(reference[4..]) ?? "" : "";
                string workload = Resolve(installed.WorkloadIdentityReference);
                var proof = await permission.ReadObserverPermissionAsync(scope.TenantId, workload, cancellation).ConfigureAwait(false);
                return proof is not null && ProtectionQualificationObserverPermissions.Matches(proof, scope, host, workload, Resolve(host.CredentialReference),
                    ProtectionQualificationObserverPermissions.DaprCredential(dapr, ErasureRootHostComposition.TenantBindings(host)[scope.TenantId].PrivateAuthority.AppId), Resolve(installed.CredentialReference), installed.ReadComponents.ToHashSet(StringComparer.Ordinal), TimeProvider.System.GetUtcNow()) ? proof : null;
            });
            await using var application = BuildApplication(provider, service, installed.CredentialReference);
            await application.RunAsync(token).ConfigureAwait(false); return 0;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception) { return 2; }
    }
    /// <summary>Attaches the actual owning provider's completion workers to the role HTTP server lifetime. Observer providers register no mutation workers.</summary>
    public static Microsoft.AspNetCore.Builder.WebApplication BuildApplication(IServiceProvider owner, ProtectionQualificationRoleService service, string credentialReference)
        => ProtectionQualificationRoleApplication.Build(service, credentialReference, owner.GetServices<IHostedService>().ToArray());
    /// <summary>Proves usable private P-256 signing before constructing any application or native graph.</summary>
    internal static bool HasPrivateSigner(ECDsa signer, ECDsa anchor)
    {
        try
        {
            if (signer.ExportParameters(false).Curve.Oid.Value != ECCurve.NamedCurves.nistP256.Oid.Value
                || anchor.ExportParameters(false).Curve.Oid.Value != ECCurve.NamedCurves.nistP256.Oid.Value
                || !signer.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(anchor.ExportSubjectPublicKeyInfo())) { return false; }
            byte[] challenge = RandomNumberGenerator.GetBytes(32);
            byte[] signature = signer.SignData(challenge, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            return anchor.VerifyData(challenge, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (CryptographicException) { return false; }
    }
    private static byte[] Read(string path, string digest)
    {
        if (!Path.IsPathFullyQualified(path) || digest is not { Length: 64 } || !File.Exists(path) || new FileInfo(path).LinkTarget is not null || new FileInfo(path).Length > 16 * 1024 * 1024)
        { throw new InvalidOperationException("Exact bounded pinned role installation required."); }
        byte[] bytes = File.ReadAllBytes(path);
        return Convert.ToHexString(SHA256.HashData(bytes)).Equals(digest, StringComparison.OrdinalIgnoreCase) ? bytes : throw new InvalidOperationException("Changed role installation.");
    }
}
