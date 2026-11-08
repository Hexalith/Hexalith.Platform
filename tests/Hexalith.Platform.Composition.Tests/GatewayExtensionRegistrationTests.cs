#if HEXALITH_PLATFORM_EXTENSION_SDK
namespace Hexalith.Platform.Composition.Tests;

using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Extensions;
using Hexalith.EventStore.Server.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.Parties.EventStoreExtensions;
using Hexalith.Platform.TestExtension;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using Shouldly;

/// <summary>Declared and legacy packages share the real gateway bootstrap without registering an adapter twice.</summary>
[Collection("Platform file entry")]
public sealed class GatewayExtensionRegistrationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task BootstrapReusesDeclaredPackagesAndKeepsLegacyPackagesAsync(bool declared, bool mixed)
    {
        string directory = Directory.CreateTempSubdirectory("gateway-extension-registration-").FullName;
        try
        {
            IConfiguration configuration = Configuration(directory, declared, mixed);
            ServiceCollection services = new();
            services.AddLogging();
            int before = PlatformTestExtensionAdapters.CreationCount;
            services.AddEventStoreExtensions(configuration);
            IIdempotencyIntentAdapter? registered = services
                .Where(descriptor => descriptor.ServiceType == typeof(IIdempotencyIntentAdapter))
                .Select(descriptor => descriptor.ImplementationInstance).Cast<IIdempotencyIntentAdapter>().SingleOrDefault();

            services.AddEventStoreRunScopedCatalogGateway(configuration);
            // Repeated bootstrap registration is already supported and must stay inert.
            services.AddEventStoreRunScopedCatalogGateway(configuration);
            PlatformTestExtensionAdapters.CreationCount.ShouldBe(before + 1);
            await using ServiceProvider provider = services.BuildServiceProvider();
            IIdempotencyIntentAdapter[] adapters = [.. provider.GetServices<IIdempotencyIntentAdapter>()];
            IIdempotencyIntentAdapter probe = adapters.Single(adapter => adapter.AdapterId == PlatformTestExtensionAdapters.AdapterId);
            if (declared) probe.ShouldBeSameAs(registered);
            if (mixed) adapters.Length.ShouldBeGreaterThan(1);
            else adapters.Length.ShouldBe(1);
            _ = new IdempotencyIntentAdapterRegistry(Substitute.For<IRouteCatalogAuthority>(), adapters, new CanonicalIdempotencyIntentEncoder());

            if (declared)
            {
                EventStoreLoadedExtension extension = provider.GetRequiredService<EventStoreExtensionCatalog>().Extensions.ShouldHaveSingleItem();
                extension.AdapterCount.ShouldBe(1);
                HealthReport report = await provider.GetRequiredService<HealthCheckService>().CheckHealthAsync(
                    registration => registration.Name == EventStoreExtensionServiceCollectionExtensions.HealthCheckName,
                    TestContext.Current.CancellationToken);
                report.Status.ShouldBe(HealthStatus.Healthy);
                report.Entries[EventStoreExtensionServiceCollectionExtensions.HealthCheckName].Data["platform-test"].ShouldBe(1);
            }
            else provider.GetService<EventStoreExtensionCatalog>().ShouldBeNull();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static IConfiguration Configuration(string directory, bool declared, bool mixed)
    {
        const string section = "EventStore:RouteCatalog:DevelopmentBootstrap:";
        Dictionary<string, string?> values = new()
        {
            [section + "Directory"] = directory,
            [section + "HostId"] = "eventstore",
            [section + "CoordinatorHostId"] = "eventstore",
            [section + "AttemptId"] = "registration-test",
            [section + "RequiredHostIds:0"] = "eventstore",
            [section + "OwnerAssemblies:0"] = typeof(PlatformTestExtensionAdapters).Assembly.Location,
            [section + "AdapterAssemblies:0"] = typeof(PlatformTestExtensionAdapters).Assembly.Location,
            ["EventStore:DomainService:AppId"] = "eventstore",
        };
        if (mixed) values[section + "AdapterAssemblies:1"] = typeof(PartiesCatalogAdapters).Assembly.Location;
        if (declared)
        {
            values["EventStore:Extensions:Packages:0:PackageId"] = typeof(PlatformTestExtensionAdapters).Assembly.GetName().Name;
            values["EventStore:Extensions:Packages:0:ExtensionId"] = PlatformTestExtensionAdapters.ExtensionId;
            values["EventStore:Extensions:Packages:0:ApiMajor"] = "1";
        }
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
#endif
