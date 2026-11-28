extern alias platformseed;

namespace Hexalith.Platform.Composition.Tests;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Shouldly;
using GatewayProtection = platformseed::PlatformGatewayProtection;

/// <summary>Checks that the composed gateway skips erasure-root protection only in a runner-owned Development composition.</summary>
public sealed class PlatformGatewayProtectionTests
{
    private const string CatalogPath = "/run/platform-components/run-catalog/catalog.json";

    [Fact]
    public void RunnerOwnedDevelopmentCompositionStartsWithoutAnInstallation()
    {
        var services = new ServiceCollection();

        GatewayProtection.AddPlatformGatewayProtection(services, Configuration(CatalogPath), Host(Environments.Development));

        GatewayProtection.IsDevelopmentComposition(Host(Environments.Development), Configuration(CatalogPath)).ShouldBeTrue();
        services.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Production", CatalogPath)]
    [InlineData("Staging", CatalogPath)]
    [InlineData("Development", null)]
    [InlineData("Development", " ")]
    public void EveryOtherHostStillRefusesWithoutAnInstallation(string environment, string? catalogPath)
    {
        var services = new ServiceCollection();

        InvalidOperationException refusal = Should.Throw<InvalidOperationException>(() =>
            GatewayProtection.AddPlatformGatewayProtection(services, Configuration(catalogPath), Host(environment)));

        refusal.Message.ShouldBe("DependencyNotAvailable:EXT-PROTECTION-1:ExplicitProtectionInstallationRequired");
        GatewayProtection.IsDevelopmentComposition(Host(environment), Configuration(catalogPath)).ShouldBeFalse();
        services.ShouldBeEmpty();
    }

    [Fact]
    public void ExplicitlyEnabledProtectionIsNeverSkipped()
    {
        IConfiguration configuration = Configuration(CatalogPath, new Dictionary<string, string?>
        {
            ["EventStore:PayloadProtection:Enabled"] = "true",
            ["EventStore:PayloadProtection:InstallationFile"] = "/missing/installation.json",
            ["EventStore:PayloadProtection:InstallationSha256"] = new string('0', 64),
        });

        Should.Throw<InvalidOperationException>(() => GatewayProtection.AddPlatformGatewayProtection(new ServiceCollection(), configuration, Host(Environments.Development)))
            .Message.ShouldBe("DependencyNotAvailable:EXT-PROTECTION-1:CompletePinnedErasureRootInstallationRequired");
    }

    private static IHostEnvironment Host(string environment) =>
        new HostingEnvironment { EnvironmentName = environment, ApplicationName = "Hexalith.Platform.EventStoreHost", ContentRootPath = Path.GetTempPath() };

    private static IConfiguration Configuration(string? catalogPath, IReadOnlyDictionary<string, string?>? extra = null)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (catalogPath is not null) values[GatewayProtection.DevelopmentCatalogPathKey] = catalogPath;
        foreach (var pair in extra ?? new Dictionary<string, string?>()) values[pair.Key] = pair.Value;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
