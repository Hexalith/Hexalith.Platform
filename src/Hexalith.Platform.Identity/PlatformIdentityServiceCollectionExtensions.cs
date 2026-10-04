using Hexalith.EventStore.Client.Security;
using Hexalith.EventStore.Server.Identity;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Hexalith.Platform.Identity;

/// <summary>Composes identity capabilities into the existing gateway without replacing authentication.</summary>
public static class PlatformIdentityServiceCollectionExtensions
{
    /// <summary>Registers explicit trust, public/private purpose keys and private durable registry services.</summary>
    public static IServiceCollection AddPlatformIdentity(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<PlatformIdentityOptions>().Bind(configuration.GetSection("PlatformIdentity"));
        services.AddOptions<IdentityAdmissionOptions>().Bind(configuration.GetSection("IdentityAdmission"));
        services.AddOptions<IdentityAdmissionSigningOptions>().Bind(configuration.GetSection("PlatformIdentity:GatewaySigning"));
        services.AddOptions<IdentityAdmissionOptions>("identity-registry").Bind(configuration.GetSection("PlatformIdentity:RegistryVerification"));
        services.AddOptions<IdentityAdmissionSigningOptions>("identity-registry").Bind(configuration.GetSection("PlatformIdentity:RegistrySigning"));
        services.AddOptions<IdentityAdmissionOptions>("identity-operator").Bind(configuration.GetSection("PlatformIdentity:OperatorVerification"));
        services.AddOptions<IdentityAdmissionOptions>("identity-bootstrap").Bind(configuration.GetSection("PlatformIdentity:BootstrapVerification"));
        services.AddOptions<ActorRegistryTrustOptions>().Bind(configuration.GetSection("PlatformIdentity:RegistryTrust"));
        services.AddSingleton<IIdentityAdmissionProof, IdentityAdmissionProof>();
        services.AddSingleton<IIdentityAdmissionSigner, IdentityAdmissionSigner>();
        foreach (string purpose in new[] { "identity-registry", "identity-operator", "identity-bootstrap" })
        {
            services.AddKeyedSingleton<IIdentityAdmissionProof>(purpose, (provider, _) => new IdentityAdmissionProof(
                new FixedOptionsMonitor<IdentityAdmissionOptions>(provider.GetRequiredService<IOptionsMonitor<IdentityAdmissionOptions>>(), purpose),
                provider.GetRequiredService<TimeProvider>()));
        }

        services.AddKeyedSingleton<IIdentityAdmissionSigner>("identity-registry", (provider, _) => new IdentityAdmissionSigner(
            new FixedOptionsMonitor<IdentityAdmissionSigningOptions>(provider.GetRequiredService<IOptionsMonitor<IdentityAdmissionSigningOptions>>(), "identity-registry")));
        services.AddSingleton<PlatformActorRegistry>();
        services.AddSingleton<PlatformOperatorProvenanceVerifier>();
        services.AddSingleton<PlatformBootstrapVerifier>();
        services.AddScoped<PlatformIdentityEnrollmentService>();
        services.AddScoped<IIdentityGatewayAdmission, PlatformIdentityGatewayAdmission>();
        return services;
    }
}
