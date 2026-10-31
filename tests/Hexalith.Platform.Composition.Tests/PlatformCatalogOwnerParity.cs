#if HEXALITH_PLATFORM_SOURCE_CATALOG
namespace Hexalith.Platform.Composition.Tests;

using System.Reflection;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;

/// <summary>Compares private reader output with real owner host registrations and installed gateway admission adapters without sidecars.</summary>
internal static class PlatformCatalogOwnerParity
{
    internal static async Task AssertAsync(PlatformRoutingCatalog catalog, IReadOnlyList<PlatformServerBinding> servers)
    {
        foreach (var server in servers.Where(server => server.ServerId == "domain"))
        {
            var assembly = Assembly.Load(new AssemblyName(Path.GetFileNameWithoutExtension(server.ProjectPath)));
            var entry = assembly.EntryPoint!.DeclaringType!;
            await (Task)typeof(PlatformCatalogOwnerParity).GetMethod(nameof(AssertHostAsync), BindingFlags.Static | BindingFlags.NonPublic)!
                .MakeGenericMethod(entry).Invoke(null, [catalog, server])!;
        }
        var services = new ServiceCollection();
        foreach (string path in catalog.AdapterAssemblies)
        foreach (var source in Assembly.Load(new AssemblyName(Path.GetFileNameWithoutExtension(path))).GetCustomAttributes<RouteCatalogAdapterSourceAttribute>())
        {
            var adapters = (IEnumerable<IDeclaredIdempotencyIntentAdapter>)source.SourceType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null)!;
            foreach (var adapter in adapters) services.AddSingleton<IIdempotencyIntentAdapter>(adapter);
        }
        services.AddSingleton<IOptions<IdempotencyAdmissionOptions>>(Options.Create(new IdempotencyAdmissionOptions
        {
            Enabled = true, ActiveDigestKeyVersion = "development-v1", DigestKeySource = IdempotencyDigestKeySource.Configuration,
            DigestKeySecretStoreName = "secretstore", DigestKeySecretName = "eventstore-idempotency-ring", DigestKeySecretGeneration = "development-v1",
            DigestKeys = new(StringComparer.Ordinal) { ["development-v1"] = Convert.ToBase64String(new byte[32]) },
        }));
        services.AddSingleton<IIdempotencyDigestKeyProvider, ConfigurationIdempotencyDigestKeyProvider>();
        using var provider = services.BuildServiceProvider();
        _ = new IdempotencyIntentAdapterRegistry(provider.GetServices<IIdempotencyIntentAdapter>(), new CanonicalIdempotencyIntentEncoder());
        var observed = await RouteCatalogAdmissionObservation.CaptureAsync(provider, "eventstore", TestContext.Current.CancellationToken);
        observed.Select(entry => entry.ContentDigest).Order().ShouldBe(catalog.Envelope.Idempotency.Select(entry => entry.ContentDigest).Order());
    }

    private static async Task AssertHostAsync<T>(PlatformRoutingCatalog catalog, PlatformServerBinding server) where T : class
    {
        await using var factory = new WebApplicationFactory<T>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["APP_API_TOKEN"] = "owner-parity-channel-token",
                ["EventStore:DomainService:AppId"] = server.AppId,
                ["EventStore:DomainService:ServiceVersion"] = "v1",
                ["Authentication:JwtBearer:Issuer"] = "owner-parity",
                ["Authentication:JwtBearer:Audience"] = "owner-parity",
                ["Authentication:JwtBearer:SigningKey"] = "owner-parity-signing-key-at-least-thirty-two-bytes",
                ["Tenants:AutoBootstrapGlobalAdmin"] = "false",
            }));
            // This component test executes the actual host's registrations and endpoint mappings.
            // Startup workers require live sidecars and are covered separately by the live lane.
            builder.ConfigureServices(services => services.RemoveAll<IHostedService>());
        });
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var expected = catalog.Envelope.Routes.Where(route => route.AppId == server.AppId).ToArray();
        var declarations = catalog.Envelope.Declarations.Where(declaration => expected.Any(route => route.RouteId == declaration.RouteId)).ToArray();
        var actual = DevelopmentDomainRouteCatalog.Targets(scope.ServiceProvider,
            factory.Services.GetRequiredService<EndpointDataSource>().Endpoints, declarations);
        actual.OrderBy(route => route.RouteId).ShouldBe(expected.Select(route => route with { OperationContractDigest = null }).OrderBy(route => route.RouteId));
    }
}
#endif
