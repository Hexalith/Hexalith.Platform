using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Production owning-process composition; does not activate an Agents consuming seam or infer accepted availability.</summary>
public static class AgentsOwningProcessComposition
{
    /// <summary>Captures private replay and denial ports, durable authority, drain worker and fresh FR-34 startup/readiness gates.</summary>
    public static IServiceCollection AddInstalledAgentsOwningProcess(this IServiceCollection services, DaprClient dapr,
        AgentsOwningProcessInstallation installation, IGovernanceScopeGuard installedPrivateGuard, TimeProvider? clock = null,PrivateExportKeyDeliveryOwner? privateDeliveryOwner=null)
    {
        TimeProvider actualClock = clock ?? TimeProvider.System;
        if (!Independent(installation.SpoolAuthority,installation.Recorder)
            || !Independent(installation.ProtectionEngine,installation.ProtectionObserver))
            throw new ArgumentException("Independent spool/recorder and engine/observer installations required.");
        var transport = new DaprSecretsInvoker(dapr);
        if(installation.ExportDeliveryDenialOriginals is not null && privateDeliveryOwner is null) throw new InvalidOperationException("InstalledPrivateExportKeyDeliveryActorOwnerAndNativeAuthorityPorts");
        privateDeliveryOwner?.RegisterInstalledPrivateExportKeyDeliveryOwner(transport,installation.ExportDeliveryDenialOriginals,actualClock);
        IndependentDaprAuthority Authority(IndependentDaprAuthorityBinding binding) => new(transport, binding, actualClock);
        var authority = new DaprReplicatedSpoolAuthority(Authority(installation.SpoolAuthority));
        var recorder = new DaprSecurityObservationRecorder(Authority(installation.Recorder));
        var spool = new ReplicatedSecurityObservationSpool(dapr, actualClock, authority, recorder);
        var protection = new Fr34ProtectionGate(installation.ProtectionTarget, new DaprFr34ProtectionEngine(Authority(installation.ProtectionEngine)),
            new DaprFr34PersistedReader(Authority(installation.ProtectionObserver)), new DaprFr34CanaryAuthority(Authority(installation.ProtectionObserver)), actualClock);
        services.AddSingleton(actualClock);
        services.AddPrivateTrustedEnvelopeVerifier(new DaprIndependentReplayRegistrar(Authority(installation.ReplayAuthority)));
        string? compromiseMissing = services.AddPrivateCompromise(installation.Secrets, transport, installation.CompromiseSubscription,
            new EventStoreDeletionCapabilityGuardRevocationMirror(installedPrivateGuard, actualClock), actualClock);
        if (compromiseMissing is not null) throw new InvalidOperationException("Missing installed owning-process input: " + compromiseMissing);
        services.AddPrivateSecurityDenialRecorder(spool);
        services.AddPrivateSecurityObservationDrainWorker(spool, installation.DrainInterval, installation.DrainMaximumCount);
        services.AddSingleton(protection);
        services.AddHostedService<Fr34ProtectionStartupGate>();
        services.AddHealthChecks().AddCheck<Fr34ProtectionReadinessCheck>("agents-protection-fr34", tags: ["ready"])
            .AddCheck("agents-security-audit", new SpoolReadiness(spool), tags: ["ready"]);
        return services;
    }
    internal static bool Independent(IndependentDaprAuthorityBinding first,IndependentDaprAuthorityBinding second)
    {
        if(first.AppId==second.AppId || NormalizeCredential(first.CredentialReference)==NormalizeCredential(second.CredentialReference)) return false;
        using var a=System.Security.Cryptography.ECDsa.Create();using var b=System.Security.Cryptography.ECDsa.Create();
        a.ImportFromPem(first.PublicKeyPem);b.ImportFromPem(second.PublicKeyPem);
        return !a.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(b.ExportSubjectPublicKeyInfo());
    }
    internal static string NormalizeCredential(string reference)
    {
        string value=reference.Trim();
        if(value.StartsWith("file:",StringComparison.Ordinal))
        {
            string path=value[5..];if(!Path.IsPathFullyQualified(path)) throw new ArgumentException("Absolute private credential mount required.");
            return "file:"+Path.GetFullPath(path);
        }
        return value;
    }
    private sealed class SpoolReadiness(ReplicatedSecurityObservationSpool spool) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            => await spool.IsReadyAsync(cancellationToken).ConfigureAwait(false) ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("DependencyNotAvailable: EXT-HOST-1 security-audit-spool.");
    }
}
