namespace Hexalith.Platform.Composition.Tests;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using Hexalith.Builds.Tooling.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

/// <summary>Renders the actual authenticated current-owner container graph without starting applications or services.</summary>
public sealed class PlatformCurrentOwnerMemoriesTransportTests
{
    /// <summary>The real Toolkit sidecar provider supplies both Dapr origins, including its allocated container-network address.</summary>
    [Fact]
    public async Task CurrentOwnerMemoriesContainerUsesItsActualSidecarOriginAsync()
    {
        using var fixture = await PlatformPartiesPackageFixture.CreateAsync(PlatformCurrentOwnerProof.Profile).ConfigureAwait(true);
        CompositionWorkspace.CreatePrivateDirectory(Path.GetDirectoryName(fixture.Plan.DaprConfigPath)!);
        await File.WriteAllTextAsync(fixture.Plan.DaprConfigPath, CompositionDaprComponentRenderer.RenderConfiguration(fixture.Plan),
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        var builder = PlatformPartiesPackageFixture.Builder();
        builder.Configuration["DcpPublisher:CliPath"] = Path.Combine(fixture.Workspace, "never-start-dcp");
        builder.Configuration["DcpPublisher:DashboardPath"] = Path.Combine(fixture.Workspace, "never-start-dashboard");
        string runtime = Path.Combine(fixture.Plan.DaprHome, ".dapr", "bin");
        Directory.CreateDirectory(runtime);
        File.WriteAllText(Path.Combine(runtime, "daprd"), "model-only runtime marker; never executed");
        PlatformCompositionOptions? admitted = null;
        HashSet<string> images = new(StringComparer.Ordinal);
        fixture.Compose(builder, options =>
        {
            options.CurrentOwnerProof!.Matches(options).ShouldBeTrue();
            admitted = options;
            return options with
            {
                ImageProbeRunner = (start, _, _) =>
                {
                    start.FileName.ShouldBe("docker");
                    start.ArgumentList.Take(2).ShouldBe(["manifest", "inspect"]);
                    PlatformPartiesPackageFixture.CurrentImages.ShouldContain(start.ArgumentList[2]);
                    images.Add(start.ArgumentList[2]);
                    return Task.FromResult(new CompositionProcessResult(true, 0, "{}", false));
                },
            };
        });
        images.Order(StringComparer.Ordinal).ShouldBe(PlatformPartiesPackageFixture.CurrentImages.Order(StringComparer.Ordinal));
        admitted.ShouldNotBeNull().CurrentOwnerProof!.Matches(admitted).ShouldBeTrue();
        await using DistributedApplication app = builder.Build();
        DistributedApplicationModel model = app.Services.GetRequiredService<DistributedApplicationModel>();
        IDistributedApplicationEventing eventing = app.Services.GetRequiredService<IDistributedApplicationEventing>();
        foreach (IDistributedApplicationEventingSubscriber subscriber in app.Services.GetServices<IDistributedApplicationEventingSubscriber>())
            await subscriber.SubscribeAsync(eventing, builder.ExecutionContext, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await eventing.PublishAsync(new BeforeStartEvent(app.Services, model), TestContext.Current.CancellationToken).ConfigureAwait(true);
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
        admitted.CurrentOwnerProof!.Matches(admitted).ShouldBeTrue();
    }
}
