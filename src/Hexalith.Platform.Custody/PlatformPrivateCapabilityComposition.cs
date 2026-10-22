using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Platform.Custody;

/// <summary>Explicit private-object composition; exception capabilities remain constructor closures and are never registered for general service resolution.</summary>
public static class PlatformPrivateCapabilityComposition
{
    /// <summary>Adds only the trusted replay verifier; the raw registrar is unavailable to target/public/Workflow/general-dispatcher resolution.</summary>
    public static IServiceCollection AddPrivateTrustedEnvelopeVerifier(this IServiceCollection services, ITrustedEnvelopeReplayRegistrar registrar)
    {
        ArgumentNullException.ThrowIfNull(services); ArgumentNullException.ThrowIfNull(registrar);
        services.AddSingleton(provider => new TrustedEnvelopeReplayVerifier(provider.GetRequiredService<TrustedEnvelopeAuthenticator>(), registrar,
            provider.GetRequiredService<IPlatformSigningProfileProvider>(), provider.GetRequiredService<TimeProvider>()));
        return services;
    }
    /// <summary>Adds only the denial recorder; raw replicated spool and its worker credential remain private constructor captures.</summary>
    public static IServiceCollection AddPrivateSecurityDenialRecorder(this IServiceCollection services, ReplicatedSecurityObservationSpool spool)
    {
        ArgumentNullException.ThrowIfNull(services); ArgumentNullException.ThrowIfNull(spool);
        services.AddSingleton(provider => new PlatformSecurityDenialRecorder(provider.GetRequiredService<TrustedEnvelopeAuthenticator>(),
            provider.GetRequiredService<PlatformHmacService>(), spool, provider.GetRequiredService<TimeProvider>()));
        return services;
    }
    /// <summary>Returns the existing spool with the supplied credential captured on its constructor. A null authority is not registered and writes nothing. The spool itself is not added to general DI.</summary>
    public static ReplicatedSecurityObservationSpool CapturePrivateSpool(DaprClient client, TimeProvider clock, PrivateReplicatedSpoolAuthority? authority, ISecurityObservationRecorder? recorder = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(clock);
        return new ReplicatedSecurityObservationSpool(client, clock, authority, recorder);
    }

    /// <summary>Adds only a restricted hosted recovery loop; raw spool/recorder credentials remain private constructor captures and ordinary registration installs no worker.</summary>
    public static IServiceCollection AddPrivateSecurityObservationDrainWorker(this IServiceCollection services, ReplicatedSecurityObservationSpool spool, TimeSpan interval, int maximumCount)
    {
        ArgumentNullException.ThrowIfNull(services); ArgumentNullException.ThrowIfNull(spool);
        services.AddHostedService(provider => new ReplicatedSecurityObservationSpoolWorker(spool, provider.GetRequiredService<TimeProvider>(), interval, maximumCount));
        return services;
    }
    /// <summary>Adds only the authenticated custody subscriber; the compromise registrar is not registered or injected into signing/dispatch paths.</summary>
    public static IServiceCollection AddPrivateDeletionRevocationSubscriber(this IServiceCollection services, DeletionCapabilityRevocationSubscription subscription,
        IDeletionCapabilityRevocationAuthenticator authenticator, IDeletionCapabilityCompromiseRegistrar registrar, IDeletionCapabilityGuardRevocationMirror mirror)
    {
        ArgumentNullException.ThrowIfNull(services); ArgumentNullException.ThrowIfNull(subscription); ArgumentNullException.ThrowIfNull(authenticator);
        ArgumentNullException.ThrowIfNull(registrar); ArgumentNullException.ThrowIfNull(mirror);
        services.AddSingleton(provider => new DeletionCapabilityRevocationSubscriber(subscription, authenticator, registrar, mirror,
            provider.GetRequiredService<TimeProvider>()));
        return services;
    }
}
