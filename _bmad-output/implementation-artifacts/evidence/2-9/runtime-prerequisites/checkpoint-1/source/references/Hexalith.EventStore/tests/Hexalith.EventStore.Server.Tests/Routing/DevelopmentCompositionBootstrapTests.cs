using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class DevelopmentCompositionBootstrapTests
{
    [Fact]
    public async Task EveryActualHostMustPrepareAndAgreeBeforePrivateBootstrapCanServe()
    {
        var fixture = new RouteCatalogTestFixture();
        string directory = Path.Combine(Path.GetTempPath(), "catalog-bootstrap-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var providers = new List<ServiceProvider>();
        using var bound = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            string[] hosts = fixture.Hosts.Select(h => h.HostId).ToArray();
            var tasks = fixture.Observations.Select((actual, index) =>
            {
                var services = new ServiceCollection();
                services.AddSingleton<IOptions<RouteCatalogHostOptions>>(Options.Create(new RouteCatalogHostOptions
                { Profile = "Development", DevelopmentFixture = true }));
                services.AddSingleton<IRouteCatalogAuthority>(provider => new RouteCatalogAuthority(fixture.Store,
                    new DevelopmentRouteCatalogTrustValidator(provider.GetRequiredService<IOptions<RouteCatalogHostOptions>>(),
                        DevelopmentEnvironment()), provider.GetRequiredService<IOptions<RouteCatalogHostOptions>>(),
                    new ObservedRouteCatalogHost(_ => Task.FromResult(actual))));
                var provider = services.BuildServiceProvider(); providers.Add(provider);
                return DevelopmentRouteCatalogBootstrap.ActivateAsync(provider, directory, actual.HostId, hosts[0], "private-attempt", hosts,
                    (_, _) => Task.FromResult(actual), index == 0 ? _ => fixture.Envelope : null, bound.Token);
            }).ToArray();
            await Task.WhenAll(tasks);
            foreach (var provider in providers)
                (await provider.GetRequiredService<IRouteCatalogAuthority>().ReadbackAsync()).Serving.ShouldBeTrue();
            Directory.GetFiles(directory, "*.committed.json").Length.ShouldBe(hosts.Length);
            var state = await fixture.Store.ReadAsync(default);
            state.State.Committed!.RequiredHosts.ShouldBe(fixture.Hosts);
            state.State.Pending.ShouldBeNull();
        }
        finally
        {
            foreach (var provider in providers) await provider.DisposeAsync();
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void MissingPrivateRunnerConfigurationRegistersNoBootstrapOrObservation()
    {
        var services = new ServiceCollection();
        services.AddEventStoreDevelopmentRouteCatalogBootstrap(new ConfigurationBuilder().Build(),
            (_, _) => throw new InvalidOperationException("Must remain inert"));
        services.ShouldBeEmpty();
    }

    [Fact]
    public async Task CopiedCandidateIdentityCannotReplaceAnActualHostsDifferentTargets()
    {
        var fixture = new RouteCatalogTestFixture();
        var candidate = fixture.Candidate();
        int index = Array.FindIndex(fixture.Observations, o => o.Targets.Count > 0);
        fixture.Observations[index] = fixture.Observations[index] with { Targets = [] };
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Authorities[index].PrepareAsync(candidate));
        (await fixture.Store.ReadAsync(default)).State.Committed.ShouldBeNull();
    }

    private static Microsoft.Extensions.Hosting.IHostEnvironment DevelopmentEnvironment() =>
        new Microsoft.Extensions.FileProviders.NullFileProvider() is var files
            ? new BootstrapEnvironment(files) : throw new InvalidOperationException();

    private sealed class BootstrapEnvironment(Microsoft.Extensions.FileProviders.IFileProvider files) : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "DevelopmentCompositionBootstrapTests";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = files;
    }
}
