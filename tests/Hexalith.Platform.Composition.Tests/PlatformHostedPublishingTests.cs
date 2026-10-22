namespace Hexalith.Platform.Composition.Tests;

using System.Text.Json;
using System.Text.Json.Nodes;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Hexalith.Builds.Tooling.Manifest;
using Microsoft.Extensions.Configuration;
using Shouldly;

[Collection("Platform file entry")]
public sealed class PlatformHostedPublishingTests
{
    private static string Root
    {
        get
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "apphost.cs"))) directory = directory.Parent;
            return directory!.FullName;
        }
    }

    [Fact]
    public void NeutralReferenceGraphAndLocalBindingHaveIdenticalServersAndReadiness()
    {
        PlatformCompositionOptions options = PlatformHostedPublishing.ReferenceOptions(Root);
        PlatformApplicationGraph graph = PlatformApplicationGraph.Resolve(options);
        IReadOnlyList<PlatformServerBinding> local = PlatformAppModel.Prepare(options);
        graph.Workloads.Count.ShouldBe(7);
        graph.Workloads.Select(w => (w.ResourceId, w.AppId, w.ReadinessEndpoint))
            .ShouldBe(local.Select(w => (w.ResourceId, w.AppId, w.ReadinessEndpoint)));
        // Existing local behavior still binds actual projects, generates channel credentials and retains its Dapr environment.
        foreach (PlatformServerBinding server in local)
        {
            server.ProjectPath.ShouldNotBeNull();
            server.Environment["Dapr__DeclarativeSubscriptions"].ShouldBe("true");
            foreach (var pair in graph.Dapr.Environment(PlatformDeclarationSet.Id(server.Module.Declaration), server.ServerId))
                server.Environment[pair.Key].ShouldBe(pair.Value);
        }
        local.Single(w => w.AppId == "parties").Environment["APP_API_TOKEN"].ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void HostedGraphDoesNotReadTheLocalProfileAndMatchesTheMaintainedChartInput()
    {
        PlatformCompositionOptions options = PlatformHostedPublishing.ReferenceOptions(Root) with { ComponentsDirectory = "/nonexistent-local-components" };
        string actual = PlatformHostedPublishing.Describe(PlatformApplicationGraph.Resolve(options)).GetRawText();
        string snapshot = File.ReadAllText(Path.Combine(Root, "eng/helm-qualification/chart/reference-graph.json"));
        JsonElement.DeepEquals(JsonDocument.Parse(actual).RootElement, JsonDocument.Parse(snapshot).RootElement).ShouldBeTrue("the maintained chart must use the current canonical metadata");
        actual.ShouldNotContain("localhost:6379");
        actual.ShouldNotContain("secretstores.local.file");
        actual.ShouldNotContain("conversation.echo");
        actual.ShouldNotContain("secretsFile");
        actual.ShouldNotContain("ProjectPath");
    }

    [Fact]
    public void HostedEntryRefusesRunModeBeforeGeneratingLocalResourcesOrSecrets()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = [], AssemblyName = typeof(PlatformAppModel).Assembly.GetName().Name, DisableDashboard = true, AllowUnsecuredTransport = true,
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Platform:Hosted:Enabled"] = "true" });
        Should.Throw<InvalidOperationException>(() => PlatformAppModel.ComposeEntry(builder)).Message.ShouldContain("publish-only");
        builder.Resources.ShouldBeEmpty();
    }

    private static IDistributedApplicationBuilder PublishBuilder()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = ["--publisher", "manifest"], AssemblyName = typeof(PlatformAppModel).Assembly.GetName().Name,
            ProjectDirectory = Root, DisableDashboard = true,
        });
        builder.Configuration["Platform:Hosted:Enabled"] = "true";
        builder.ExecutionContext.IsPublishMode.ShouldBeTrue();
        return builder;
    }

    [Fact]
    public void HostedPublishWritesTheCanonicalGraphAndAddsOnlyNativeWorkloadsAndEnvironment()
    {
        string directory = Path.Combine(Path.GetTempPath(), "hosted-probe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "graph.json");
            IDistributedApplicationBuilder builder = PublishBuilder();
            builder.Configuration["Platform:Hosted:GraphPath"] = path;
            PlatformAppModel.ComposeEntry(builder);
            PlatformApplicationGraph graph = PlatformApplicationGraph.Resolve(PlatformHostedPublishing.ReferenceOptions(Root));
            JsonElement.DeepEquals(JsonDocument.Parse(File.ReadAllText(path)).RootElement, PlatformHostedPublishing.Describe(graph)).ShouldBeTrue();
            builder.Resources.Count.ShouldBe(15); // One environment, seven workloads and their native Dapr sidecar resources.
            builder.Resources.ShouldAllBe(r => r is ContainerResource || r is IDaprSidecarResource || r.Name == "qualification");
            builder.Resources.Single(r => r.Name == "qualification").GetType().Name.ShouldBe("KubernetesEnvironmentResource");
            ContainerResource[] workloads = builder.Resources.OfType<ContainerResource>().ToArray();
            workloads.Select(r => r.Name).Order().ShouldBe(graph.Workloads.Select(w => w.ResourceId).Order());
            foreach (PlatformWorkload workload in graph.Workloads)
            {
                ContainerResource resource = workloads.Single(r => r.Name == workload.ResourceId);
                EndpointAnnotation endpoint = resource.Annotations.OfType<EndpointAnnotation>().Single();
                endpoint.Name.ShouldBe("http");
                endpoint.TargetPort.ShouldBe(8080);
                IDaprSidecarResource sidecar = resource.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar;
                DaprSidecarOptions options = sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options;
                options.AppId.ShouldBe(workload.AppId);
                options.AppPort.ShouldBe(8080);
            }
            Directory.GetFiles(directory).ShouldBe([path]);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void HostedPublishRequiresGraphPathBeforeAddingResources()
    {
        IDistributedApplicationBuilder builder = PublishBuilder();
        Should.Throw<InvalidDataException>(() => PlatformAppModel.ComposeEntry(builder)).Message.ShouldContain("Platform:Hosted:GraphPath is required");
        builder.Resources.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Timesheets")]
    [InlineData("Works")]
    [InlineData("Agents")]
    public void HostedPublishRefusesLocalSelectionsBeforeWritingGraphOrAddingResources(string selection)
    {
        IDistributedApplicationBuilder builder = PublishBuilder();
        builder.Configuration[$"Platform:{selection}:Enabled"] = "true";
        Should.Throw<InvalidDataException>(() => PlatformAppModel.ComposeEntry(builder)).Message.ShouldContain("cannot combine local composition selections");
        builder.Resources.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LocalTimesheetsOptInPrecedesReadinessValidationEvenWithoutEnabledServers(bool disabled)
    {
        JsonNode document = JsonNode.Parse(File.ReadAllText(Path.Combine(Root, "eng/timesheets-deployment/hexalith.module-manifest.v2.json")))!;
        JsonNode module = document["modules"]![0]!;
        module["lifecycle"]!["readiness"] = new JsonArray();
        if (disabled)
            foreach (JsonNode? server in module["identity"]!["servers"]!.AsArray()) server!["enabled"] = false;
        PlatformModuleDeclaration declaration = new("timesheets-fixture", JsonSerializer.SerializeToElement(module));
        PlatformCompositionOptions options = PlatformHostedPublishing.ReferenceOptions(Root);
        Should.Throw<InvalidDataException>(() => PlatformServerBindings.Resolve(options, [declaration])).Message.ShouldContain("explicit Development/source opt-in is required");
    }

    [Fact]
    public void SharedAclUsesTheSelectedHostedNamespaceAndTrustDomain()
    {
        PlatformApplicationGraph graph = PlatformApplicationGraph.Resolve(PlatformHostedPublishing.ReferenceOptions(Root));
        JsonElement acl = graph.Dapr.AccessControl("parties", "qualified", "fixture.invalid");
        acl.GetProperty("defaultAction").GetString().ShouldBe("deny");
        JsonElement[] policies = acl.GetProperty("policies").EnumerateArray().ToArray();
        policies.ShouldNotBeEmpty();
        policies.ShouldAllBe(p => p.GetProperty("namespace").GetString() == "qualified" && p.GetProperty("trustDomain").GetString() == "fixture.invalid");
        policies.Single().GetProperty("operations").GetArrayLength().ShouldBe(13);
    }
}
