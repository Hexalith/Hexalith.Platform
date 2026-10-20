namespace Hexalith.Platform.Composition.Tests;

using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Hexalith.Builds.Tooling.Manifest;
using Hexalith.Builds.Tooling.Workspace;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

[Collection("Platform file entry")]
public sealed class PartiesAdoptionTests
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

    private static PlatformCompositionOptions Options()
    {
        SourceMappingEntry[] entries = new[] { "EventStore", "Tenants", "Memories", "Parties" }
            .Select(name => new SourceMappingEntry("Hexalith." + name, "source", Path.Combine(Root, "references", "Hexalith." + name))).ToArray();
        return new PlatformCompositionOptions
        {
            PlatformRoot = Root,
            Mapping = new SourceMapping(WorkspaceMode.Source, Root, "Hexalith.Platform", entries, false),
            ActiveModule = "parties",
            DeclarationPaths = [Path.Combine(Root, "references/Hexalith.Parties/module/hexalith.module-manifest.v2.json")],
        };
    }

    [Fact]
    public void ActualPartiesDeclarationValidatesAndSelectsSevenMappedServers()
    {
        PlatformCompositionOptions options = Options();
        PlatformManifestValidationResult validation = PlatformManifestValidator.Validate(options.DeclarationPaths, TestContext.Current.CancellationToken);
        validation.IsValid.ShouldBeTrue(string.Join("; ", validation.Diagnostics.Select(diagnostic => diagnostic.Message)));
        IReadOnlyList<PlatformServerBinding> servers = PlatformAppModel.Prepare(options);
        servers.Select(server => server.ResourceId).Order().ShouldBe(new[] { "eventstore", "tenants", "tenants-api", "memories", "memories-access-telemetry-clock", "memories-access-telemetry", "parties" }.Order());
        servers.ShouldAllBe(server => server.ProjectPath != null && server.Image == null && server.ReadinessEndpoint == "/ready");
        PlatformServerBinding parties = servers.Single(server => server.ResourceId == "parties");
        parties.ProjectPath.ShouldBe(Path.Combine(Root, "references/Hexalith.Parties/src/Hexalith.Parties/Hexalith.Parties.csproj"));
        parties.Environment["APP_API_TOKEN"].ShouldBe(parties.Environment["EventStore__AppChannelToken"]);
        parties.Environment["APP_API_TOKEN"].ShouldNotBeNullOrWhiteSpace();
        parties.Environment["Authentication__JwtBearer__SigningKey"].ShouldBe(servers.Single(server => server.ResourceId == "eventstore").Environment["Authentication__JwtBearer__SigningKey"]);
        parties.Environment["Authentication__Workload__Audience"].ShouldBe("parties");
        parties.Environment["Parties__Security__StateStoreName"].ShouldBe("statestore");
        parties.Environment["Parties__MemoriesSearch__MappingStore__StateStoreName"].ShouldBe("statestore");
        parties.Environment["Dapr__DeclarativeSubscriptions"].ShouldBe("true");
        parties.Environment["Tenants__PubSubName"].ShouldBe("pubsub");
        parties.Environment["Tenants__TopicName"].ShouldBe("system.tenants.events");
        parties.Environment["Parties__MemoriesSearch__Enabled"].ShouldBe("false");
        JsonElement surface = parties.Module.Declaration.GetProperty("surfaces").GetProperty("interfaces")[0];
        surface.GetProperty("callers").EnumerateArray().Select(caller => caller.GetString()).ShouldBe(["eventstore"]);
        surface.GetProperty("invocationRules").GetArrayLength().ShouldBe(13);
        surface.GetProperty("invocationRules").EnumerateArray().ShouldAllBe(rule => rule.GetProperty("caller").GetString() == "eventstore" && rule.GetProperty("verbs")[0].GetString() == "POST");
    }

    [Fact]
    public async Task CompositionWiresSingularPartyGatewayAndDeclarativeTenantSubscription()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { Args = [], AssemblyName = typeof(PlatformAppModel).Assembly.GetName().Name, DisableDashboard = true, AllowUnsecuredTransport = true });
        PlatformAppModel.Compose(builder, Options());
        ProjectResource[] servers = builder.Resources.OfType<ProjectResource>().Where(resource => resource.Annotations.OfType<PlatformServerAnnotation>().Any()).ToArray();
        servers.Length.ShouldBe(7);
        servers.Select(server => server.Name).ShouldNotContain("parties-ui");
        servers.Select(server => server.Name).ShouldNotContain("parties-mcp");
        ProjectResource gateway = servers.Single(server => server.Name == "eventstore");
        Dictionary<string, object> environment = new();
        foreach (EnvironmentCallbackAnnotation callback in gateway.Annotations.OfType<EnvironmentCallbackAnnotation>())
            await callback.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, gateway, environment, TestContext.Current.CancellationToken));
        environment["EventStore__DomainServices__Registrations__wildcard_party_v1__AppId"].ShouldBe("parties");
        environment["EventStore__DomainServices__Registrations__wildcard_party_v1__Domain"].ShouldBe("party");
        environment["EventStore__DomainServices__Registrations__wildcard_party_v1__MethodName"].ShouldBe("process");
        ProjectResource parties = servers.Single(server => server.Name == "parties");
        DaprSidecarOptions sidecar = parties.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options;
        string[] subscriptions = sidecar.ResourcesPaths!.SelectMany(directory => Directory.GetFiles(directory, "subscription.*")).Select(File.ReadAllText).ToArray();
        JsonElement[] partySubscriptions = subscriptions.Select(content =>
        {
            using JsonDocument document = JsonDocument.Parse(content);
            return document.RootElement.Clone();
        }).Where(subscription => subscription.GetProperty("scopes").EnumerateArray().Any(scope => scope.GetString() == "parties")).ToArray();
        JsonElement tenantSubscription = partySubscriptions.ShouldHaveSingleItem();
        tenantSubscription.GetProperty("apiVersion").GetString().ShouldBe("dapr.io/v2alpha1");
        tenantSubscription.GetProperty("kind").GetString().ShouldBe("Subscription");
        tenantSubscription.GetProperty("scopes").EnumerateArray().Select(scope => scope.GetString()).ShouldBe(["parties"]);
        JsonElement specification = tenantSubscription.GetProperty("spec");
        specification.GetProperty("pubsubname").GetString().ShouldBe("pubsub");
        specification.GetProperty("topic").GetString().ShouldBe("system.tenants.events");
        specification.GetProperty("routes").EnumerateObject().Select(route => (route.Name, route.Value.GetString()))
            .ShouldBe([("default", "/tenants/events")]);
        specification.TryGetProperty("deadLetterTopic", out _).ShouldBeFalse();
        PlatformServerBinding tenants = servers.Single(server => server.Name == "tenants").Annotations.OfType<PlatformServerAnnotation>().Single().Binding;
        JsonElement[] rules = tenants.Module.Declaration.GetProperty("surfaces").GetProperty("interfaces").EnumerateArray().Where(surface => surface.TryGetProperty("invocationRules", out _)).SelectMany(surface => surface.GetProperty("invocationRules").EnumerateArray()).Where(rule => rule.GetProperty("caller").GetString() == "parties").ToArray();
        rules.Length.ShouldBe(1);
        rules[0].GetProperty("path").GetString().ShouldBe("/ready");
        rules[0].GetProperty("verbs").EnumerateArray().Select(verb => verb.GetString()).ShouldBe(["GET"]);
        await using DistributedApplication application = builder.Build();
    }

    [Fact]
    public void MissingOrOverriddenPartiesSourceRefusesBeforeComposition()
    {
        PlatformCompositionOptions options = Options();
        SourceMappingEntry[] missing = options.Mapping.Entries.Where(entry => entry.Identity != "Hexalith.Parties").ToArray();
        Should.Throw<InvalidDataException>(() => PlatformAppModel.Prepare(options with { Mapping = options.Mapping with { Entries = missing } })).Message.ShouldContain("parties");
        Dictionary<string, string> projects = new(PlatformServerBindings.ReferenceProjects) { ["parties/domain"] = "../Hexalith.Tenants/src/Hexalith.Tenants/Hexalith.Tenants.csproj" };
        Should.Throw<InvalidDataException>(() => PlatformAppModel.Prepare(options with { Projects = projects })).Message.ShouldContain("outside");
    }
}
