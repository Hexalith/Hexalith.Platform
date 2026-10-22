namespace Hexalith.Platform.Composition.Tests;

using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
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
