namespace Hexalith.Platform.Composition.Tests;

using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Hexalith.Builds.Tooling.Manifest;
using Hexalith.Builds.Tooling.Runtime;
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
    public void ActualPartiesDeclarationValidatesAndSelectsEightMappedServers()
    {
        PlatformCompositionOptions options = Options();
        PlatformManifestValidationResult validation = PlatformManifestValidator.Validate(options.DeclarationPaths, TestContext.Current.CancellationToken);
        validation.IsValid.ShouldBeTrue(string.Join("; ", validation.Diagnostics.Select(diagnostic => diagnostic.Message)));
        IReadOnlyList<PlatformServerBinding> servers = PlatformAppModel.Prepare(options);
        servers.Select(server => server.ResourceId).Order().ShouldBe(new[] { "eventstore", "tenants", "tenants-api", "memories", "memories-domain-service", "memories-access-telemetry-clock", "memories-access-telemetry", "parties" }.Order());
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
        servers.Length.ShouldBe(8);
        servers.Select(server => server.Name).ShouldNotContain("parties-ui");
        servers.Select(server => server.Name).ShouldNotContain("parties-mcp");
        ProjectResource gateway = servers.Single(server => server.Name == "eventstore");
        Dictionary<string, object> environment = new();
        foreach (EnvironmentCallbackAnnotation callback in gateway.Annotations.OfType<EnvironmentCallbackAnnotation>())
            await callback.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, gateway, environment, TestContext.Current.CancellationToken));
        environment["EventStore__DomainServices__Registrations__wildcard_party_v1__AppId"].ShouldBe("parties");
        environment["EventStore__DomainServices__Registrations__wildcard_party_v1__Domain"].ShouldBe("party");
        environment["EventStore__DomainServices__Registrations__wildcard_party_v1__MethodName"].ShouldBe("process");
        environment["EventStore__DomainServices__Registrations__wildcard_party_v1__TenantId"].ShouldBe("*");
        environment["EventStore__DomainServices__Registrations__wildcard_party_v1__Version"].ShouldBe("v1");
        ProjectResource parties = servers.Single(server => server.Name == "parties");
        DaprSidecarOptions sidecar = parties.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options;
        using JsonDocument accessControl = JsonDocument.Parse(File.ReadAllText(sidecar.Config!));
        JsonElement receiver = accessControl.RootElement.GetProperty("spec");
        receiver.GetProperty("mtls").GetProperty("enabled").GetBoolean().ShouldBeTrue();
        JsonElement acl = receiver.GetProperty("accessControl");
        acl.GetProperty("defaultAction").GetString().ShouldBe("deny");
        acl.GetProperty("trustDomain").GetString().ShouldBe("public");
        JsonElement policy = acl.GetProperty("policies").EnumerateArray().ShouldHaveSingleItem();
        policy.GetProperty("appId").GetString().ShouldBe("eventstore");
        policy.GetProperty("defaultAction").GetString().ShouldBe("deny");
        policy.GetProperty("trustDomain").GetString().ShouldBe("public");
        policy.GetProperty("namespace").GetString().ShouldBe("default");
        JsonElement[] operations = policy.GetProperty("operations").EnumerateArray().ToArray();
        // The existing Dapr prefix-node fallback renders approved parent paths with /*.
        operations.Select(operation => operation.GetProperty("name").GetString()).Order().ShouldBe(new[]
        {
            "/process", "/query", "/admin/operational-index-metadata", "/project/*",
            "/project/v2/*", "/project/v2/reconcile", "/replay-state", "/project/rebuild/v1",
            "/project/rebuild/shared/v1", "/project/rebuild/stage/v1", "/project/rebuild/commit/v1",
            "/project/rebuild/abort/v1", "/project/rebuild/verify/v1",
        }.Order());
        foreach (JsonElement operation in operations)
        {
            operation.GetProperty("httpVerb").EnumerateArray().Select(verb => verb.GetString()).ShouldBe(["POST"]);
            operation.GetProperty("action").GetString().ShouldBe("allow");
        }
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

    /// <summary>Verifies the declared source suite uses only its runner-supplied, mapped Builds host.</summary>
    [Fact]
    public void NativeSourceSuiteRequiresTheExactMappedRunnerSeed()
    {
        PlatformCompositionOptions options = Options();
        string buildsRoot = Path.Combine(Root, "references", "Hexalith.Builds");
        string seed = Path.Combine(buildsRoot, "src", "hosts", "Hexalith.Builds.Module.EventStoreHost", "Hexalith.Builds.Module.EventStoreHost.csproj");
        SourceMapping mapping = options.Mapping with { Entries = [.. options.Mapping.Entries, new("Hexalith.Builds", "source", buildsRoot)] };
        DateTimeOffset now = DateTimeOffset.UtcNow;
        CompositionRunPlan plan = CompositionRunPlanFactory.Create(CompositionRunPlanFactory.NewRunId(), Path.GetTempPath(), Path.GetTempPath(),
            CompositionPortAllocator.Allocate(1), [], [], mapping, options.DeclarationPaths, "parties", typeof(PlatformAppModel).Assembly.Location) with
        {
            SelectedTest = new("parties", "platform-source", "dotnet", "tests/native", new string('a', 64), new string('b', 64), 120, 10, now, now.AddMinutes(2), now.AddMinutes(2), "source"),
        };
        options = options with { Mapping = mapping, RunPlan = plan, EventStoreSeedProject = seed };
        PlatformAppModel.Prepare(options).Single(server => server.ResourceId == "eventstore").ProjectPath.ShouldBe(seed);
        Should.Throw<InvalidDataException>(() => PlatformAppModel.Prepare(options with { EventStoreSeedProject = null })).Message.ShouldContain("seed");
        Should.Throw<InvalidDataException>(() => PlatformAppModel.Prepare(options with
        {
            EventStoreSeedProject = Path.Combine(Root, "src", "Hexalith.Platform.EventStoreHost", "Hexalith.Platform.EventStoreHost.csproj"),
        })).Message.ShouldContain("tool-selected Builds host");
        Should.Throw<InvalidDataException>(() => PlatformAppModel.Prepare(options with { Mapping = mapping with { Entries = options.Mapping.Entries.Where(entry => entry.Identity != "Hexalith.Builds").ToArray() } }))
            .Message.ShouldContain("Builds source identity");
    }

    [Fact]
    public async Task SignedPartiesRunUsesTheRunnerJwtSettingsAsync()
    {
        string workspace = Path.Combine(Path.GetTempPath(), "parties-signed-run-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        try
        {
            PlatformCompositionOptions options = Options();
            SourceMapping mapping = options.Mapping with { Entries = [.. options.Mapping.Entries, new SourceMappingEntry("Hexalith.Platform", "source", Root)] };
            CompositionRunPlan plan = CompositionRunPlanFactory.Create(CompositionRunPlanFactory.NewRunId(), workspace, workspace,
                CompositionPortAllocator.Allocate(1), [], [], mapping, options.DeclarationPaths, "parties", typeof(PlatformAppModel).Assembly.Location);
            await CompositionDaprComponentRenderer.WriteAsync(plan, TestContext.Current.CancellationToken);
            IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { Args = [], AssemblyName = typeof(PlatformAppModel).Assembly.GetName().Name, DisableDashboard = true, AllowUnsecuredTransport = true });
            string signingKey = CompositionSigningKey.Create();
            PlatformAppModel.ComposeRun(builder, plan, signingKey, seedProject: null);
            foreach (string resourceId in new[] { "parties", "eventstore" })
            {
                ProjectResource resource = builder.Resources.OfType<ProjectResource>().Single(server => server.Name == resourceId);
                Dictionary<string, object> environment = new();
                foreach (EnvironmentCallbackAnnotation callback in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
                    await callback.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, resource, environment, TestContext.Current.CancellationToken));
                environment["Authentication__JwtBearer__SigningKey"].ShouldBe(signingKey, resourceId);
                environment["Authentication__JwtBearer__Issuer"].ShouldBe(CompositionEnvironment.TokenIssuer, resourceId);
                environment["Authentication__JwtBearer__Audience"].ShouldBe(CompositionEnvironment.TokenAudience, resourceId);
                environment.Where(pair => pair.Key.StartsWith("Authentication__JwtBearer__ValidAudiences__", StringComparison.Ordinal))
                    .Select(pair => (pair.Key, pair.Value)).ShouldBe([("Authentication__JwtBearer__ValidAudiences__0", (object)CompositionEnvironment.TokenAudience)]);
                environment[CompositionEnvironment.RunId].ShouldBe(plan.RunId);
            }
            await using DistributedApplication application = builder.Build();
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
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

    [Theory]
    [InlineData("Hexalith.Parties")]
    [InlineData("renamed-parties-checkout")]
    public async Task ResolvedStandaloneRootsBindTheirActualPartiesProjectAsync(string name)
    {
        string parent = Path.Combine(Path.GetTempPath(), "parties-resolved-root-" + Guid.NewGuid().ToString("N"));
        using PlatformCompositionWorkspace cleanup = new(parent);
        using PlatformOwnerFixture owner = PlatformOwnerFixture.Copy(Root, "parties", Path.Combine(parent, name));
        SourceMapping mapping = await WorkspaceRootResolver.ResolveAsync(owner.Manifest, WorkspaceMode.Source, TestContext.Current.CancellationToken);
        mapping.ActiveModule.ShouldBe("Hexalith.Parties");
        mapping.Entries.ShouldBe([new SourceMappingEntry("Hexalith.Parties", "source", owner.Root)]);
        PlatformManifestValidationResult validated = PlatformManifestValidator.Validate([owner.Manifest], TestContext.Current.CancellationToken);
        validated.IsValid.ShouldBeTrue(string.Join("; ", validated.Diagnostics.Select(diagnostic => diagnostic.Message)));

        PlatformServerBinding binding = PlatformServerBindings.Resolve(Options() with { Mapping = mapping }, validated.Declarations!).Single();

        binding.ProjectPath.ShouldBe(Path.Combine(owner.Root, PlatformServerBindings.ReferenceProjects["parties/domain"]));
        File.Exists(binding.ProjectPath).ShouldBeTrue();
    }
}
