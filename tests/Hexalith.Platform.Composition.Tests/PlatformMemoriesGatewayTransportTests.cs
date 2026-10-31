namespace Hexalith.Platform.Composition.Tests;

using System.Text.Json;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

public sealed partial class PlatformAppModelTests
{
    /// <summary>Routes the actual workload+Dapr client through its own sidecar, preserving other clients' HTTP binding.</summary>
    [Fact]
    public async Task MemoriesGatewayUsesItsOwnDaprOriginInsteadOfDirectGatewayHttpAsync()
    {
        IDistributedApplicationBuilder builder = Builder();
        try
        {
            SourceMapping mapping = await WorkspaceRootResolver.ResolveAsync(Path.Combine(Root, "references", "Hexalith.Memories", "module", "hexalith.module-manifest.v2.json"), WorkspaceMode.Source, TestContext.Current.CancellationToken);
            PlatformAppModel.Compose(builder, Options() with { Mapping = mapping });
            IResource memories = builder.Resources.Single(resource => resource.Name == "memories");
            Dictionary<string, object> environment = await MemoriesGatewayEnvironmentAsync(builder, memories);
            var gatewayOrigin = new Uri(environment["EventStore__CommandGateway__BaseAddress"].ShouldBeOfType<string>());
            var daprOrigin = new Uri(environment["DAPR_HTTP_ENDPOINT"].ShouldBeOfType<string>());
            gatewayOrigin.ShouldBe(daprOrigin);
            gatewayOrigin.AbsolutePath.ShouldBe("/");
            IDaprSidecarResource sidecar = memories.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar;
            gatewayOrigin.Port.ShouldBe(sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options.DaprHttpPort.GetValueOrDefault(-1));
            IResource tenants = builder.Resources.Single(resource => resource.Name == "tenants-api");
            Dictionary<string, object> tenantEnvironment = await MemoriesGatewayEnvironmentAsync(builder, tenants);
            tenantEnvironment["EventStore__CommandGateway__BaseAddress"].ShouldBeOfType<EndpointReference>().Resource.Name.ShouldBe("eventstore");
        }
        finally
        {
            using ServiceProvider provider = builder.Services.BuildServiceProvider();
            provider.GetService<PlatformCompositionWorkspace>()?.Dispose();
        }
    }

    /// <summary>Keeps a package-mode Memories container on the EventStore endpoint; its own loopback hosts no daprd.</summary>
    [Fact]
    public async Task PackageMemoriesContainerKeepsTheGatewayEndpointReferenceAsync()
    {
        IDistributedApplicationBuilder builder = Builder();
        try
        {
            PlatformAppModel.Compose(builder, PackageGraphOptions() with
            {
                ImageProbeRunner = (_, _, _) => Task.FromResult(new CompositionProcessResult(true, 0, "{}", false)),
            });
            IResource memories = builder.Resources.Single(resource => resource.Name == "memories").ShouldBeOfType<ContainerResource>();
            Dictionary<string, object> environment = await MemoriesGatewayEnvironmentAsync(builder, memories);
            environment["EventStore__CommandGateway__BaseAddress"].ShouldBeOfType<EndpointReference>().Resource.Name.ShouldBe("eventstore");
            environment.ShouldNotContainKey("DAPR_HTTP_ENDPOINT");
        }
        finally
        {
            string? policy = builder.Resources.SelectMany(resource => resource.Annotations.OfType<DaprSidecarAnnotation>())
                .Select(sidecar => sidecar.Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options.Config).FirstOrDefault();
            if (policy is not null) await CompositionWorkspace.TryDeleteAsync(Path.GetDirectoryName(policy)!, CancellationToken.None);
        }
    }

    /// <summary>Renders only EventStore's two required canonical POST operations on the actual Memory receiver.</summary>
    [Fact]
    public void MemoriesDomainGrantsCanonicalMetadataWithoutBroadeningItsTransport()
    {
        PlatformServerBinding[] servers = PlatformAppModel.Prepare(Options(active: "memories")).ToArray();
        PlatformServerBinding domain = servers.Single(server => server.ServerId == "domain" && PlatformDeclarationSet.Id(server.Module.Declaration) == "memories");
        PlatformDaprBindings bindings = PlatformDaprBindings.Resolve(servers.Select(server => server.Module).DistinctBy(module => module.Source).ToArray());
        JsonElement acl = bindings.AccessControl(domain.AppId, "default", "public");
        acl.GetProperty("defaultAction").GetString().ShouldBe("deny");
        JsonElement policy = acl.GetProperty("policies").EnumerateArray().Single();
        policy.GetProperty("appId").GetString().ShouldBe("eventstore");
        policy.GetProperty("defaultAction").GetString().ShouldBe("deny");
        JsonElement[] operations = policy.GetProperty("operations").EnumerateArray().ToArray();
        operations.Select(operation => operation.GetProperty("name").GetString()).Order(StringComparer.Ordinal)
            .ShouldBe(["/admin/operational-index-metadata", "/process"]);
        foreach (JsonElement operation in operations)
        {
            operation.GetProperty("action").GetString().ShouldBe("allow");
            operation.GetProperty("httpVerb").EnumerateArray().Select(verb => verb.GetString()).ShouldBe(["POST"]);
        }
    }

    private static async Task<Dictionary<string, object>> MemoriesGatewayEnvironmentAsync(IDistributedApplicationBuilder builder, IResource resource)
    {
        Dictionary<string, object> environment = new();
        foreach (EnvironmentCallbackAnnotation callback in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
            await callback.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, resource, environment, TestContext.Current.CancellationToken));
        return environment;
    }

}
