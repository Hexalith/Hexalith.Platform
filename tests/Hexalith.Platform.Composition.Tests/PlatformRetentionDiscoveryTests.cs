namespace Hexalith.Platform.Composition.Tests;

using System.Text.Json.Nodes;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Shouldly;
using YamlDotNet.Serialization;

public sealed partial class PlatformAppModelTests
{
    private static JsonNode ParseDiscoveryConfiguration(string content)
    {
        object value = new DeserializerBuilder().WithAttemptingUnquotedStringTypeDeserialization().Build().Deserialize<object>(content);
        return JsonNode.Parse(new SerializerBuilder().JsonCompatible().Build().Serialize(value))!;
    }

    [Theory]
    [InlineData("component")]
    [InlineData("version")]
    [InlineData("foreign-registry")]
    public void RetentionDiscoveryWorkaroundRefusesAnUnboundResolver(string drift)
    {
        CompositionRunPlan plan = RetentionPlan();
        PlatformRetentionProfile profile = PlatformRetentionProfile.Select(plan, Root).ShouldNotBeNull();
        string configuration = CompositionDaprComponentRenderer.RenderConfiguration(plan);
        configuration = drift switch
        {
            "component" => configuration.Replace("component: \"sqlite\"", "component: \"mdns\"", StringComparison.Ordinal),
            "version" => configuration.Replace("version: \"v1\"", "version: \"v2\"", StringComparison.Ordinal),
            _ => configuration.Replace("name-resolution.db", "foreign.db", StringComparison.Ordinal),
        };
        Should.Throw<InvalidDataException>(() => profile.ConfigureDiscovery(configuration));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OnlySelectedRetentionDisablesDiscoveryGarbageCollectionAsync(bool selected)
    {
        string workspace = Path.Combine(Path.GetTempPath(), "retention-discovery-" + Guid.NewGuid().ToString("N"));
        try
        {
            CompositionRunPlan initial = RetentionPlan();
            SourceMapping mapping = initial.SourceMapping! with { Entries = [.. initial.SourceMapping.Entries, new SourceMappingEntry("Hexalith.Platform", "source", Root)] };
            CompositionRunPlan plan = CompositionRunPlanFactory.Create(initial.RunId, workspace, workspace, CompositionPortAllocator.Allocate(3), [], [], mapping,
                initial.PlatformDeclarationPaths, "memories", typeof(PlatformAppModel).Assembly.Location) with { SelectedTest = selected ? initial.SelectedTest : null };
            await CompositionDaprComponentRenderer.WriteAsync(plan, TestContext.Current.CancellationToken);
            string runnerConfiguration = File.ReadAllText(plan.DaprConfigPath);
            IDistributedApplicationBuilder builder = Builder();
            builder.Environment.EnvironmentName = "Development";
            PlatformAppModel.ComposeRun(builder, plan, CompositionSigningKey.Create(), null);
            IResource[] services = builder.Resources.Where(resource => resource.Annotations.OfType<PlatformServerAnnotation>().Any()).ToArray();
            services.Length.ShouldBe(5);
            string registry = Path.Combine(Path.GetDirectoryName(plan.DaprConfigPath)!, "name-resolution.db").Replace('\\', '/');
            foreach (IResource service in services)
            {
                DaprSidecarOptions sidecar = service.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options;
                JsonNode spec = ParseDiscoveryConfiguration(File.ReadAllText(sidecar.Config!))["spec"]!;
                JsonNode discovery = spec["nameResolution"]!;
                discovery["component"]!.GetValue<string>().ShouldBe("sqlite");
                discovery["version"]!.GetValue<string>().ShouldBe("v1");
                JsonObject configuration = discovery["configuration"]!.AsObject();
                configuration["connectionString"]!.GetValue<string>().ShouldBe(registry);
                if (selected) configuration["cleanupInterval"].ShouldNotBeNull().GetValue<string>().ShouldBe("0s", service.Name);
                else configuration.ContainsKey("cleanupInterval").ShouldBeFalse(service.Name);
                configuration.ContainsKey("updateInterval").ShouldBeFalse();
                configuration.ContainsKey("timeout").ShouldBeFalse();
                string appId = service.Annotations.OfType<PlatformServerAnnotation>().Single().Binding.AppId;
                int sentryPort = builder.Resources.Single(resource => resource.Name == "dapr-sentry").Annotations.OfType<EndpointAnnotation>().Single(endpoint => endpoint.Name == "grpc").Port!.Value;
                IReadOnlyDictionary<string, string> baselineFiles = PlatformDaprBindings.Resolve(PlatformDeclarationSet.Load(Options(active: "memories")))
                    .Render(Path.Combine(Root, "DaprComponents", "reference"), 1234, 2345, "offline-secrets", null, "localhost:" + sentryPort);
                JsonNode expected = ParseDiscoveryConfiguration(baselineFiles["accesscontrol." + appId + ".yaml"]);
                JsonNode actual = ParseDiscoveryConfiguration(File.ReadAllText(sidecar.Config!));
                actual["spec"]!.AsObject().Remove("nameResolution");
                JsonNode.DeepEquals(actual, expected).ShouldBeTrue("discovery must preserve the independently rendered security configuration");
                spec["mtls"]!["enabled"]!.GetValue<bool>().ShouldBeTrue();
                spec["mtls"]!["controlPlaneTrustDomain"]!.GetValue<string>().ShouldBe("localhost");
                spec["mtls"]!["sentryAddress"]!.GetValue<string>().ShouldBe("localhost:" + sentryPort);
                spec["features"]!.AsArray().Single(item => item!["name"]!.GetValue<string>() == "HotReload")!["enabled"]!.GetValue<bool>().ShouldBeFalse();
            }
            File.ReadAllText(plan.DaprConfigPath).ShouldBe(runnerConfiguration, "the supported runner's original configuration is retained");
            if (selected)
            {
                JsonNode component = JsonNode.Parse(File.ReadAllText(Path.Combine(workspace, "platform-components", "telemetry-state.yaml")))!;
                JsonArray metadata = component["spec"]!["metadata"]!.AsArray();
                metadata.Single(item => item!["name"]!.GetValue<string>() == "cleanupInterval")!["value"]!.GetValue<string>().ShouldBe("5m");
                component["spec"]!["type"]!.GetValue<string>().ShouldBe("state.postgresql");
            }
            // The registry is run-owned; disposal removes it with this workspace, including any stale registrations.
            File.WriteAllText(registry, "offline registry ownership fixture");
            (await CompositionWorkspace.TryDeleteAsync(workspace, TestContext.Current.CancellationToken)).ShouldBeTrue();
            File.Exists(registry).ShouldBeFalse();
        }
        finally { if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true); }
    }
}
