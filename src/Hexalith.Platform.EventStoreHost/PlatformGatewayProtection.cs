using Hexalith.Platform.Protection;

/// <summary>Registers the erasure-root protection the composed gateway requires, except in a runner-owned Development composition.</summary>
/// <remarks>
/// The local complete environment has no erasure-root installation. Only a Development host that serves a run-scoped development
/// catalog may start without it. Every other configuration still refuses with EXT-PROTECTION-1, and explicitly enabled
/// protection is always validated.
/// </remarks>
internal static class PlatformGatewayProtection
{
    internal const string DevelopmentCatalogPathKey = "EventStore:RouteCatalog:DevelopmentBootstrap:CatalogPath";

    private const string EnabledKey = "EventStore:PayloadProtection:Enabled";

    /// <summary>Gets whether this host is a Development composition whose run-scoped catalog bootstrap the Platform runner owns.</summary>
    internal static bool IsDevelopmentComposition(IHostEnvironment environment, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(configuration);
        return environment.IsDevelopment()
            && !string.IsNullOrWhiteSpace(configuration[DevelopmentCatalogPathKey])
            && !configuration.GetValue<bool>(EnabledKey);
    }

    /// <summary>Adds the erasure-root protection unless the host is a runner-owned Development composition.</summary>
    internal static IServiceCollection AddPlatformGatewayProtection(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
        => IsDevelopmentComposition(environment, configuration) ? services : services.AddPlatformErasureRootProtection(configuration);
}
