namespace Hexalith.Platform.Composition.Tests;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using Hexalith.Builds.Tooling.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

using CommunityToolkit.Aspire.Hosting.Dapr;

/// <summary>Exercises Toolkit generation and the production subscriber in an unstarted component model; grants no image admission.</summary>
public sealed class PlatformMemoriesContainerTransportTests
{
    [Fact]
    public async Task GeneratedContainerSidecarRetainsOriginsCredentialsAndGatewayReadinessAsync()
    {
        using var fixture = await PlatformPartiesPackageFixture.CreateAsync(PlatformCurrentOwnerProof.Profile);
        PlatformCompositionOptions options = fixture.CaptureCurrent();
        var servers = PlatformAppModel.Prepare(options);
        PlatformPartiesPackageFixture.AssertCatalogSelectedDependencies(
            PlatformPartiesPackageCatalogQualification.Bind(options, PlatformDeclarationSet.Load(options), servers, developmentRun: true));
        // Genuine resolved declaration environments retain workload credentials and readiness settings.
        PlatformServerBinding memoryBinding = servers.Single(server => server.ResourceId == "memories");
        PlatformServerBinding tenantBinding = servers.Single(server => server.ResourceId == "tenants-api");
        PlatformServerBinding gatewayBinding = servers.Single(server => server.ResourceId == "eventstore");
        var dapr = PlatformDaprBindings.Resolve(PlatformDeclarationSet.Load(options));
        PlatformRoutingCatalogBootstrap catalog = PlatformRoutingCatalogBootstrap.Create(options, servers, dapr,
            Path.Combine(fixture.Workspace, "platform-components"))!;
        var builder = PlatformPartiesPackageFixture.Builder();
        builder.Configuration["DcpPublisher:CliPath"] = Path.Combine(fixture.Workspace, "never-start-dcp");
        builder.Configuration["DcpPublisher:DashboardPath"] = Path.Combine(fixture.Workspace, "never-start-dashboard");
        var gateway = builder.AddProject("eventstore", fixture.Seed).WithHttpEndpoint(port: 31001, name: "http");
        var memory = builder.AddContainer("memories", "fixture/transport-only", "unstarted")
            .WithHttpEndpoint(targetPort: 8080, name: "http")
            .WithAnnotation(new PlatformServerAnnotation(memoryBinding));
        var tenant = builder.AddContainer("tenants-api", "fixture/transport-only", "unstarted")
            .WithHttpEndpoint(targetPort: 8080, name: "http")
            .WithAnnotation(new PlatformServerAnnotation(tenantBinding));
        foreach (var pair in memoryBinding.Environment) memory.WithEnvironment(pair.Key, pair.Value);
        foreach (var pair in tenantBinding.Environment) tenant.WithEnvironment(pair.Key, pair.Value);
        catalog.Configure(memory, memoryBinding);
        catalog.Configure(tenant, tenantBinding);
        catalog.Configure(gateway, gatewayBinding);
        memory.WithEnvironment("EventStore__CommandGateway__BaseAddress", gateway.GetEndpoint("http"));
        tenant.WithEnvironment("EventStore__CommandGateway__BaseAddress", gateway.GetEndpoint("http"))
            .WithEnvironment("EventStore__GatewayReadiness__BaseAddress", gateway.GetEndpoint("http"));
        string runtime = Path.Combine(fixture.Workspace, "model-runtime");
        Directory.CreateDirectory(Path.Combine(runtime, ".dapr", "bin"));
        File.WriteAllText(Path.Combine(runtime, ".dapr", "bin", "daprd"), "unstarted model marker");
        // Toolkit generates this model without executing a CLI; never discover a machine-installed Dapr.
        string cli = Path.Combine(runtime, OperatingSystem.IsWindows() ? "dapr.exe" : "dapr");
        File.WriteAllText(cli, "unstarted model marker");
        builder.AddDapr(options => { options.EnableTelemetry = false; options.DaprPath = cli; });
        foreach (var resource in new[] { memory, tenant })
        {
            resource.WithDaprSidecar(sidecar => sidecar.WithOptions(new DaprSidecarOptions { AppId = resource.Resource.Name, RuntimePath = runtime }));
        }
        gateway.WithDaprSidecar(sidecar => sidecar.WithOptions(new DaprSidecarOptions { AppId = "eventstore", RuntimePath = runtime }));
        builder.Services.AddSingleton<IDistributedApplicationEventingSubscriber>(new PlatformDaprEndpointSubscriber(
            [memoryBinding, tenantBinding, gatewayBinding]));
        TestContext.Current.TestOutputHelper!.WriteLine("Generated container and project bindings use the actual allocated sidecar endpoints.");
        await using DistributedApplication app = builder.Build();
        DistributedApplicationModel model = app.Services.GetRequiredService<DistributedApplicationModel>();
        IDistributedApplicationEventing eventing = app.Services.GetRequiredService<IDistributedApplicationEventing>();
        foreach (IDistributedApplicationEventingSubscriber subscriber in app.Services.GetServices<IDistributedApplicationEventingSubscriber>())
            await subscriber.SubscribeAsync(eventing, builder.ExecutionContext, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await eventing.PublishAsync(new BeforeStartEvent(app.Services, model), TestContext.Current.CancellationToken).ConfigureAwait(true);
        // Every generated CLI must use the fixture path, so a machine-installed Dapr can never satisfy this model.
        ExecutableResource[] clis = model.Resources.OfType<ExecutableResource>().Where(resource => resource.Name.EndsWith("-dapr-cli", StringComparison.Ordinal)).ToArray();
        clis.Select(resource => resource.Name).Order(StringComparer.Ordinal).ShouldBe(["eventstore-dapr-cli", "memories-dapr-cli", "tenants-api-dapr-cli"]);
        clis.ShouldAllBe(resource => resource.Command == cli);
        const string containerHost = "allocated-container-host.invalid";
        foreach (IResource resource in model.Resources)
        foreach (EndpointAnnotation endpoint in resource.Annotations.OfType<EndpointAnnotation>())
        {
            int port = endpoint.Port ?? endpoint.TargetPort ?? 32000;
            endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "127.0.0.1", port);
            endpoint.AllAllocatedEndpoints.AddOrUpdateAllocatedEndpoint(KnownNetworkIdentifiers.DefaultAspireContainerNetwork,
                new AllocatedEndpoint(endpoint, containerHost, port, EndpointBindingMode.SingleAddress,
                    networkId: KnownNetworkIdentifiers.DefaultAspireContainerNetwork));
        }

        ContainerResource memories = model.Resources.OfType<ContainerResource>().Single(resource => resource.Name == "memories");
        IExecutionConfigurationResult rendered = await ExecutionConfigurationBuilder.Create(memories).WithEnvironmentVariablesConfig()
            .BuildAsync(builder.ExecutionContext, NullLogger.Instance, TestContext.Current.CancellationToken).ConfigureAwait(true);
        rendered.Exception.ShouldBeNull();
        var environment = rendered.EnvironmentVariables.ToDictionary();
        var providers = rendered.EnvironmentVariablesWithUnprocessed.ToDictionary();
        EndpointReference sidecar = providers["DAPR_HTTP_ENDPOINT"].Unprocessed.ShouldBeOfType<EndpointReference>();
        sidecar.Resource.Name.ShouldBe("memories-dapr-cli");
        providers["EventStore__CommandGateway__BaseAddress"].Unprocessed.ShouldBeSameAs(sidecar);
        Uri gatewayOrigin = new(environment["EventStore__CommandGateway__BaseAddress"]);
        gatewayOrigin.ShouldBe(new Uri(environment["DAPR_HTTP_ENDPOINT"]));
        gatewayOrigin.Host.ShouldBe(containerHost);
        gatewayOrigin.AbsolutePath.ShouldBe("/");
        var context = new ValueProviderContext { Caller = memories, ExecutionContext = builder.ExecutionContext, Network = KnownNetworkIdentifiers.DefaultAspireContainerNetwork };
        EndpointReference direct = model.Resources.OfType<ProjectResource>().Single(resource => resource.Name == "eventstore").GetEndpoint("http");
        gatewayOrigin.ShouldNotBe(new Uri((await direct.GetValueAsync(context, TestContext.Current.CancellationToken).ConfigureAwait(true))!));
        gatewayOrigin.Port.ShouldBe(sidecar.Port);
        environment["EventStore__CommandGateway__Audience"].ShouldBe("eventstore");
        environment["EventStore__DomainService__AppId"].ShouldBe("memories");
        environment["Authentication__Workload__Audience"].ShouldBe("memories");
        string.IsNullOrWhiteSpace(environment["EventStore__CommandGateway__BoundCommand__PrivateKeyPkcs8Base64"]).ShouldBeFalse();
        var binding = memories.Annotations.OfType<PlatformServerAnnotation>().Single().Binding;
        string.IsNullOrWhiteSpace(environment["DAPR_API_TOKEN"]).ShouldBeFalse();
        string.Equals(environment["DAPR_API_TOKEN"], binding.Environment["DAPR_API_TOKEN"], StringComparison.Ordinal).ShouldBeTrue();
        binding.Environment.Where(pair => pair.Key is "DAPR_API_TOKEN" or "APP_API_TOKEN" || pair.Key.StartsWith("Authentication__", StringComparison.Ordinal))
            .All(pair => environment.TryGetValue(pair.Key, out string? value) && value == pair.Value).ShouldBeTrue();
        ContainerResource tenants = model.Resources.OfType<ContainerResource>().Single(resource => resource.Name == "tenants-api");
        IExecutionConfigurationResult tenantConfiguration = await ExecutionConfigurationBuilder.Create(tenants).WithEnvironmentVariablesConfig()
            .BuildAsync(builder.ExecutionContext, NullLogger.Instance, TestContext.Current.CancellationToken).ConfigureAwait(true);
        tenantConfiguration.Exception.ShouldBeNull();
        tenantConfiguration.EnvironmentVariablesWithUnprocessed.Single(pair => pair.Key == "EventStore__CommandGateway__BaseAddress")
            .Value.Unprocessed.ShouldBeOfType<EndpointReference>().Resource.Name.ShouldBe("eventstore");
        tenantConfiguration.EnvironmentVariablesWithUnprocessed.Single(pair => pair.Key == "EventStore__GatewayReadiness__BaseAddress")
            .Value.Unprocessed.ShouldBeOfType<EndpointReference>().Resource.Name.ShouldBe("eventstore");
        options.CurrentOwnerProof!.Matches(options).ShouldBeTrue();
    }
}
