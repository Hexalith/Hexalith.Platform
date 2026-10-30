using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Platform.Protection;

/// <summary>Explicit private signing-owner composition; existing qualified signing authority and independent readiness enrollment are mandatory.</summary>
public static class DeletionSigningAvailabilityComposition
{
    /// <summary>Composes the actual actor, provider/trust readers and read-only independently authenticated readiness grant; performs no provisioning.</summary>
    public static IServiceCollection AddDeletionSigningAvailability(this IServiceCollection services, IDeletionCapabilitySigningAuthority signingAuthority,
        IndependentDaprAuthority readinessAuthority, SecretsRuntimeConfiguration configuration, ISecretsDaprInvoker transport, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(signingAuthority); ArgumentNullException.ThrowIfNull(readinessAuthority);
        if (string.IsNullOrWhiteSpace(configuration.CapabilitySignerReference) || string.IsNullOrWhiteSpace(configuration.CapabilitySignerCredentialReference)
            || string.IsNullOrWhiteSpace(configuration.PublicTrustReference) || transport is not ISecretsCredentialBoundDaprInvoker)
        { throw new InvalidOperationException("Complete independently enrolled signing availability inputs required."); }
        services.AddSingleton<IDeletionCapabilitySigningAuthority>(new DaprDeletionSigningAvailabilityAuthority(signingAuthority, readinessAuthority, configuration.CapabilitySignerReference, clock));
        services.AddSingleton<IDeletionCapabilitySigningProvider>(new DaprSigningProviderAdapter(configuration, transport, clock));
        services.AddSingleton<IDeletionCapabilitySigningTrustProvider>(new DaprSigningTrustAdapter(configuration, transport, clock));
        services.AddActors(options => options.Actors.RegisterActor<DeletionCapabilitySigningActor>());
        return services;
    }
}
