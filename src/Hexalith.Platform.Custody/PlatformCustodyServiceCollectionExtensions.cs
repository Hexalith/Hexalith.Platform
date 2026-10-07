using Hexalith.EventStore.Contracts.Security;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.Platform.Custody;

/// <summary>Explicit library registration with unavailable production defaults.</summary>
public static class PlatformCustodyServiceCollectionExtensions
{
    /// <summary>Adds cryptographic prerequisites; owners must independently supply custody/profile and domain/replay authorization.</summary>
    public static IServiceCollection AddPlatformCustody(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.TryAddSingleton<IPlatformHmacKeyProvider, UnavailablePlatformHmacKeyProvider>();
        services.TryAddSingleton<IPlatformSigningProfileProvider, UnavailablePlatformSigningProfileProvider>();
        services.TryAddSingleton<PlatformHmacService>();
        services.TryAddSingleton<TrustedEnvelopeAuthenticator>();
        services.TryAddScoped<IdentityHistoryCleanup>(provider => new(
            provider.GetRequiredService<TimeProvider>(), provider.GetService<IIdentityHistoryCustody>()));
        return services;
    }
}
