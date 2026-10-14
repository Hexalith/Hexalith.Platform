namespace Hexalith.Platform.Composition.Tests;

using System.Text.Json.Nodes;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Hexalith.Builds.Tooling.Workspace;
using Hexalith.Builds.Tooling.Runtime;
using Shouldly;
using Microsoft.Extensions.Configuration;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.ServiceDefaults.Authentication;

[Collection("Platform file entry")]
public sealed partial class PlatformAppModelTests
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

    private static PlatformCompositionOptions Options(WorkspaceMode mode = WorkspaceMode.Source, string? active = null)
    {
        SourceMappingEntry[] entries = new[] { "EventStore", "Tenants", "Memories" }
            .Select(name => new SourceMappingEntry("Hexalith." + name, mode == WorkspaceMode.Source ? "source" : "package", mode == WorkspaceMode.Source ? Path.Combine(Root, "references", "Hexalith." + name) : null)).ToArray();
        return new PlatformCompositionOptions
        {
            PlatformRoot = Root, Mapping = new SourceMapping(mode, Root, "Hexalith.Platform", entries, false), ActiveModule = active,
            EventStoreSeedProject = Path.Combine(Root, "references", "Hexalith.Builds", "src", "hosts", "Hexalith.Builds.Module.EventStoreHost", "Hexalith.Builds.Module.EventStoreHost.csproj"),
            ReleaseCommits = mode == WorkspaceMode.Package ? new Dictionary<string, string>
            {
                ["eventstore/3.120.0"] = "947c843c7bb73f217e27463d9540fde0c24d0332",
                ["tenants/5.8.0"] = "6f88e4e95ae464617766f6a1294ff08a4504f42b",
                ["memories/2.27.1"] = "867e09a357ab94719e4d1572b1ab14d8d124eabe",
            } : new Dictionary<string, string>(),
            ReleaseSnapshotHashes = mode == WorkspaceMode.Package ? new Dictionary<string, string>
            {
                ["eventstore/3.120.0"] = "c76853a3fd0d03f345929927f94cafcdf71714fdc20192bc2717a5fda8501a09",
                ["tenants/5.8.0"] = "e79a87df18b8ca40eaa89c94a2534b14e97fa00ecdd4c3a91b7bcc7405a245d4",
                ["memories/2.27.1"] = "30f97d509aeb9fa019373579ad24db113aa84a9df52139a88a0cfdbaa6b192d4",
            } : new Dictionary<string, string>(),
        };
    }

    [Fact]
    public void PackageModeRefusesUnreleasedOwnerSnapshotBeforeAddingResources()
    {
        IDistributedApplicationBuilder builder = Builder();
        InvalidDataException refusal = Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder, Options(WorkspaceMode.Package) with { ReleaseCommits = new PlatformCompositionOptions { PlatformRoot = Root, Mapping = Options().Mapping }.ReleaseCommits }));
        refusal.Message.ShouldContain("Hexalith.EventStore");
        refusal.Message.ShouldContain("3.120.0");
        refusal.Message.ShouldContain("947c843c");
        builder.Resources.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null, 6)]
    [InlineData("eventstore", 1)]
    [InlineData("memories", 4)]
    [InlineData("tenants", 6)]
    public void SelectsOnlyTheDeclaredDependencyClosure(string? active, int expected)
    {
        IReadOnlyList<PlatformServerBinding> servers = PlatformAppModel.Prepare(Options(active: active));
        servers.Count.ShouldBe(expected);
        servers.Count(s => s.ResourceId == "eventstore").ShouldBe(1);
        servers.ShouldAllBe(s => s.ReadinessEndpoint == "/ready" && s.Tasks.Count == 0);
        servers.ShouldAllBe(s => s.ProjectPath != null && s.Image == null);
    }

    [Fact]
    public void PackageModeUsesSnapshotsCatalogImagesAndTheSingleSeed()
    {
        PlatformCompositionOptions options = Options(WorkspaceMode.Package);
        IReadOnlyList<PlatformServerBinding> servers = PlatformAppModel.Prepare(options);
        servers.Count.ShouldBe(6);
        servers.Single(s => s.ResourceId == "eventstore").ProjectPath.ShouldBe(options.EventStoreSeedProject);
        servers.Single(s => s.ResourceId == "tenants").Image.ShouldBe("registry.hexalith.com/tenants:5.8.0");
        servers.Single(s => s.ResourceId == "memories").Image.ShouldBe("registry.hexalith.com/memories:2.27.1");
        servers.Where(s => s.ResourceId != "eventstore").ShouldAllBe(s => s.ProjectPath == null);
        options.Mapping.Entries.ShouldAllBe(e => e.Path == null);
    }

    [Fact]
    public async Task PackageCompositionAddsCatalogContainersWithDeclaredSidecarsAndScopedBindingsAsync()
    {
        List<string> inspected = [];
        PlatformCompositionOptions options = Options(WorkspaceMode.Package) with
        {
            Configuration = new Dictionary<string, string> { ["tenants/api/Tenants:ApiDaprToken"] = "api-scoped-token", ["tenants/domain/Tenants:AppChannelToken"] = "domain-scoped-token" },
            ImageProbeRunner = (process, timeout, _) =>
            {
                process.FileName.ShouldBe("docker");
                process.ArgumentList.Take(2).ShouldBe(["manifest", "inspect"]);
                timeout.ShouldBe(TimeSpan.FromSeconds(20));
                inspected.Add(process.ArgumentList[2]);
                return Task.FromResult(new CompositionProcessResult(true, 0, "{}", false));
            },
        };
        IDistributedApplicationBuilder builder = Builder();
        PlatformAppModel.Compose(builder, options);
        IResource[] servers = builder.Resources.Where(resource => resource.Annotations.OfType<PlatformServerAnnotation>().Any()).ToArray();
        servers.Length.ShouldBe(6);
        servers.OfType<ProjectResource>().Single().Name.ShouldBe("eventstore");
        servers.OfType<ProjectResource>().Single().GetProjectMetadata().ProjectPath.ShouldBe(options.EventStoreSeedProject);
        servers.OfType<ContainerResource>().Count().ShouldBe(5);
        inspected.Order().ShouldBe(servers.Select(resource => resource.Annotations.OfType<PlatformServerAnnotation>().Single().Binding.Image).OfType<string>().Order());
        foreach (IResource server in servers)
        {
            PlatformServerBinding binding = server.Annotations.OfType<PlatformServerAnnotation>().Single().Binding;
            server.Annotations.OfType<HealthCheckAnnotation>().Single().Key.ShouldContain("/ready");
            DaprSidecarOptions sidecar = server.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options;
            sidecar.AppId.ShouldBe(binding.AppId);
            sidecar.DaprHttpPort.GetValueOrDefault().ShouldBeGreaterThan(0);
            sidecar.DaprGrpcPort.GetValueOrDefault().ShouldBeGreaterThan(0);
            sidecar.DaprInternalGrpcPort.GetValueOrDefault().ShouldBeGreaterThan(0);
            sidecar.MetricsPort.GetValueOrDefault().ShouldBeGreaterThan(0);
            Dictionary<string, object> environment = new();
            foreach (EnvironmentCallbackAnnotation callback in server.Annotations.OfType<EnvironmentCallbackAnnotation>())
                await callback.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, server, environment, TestContext.Current.CancellationToken));
            foreach (KeyValuePair<string, string> input in binding.Environment)
                if (input.Key is "ConnectionStrings__redis" or "ConnectionStrings__falkordb") environment[input.Key].ShouldBeOfType<ReferenceExpression>();
                else if (input.Key == "AccessTelemetryLifecycle__AttestationVerificationKey") environment[input.Key].ShouldBeOfType<string>().ShouldNotBeNullOrWhiteSpace();
                else environment[input.Key].ShouldBe(input.Value);
            if (server.Name == "tenants-api") { environment["DAPR_API_TOKEN"].ShouldBe("api-scoped-token"); environment.ShouldNotContainKey("APP_API_TOKEN"); }
            if (server.Name == "tenants") environment["APP_API_TOKEN"].ShouldBe("domain-scoped-token");
            if (server is ContainerResource container)
            {
                ContainerImageAnnotation image = container.Annotations.OfType<ContainerImageAnnotation>().Single();
                (image.Image + ":" + image.Tag).ShouldBe(binding.Image);
                container.Annotations.OfType<EndpointAnnotation>().Single(endpoint => endpoint.Name == "http").TargetPort.ShouldBe(8080);
            }
        }
        DaprSidecarOptions[] sidecars = servers.Select(server => server.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options).ToArray();
        sidecars.SelectMany(sidecar => new[] { sidecar.DaprHttpPort, sidecar.DaprGrpcPort, sidecar.DaprInternalGrpcPort, sidecar.MetricsPort }).Distinct().Count().ShouldBe(24);
        builder.Resources.ShouldNotContain(resource => resource.Name.Contains("admin") || resource.Name.Contains("mcp") || resource.Name == "eventstore-2");
    }

    [Fact]
    public void GraphHasSixDeclaredServersAndNoOptionalOrLegacyHosts()
    {
        IDistributedApplicationBuilder builder = Builder();
        PlatformAppModel.Compose(builder, Options());
        IResource[] servers = builder.Resources.Where(r => r.Annotations.OfType<PlatformServerAnnotation>().Any()).ToArray();
        servers.Length.ShouldBe(6);
        servers.Count(r => r.Name == "eventstore").ShouldBe(1);
        servers.ShouldAllBe(r => r.Annotations.OfType<PlatformServerAnnotation>().Single().Binding.ReadinessEndpoint == "/ready");
        foreach (IResource server in servers) server.Annotations.OfType<HealthCheckAnnotation>().Single().Key.ShouldContain("/ready");
        foreach (IResource server in servers)
            server.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options.EnableAppHealthCheck.ShouldNotBe(true);
        builder.Resources.ShouldNotContain(r => r.Name.Contains("admin") || r.Name.Contains("mcp") || r.Name == "eventstore-2");
        servers.OfType<ProjectResource>().ShouldAllBe(r => r.Annotations.OfType<ExecutableAnnotation>().Last().Command == "dotnet");
        builder.Services[0].ServiceType.ShouldBe(typeof(IHostedService));
        using ServiceProvider provider = builder.Services.BuildServiceProvider();
        using PlatformGatewayStartup startup = builder.Services[0].ImplementationFactory!(provider).ShouldBeOfType<PlatformGatewayStartup>();
    }

    [Fact]
    public async Task ActualDaprCliListenersAndTokensRemainServerScopedAsync()
    {
        IDistributedApplicationBuilder builder = Builder();
        // Model lifecycle callbacks need discovery metadata, but none of these executables is started.
        builder.Configuration["DcpPublisher:CliPath"] = Path.Combine(Root, "test-dcp-not-started");
        builder.Configuration["DcpPublisher:DashboardPath"] = Path.Combine(Root, "test-dashboard-not-started");
        builder.AddDapr(options => { options.EnableTelemetry = false; options.DaprPath = Path.Combine(Root, "test-dapr-not-started"); });
        PlatformAppModel.Compose(builder, Options() with { Configuration = new Dictionary<string, string> { ["tenants/api/Tenants:ApiDaprToken"] = "api-only", ["tenants/domain/Tenants:AppChannelToken"] = "domain-only" } });
        await using DistributedApplication app = builder.Build();
        _ = app.Services.GetRequiredService<PlatformCompositionWorkspace>();
        DistributedApplicationModel model = app.Services.GetRequiredService<DistributedApplicationModel>();
        model.Resources.OfType<ExecutableResource>().Where(resource => resource.Name.EndsWith("-dapr-cli", StringComparison.Ordinal)).ShouldBeEmpty();
        IReadOnlyList<PlatformServerBinding> servers = model.Resources.SelectMany(resource => resource.Annotations.OfType<PlatformServerAnnotation>()).Select(annotation => annotation.Binding).ToArray();
        IDistributedApplicationEventing eventing = app.Services.GetRequiredService<IDistributedApplicationEventing>();
        IDistributedApplicationEventingSubscriber[] subscribers = app.Services.GetServices<IDistributedApplicationEventingSubscriber>().ToArray();
        subscribers.ShouldContain(subscriber => subscriber is PlatformDaprEndpointSubscriber);
        subscribers.ShouldContain(subscriber => subscriber.GetType().Name == "DaprDistributedApplicationLifecycleHook");
        foreach (IDistributedApplicationEventingSubscriber subscriber in subscribers)
            await subscriber.SubscribeAsync(eventing, builder.ExecutionContext, TestContext.Current.CancellationToken);
        await eventing.PublishAsync(new BeforeStartEvent(app.Services, model), TestContext.Current.CancellationToken);
        model.Resources.OfType<ExecutableResource>().Count(resource => resource.Name.EndsWith("-dapr-cli", StringComparison.Ordinal)).ShouldBe(6);
        foreach (PlatformServerBinding server in servers)
        {
            ExecutableResource sidecar = model.Resources.OfType<ExecutableResource>().Single(resource => resource.Name == server.ResourceId + "-dapr-cli");
            IResource application = model.Resources.Single(resource => resource.Name == server.ResourceId);
            DaprSidecarOptions options = application.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options;
            foreach ((string name, int? expected) in new[] { ("http", options.DaprHttpPort), ("grpc", options.DaprGrpcPort), ("metrics", options.MetricsPort) })
            {
                expected.ShouldNotBeNull($"{server.ResourceId} must declare a fixed {name} listener port.");
                EndpointAnnotation endpoint = sidecar.Annotations.OfType<EndpointAnnotation>().Single(endpoint => endpoint.Name == name);
                endpoint.Port.ShouldBe(expected);
                endpoint.TargetPort.ShouldBe(expected);
                endpoint.IsProxied.ShouldBeFalse();
            }
            Dictionary<string, object> environment = new();
            foreach (EnvironmentCallbackAnnotation callback in sidecar.Annotations.OfType<EnvironmentCallbackAnnotation>())
                await callback.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, sidecar, environment, TestContext.Current.CancellationToken));
            foreach (string token in new[] { "APP_API_TOKEN", "DAPR_API_TOKEN" })
                if (server.Environment.TryGetValue(token, out string? value)) environment[token].ShouldBe(value);
                else environment.ShouldNotContainKey(token);
            if (server.ResourceId == "eventstore")
                environment["APP_API_TOKEN"].ShouldBe(server.Environment["EventStore__AppChannelToken"]);
            if (server.ResourceId is "memories" or "memories-access-telemetry-clock" or "memories-access-telemetry")
            {
                environment["APP_API_TOKEN"].ShouldBe(server.Environment["Memories__AppChannelToken"]);
                environment["DAPR_API_TOKEN"].ShouldBe(server.Environment["Memories__DaprApiToken"]);
            }
            if (server.ResourceId == "tenants") environment["APP_API_TOKEN"].ShouldBe("domain-only");
            if (server.ResourceId == "tenants-api") environment["DAPR_API_TOKEN"].ShouldBe("api-only");
        }
    }

    [Fact]
    public void GatewaySettingsContainOnlySelectedAppRegistrations()
    {
        string directory = Path.Combine(Path.GetTempPath(), "gateway-config-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            IReadOnlyList<PlatformServerBinding> servers = PlatformAppModel.Prepare(Options());
            string content = PlatformGatewayConfiguration.Create(servers.Single(server => server.ResourceId == "eventstore").ProjectPath!, directory, servers);
            JsonObject registrations = JsonNode.Parse(File.ReadAllText(Path.Combine(content, "appsettings.Development.json")))!["EventStore"]!["DomainServices"]!["Registrations"]!.AsObject();
            registrations.ShouldNotBeEmpty();
            registrations.ShouldAllBe(pair => pair.Value!["AppId"]!.GetValue<string>() == "tenants");
            IReadOnlyList<PlatformServerBinding> gatewayOnly = PlatformAppModel.Prepare(Options(active: "eventstore"));
            content = PlatformGatewayConfiguration.Create(gatewayOnly.Single().ProjectPath!, directory, gatewayOnly);
            JsonNode.Parse(File.ReadAllText(Path.Combine(content, "appsettings.Development.json")))!["EventStore"]!["DomainServices"]!["Registrations"]!.AsObject().ShouldBeEmpty();
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void WorksPreviewPreservesItsMissingSiblingRefusal()
    {
        string directory = Path.Combine(Path.GetTempPath(), "works-route-test-" + Guid.NewGuid().ToString("N"));
        string platform = Path.Combine(directory, "platform");
        Directory.CreateDirectory(platform);
        try
        {
            IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { Args = [], AssemblyName = typeof(PlatformAppModel).Assembly.GetName().Name, ProjectDirectory = platform, DisableDashboard = true });
            FileNotFoundException missing = Should.Throw<FileNotFoundException>(() => WorksPreviewModel.Compose(builder));
            missing.Message.ShouldContain("requires the sibling Works source checkout");
            missing.FileName.ShouldBe(Path.Combine(directory, "works", "src", "Hexalith.Works", "Hexalith.Works.csproj"));
            builder.Resources.ShouldBeEmpty();
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void WorksPreviewComposesItsPreservedGraphWithoutDeclarationServers()
    {
        string directory = Path.Combine(Path.GetTempPath(), "works-graph-test-" + Guid.NewGuid().ToString("N"));
        string platform = Path.Combine(directory, "platform");
        Directory.CreateDirectory(platform);
        try
        {
            File.WriteAllText(Path.Combine(platform, "apphost.cs"), "// fixture");
            File.WriteAllText(Path.Combine(platform, "aspire.config.json"), "{}");
            string[] projects = ["src/Hexalith.Works/Hexalith.Works.csproj", "references/Hexalith.EventStore/src/Hexalith.EventStore/Hexalith.EventStore.csproj", "references/Hexalith.EventStore/src/Hexalith.EventStore.Admin.Server.Host/Hexalith.EventStore.Admin.Server.Host.csproj", "references/Hexalith.EventStore/src/Hexalith.EventStore.Operations/Hexalith.EventStore.Operations.csproj"];
            foreach (string relative in projects)
            {
                string path = Path.Combine(directory, "works", relative);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
                string properties = Path.Combine(Path.GetDirectoryName(path)!, "Properties");
                Directory.CreateDirectory(properties);
                File.WriteAllText(Path.Combine(properties, "launchSettings.json"), "{\"profiles\":{\"http\":{\"commandName\":\"Project\",\"applicationUrl\":\"http://localhost:5010\"}}}");
            }
            foreach (string relative in new[] { "accesscontrol.yaml", "accesscontrol.works.yaml", "accesscontrol.eventstore-admin.yaml", "accesscontrol.eventstore-operations.yaml", "resiliency/resiliency.yaml", "statestore.yaml", "pubsub.yaml", "sentry.yaml" })
            {
                string target = Path.Combine(platform, "DaprComponents", relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(Path.Combine(Root, "DaprComponents", relative), target);
            }
            IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { Args = [], AssemblyName = typeof(PlatformAppModel).Assembly.GetName().Name, ProjectDirectory = platform, DisableDashboard = true });
            builder.Environment.EnvironmentName = Environments.Development;
            builder.Configuration["Dapr:Mtls:CertificateDirectory"] = Path.Combine(directory, "certificates");
            builder.Configuration["Platform:Works:Enabled"] = "true";
            builder.Configuration["Platform:Timesheets:Enabled"] = "false";
            PlatformAppModel.ComposeEntry(builder);
            builder.Resources.OfType<ProjectResource>().Select(resource => resource.Name).Order().ShouldBe(new[] { "eventstore", "eventstore-admin", "eventstore-operations", "works" }.Order());
            builder.Resources.ShouldNotContain(resource => resource.Annotations.OfType<PlatformServerAnnotation>().Any() || resource.Name.StartsWith("tenants", StringComparison.Ordinal) || resource.Name.StartsWith("memories", StringComparison.Ordinal));
            builder.Resources.OfType<ProjectResource>().ShouldAllBe(resource => resource.Annotations.OfType<DaprSidecarAnnotation>().Count() == 1);
            builder.Resources.ShouldContain(resource => resource.Name == "dapr-sentry");
            builder.Resources.ShouldContain(resource => resource.Name == "dapr-placement-mtls");
            builder.Resources.ShouldContain(resource => resource.Name == "dapr-scheduler-mtls");
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void MissingMappedProjectFailsBeforeAddingResources()
    {
        PlatformCompositionOptions options = Options() with { Projects = new Dictionary<string, string>() };
        IDistributedApplicationBuilder builder = Builder();
        InvalidDataException failure = Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder, options));
        failure.Message.ShouldContain("eventstore/gateway");
        failure.Message.ShouldContain("project");
        builder.Resources.ShouldBeEmpty();
    }

    [Fact]
    public void MissingImageFailsBeforeAddingResources()
    {
        PlatformCompositionOptions options = Options(WorkspaceMode.Package) with { Images = new Dictionary<string, string>() };
        IDistributedApplicationBuilder builder = Builder();
        Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder, options)).Message.ShouldContain("released image is unavailable");
        builder.Resources.ShouldBeEmpty();
    }

    [Fact]
    public void MissingSnapshotFailsByOwnerWithoutReadingSourceReferences()
    {
        IDistributedApplicationBuilder builder = Builder();
        PlatformCompositionOptions options = Options(WorkspaceMode.Package) with { PlatformRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")) };
        InvalidDataException failure = Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder, options));
        failure.Message.ShouldContain("Hexalith.EventStore");
        failure.Message.ShouldContain("release snapshot");
        builder.Resources.ShouldBeEmpty();
    }

    [Fact]
    public void MissingSourceDeclarationFailsByIdentityWithoutFallback()
    {
        PlatformCompositionOptions original = Options();
        PlatformCompositionOptions options = original with { Mapping = original.Mapping with { Entries = original.Mapping.Entries.Where(e => e.Identity != "Hexalith.Memories").ToArray() } };
        IDistributedApplicationBuilder builder = Builder();
        Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder, options)).Message.ShouldContain("Hexalith.Memories");
        builder.Resources.ShouldBeEmpty();
    }

    [Fact]
    public void DuplicateOrInvalidDeclarationIsAtomicAndReportsSourceFieldAndReason()
    {
        IDistributedApplicationBuilder builder = Builder();
        string duplicate = Path.Combine(Root, "src", "Hexalith.Platform.Composition", "Declarations", "eventstore.json");
        InvalidDataException failure = Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder, Options() with { DeclarationPaths = [duplicate] }));
        failure.Message.ShouldContain("eventstore.json");
        failure.Message.ShouldContain("moduleId");
        failure.Message.ShouldContain("duplicated");
        builder.Resources.ShouldBeEmpty();
    }

    [Fact]
    public void ConfigurationUsesFullKeysAndServerScopedTokenMappings()
    {
        PlatformCompositionOptions options = Options(active: "tenants") with
        {
            Configuration = new Dictionary<string, string>
            {
                ["tenants/api/EventStore:Authentication:Authority"] = "https://delegated.example",
                ["tenants/api/Authentication:JwtBearer:Authority"] = "https://validation.example",
                ["tenants/domain/Tenants:AppChannelToken"] = "domain-token",
                ["tenants/api/Tenants:ApiDaprToken"] = "api-token",
            },
        };
        IReadOnlyList<PlatformServerBinding> servers = PlatformAppModel.Prepare(options);
        PlatformServerBinding api = servers.Single(s => s.ResourceId == "tenants-api");
        api.Environment["EventStore__Authentication__Authority"].ShouldBe("https://delegated.example");
        api.Environment["Authentication__JwtBearer__Authority"].ShouldBe("https://validation.example");
        api.Environment["DAPR_API_TOKEN"].ShouldBe("api-token");
        api.Environment.ShouldNotContainKey("APP_API_TOKEN");
        servers.Single(s => s.ResourceId == "tenants").Environment["APP_API_TOKEN"].ShouldBe("domain-token");
    }

    [Fact]
    public void ReferenceConfigurationBindsTheActualOwnerNumericAndTimeOptions()
    {
        PlatformServerBinding gateway = PlatformAppModel.Prepare(Options(active: "eventstore")).Single();
        IConfigurationRoot configuration = new ConfigurationBuilder().AddInMemoryCollection(gateway.Environment.Select(pair => new KeyValuePair<string, string?>(pair.Key.Replace("__", ":", StringComparison.Ordinal), pair.Value))).Build();
        WorkloadAssertionIssuerOptions issuer = configuration.GetSection("Authentication:WorkloadIssuer").Get<WorkloadAssertionIssuerOptions>()!;
        issuer.LifetimeSeconds.ShouldBe(WorkloadAssertionIssuerOptions.DefaultLifetimeSeconds);
        issuer.AudienceScopePrefix.ShouldBe(WorkloadAssertionIssuerOptions.DefaultAudienceScopePrefix);
        ProjectionDispatchOptions dispatch = configuration.GetSection("EventStore:ProjectionDispatch").Get<ProjectionDispatchOptions>()!;
        dispatch.Validate();
        dispatch.RetryBaseDelay.ShouldBe(TimeSpan.FromSeconds(1));
        ProjectionOptions projections = configuration.GetSection("EventStore:Projections").Get<ProjectionOptions>()!;
        projections.Validate();
        ProjectionDeliveryIdempotencyOptions delivery = configuration.GetSection("EventStore:ProjectionDeliveryIdempotency").Get<ProjectionDeliveryIdempotencyOptions>()!;
        delivery.Validate();
        delivery.ReservationLease.ShouldBe(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void DomainAddsReferenceOwnersAndExcludesUnrelatedModule()
    {
        string directory = Path.Combine(Path.GetTempPath(), "platform-model-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string domain = Fixture(directory, "parties", tasks: true);
            string unrelated = Fixture(directory, "unrelated", tasks: false);
            string project = Path.Combine(directory, "server.csproj");
            File.WriteAllText(project, "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");
            PlatformCompositionOptions original = Options();
            Dictionary<string, string> projects = new(PlatformServerBindings.ReferenceProjects) { ["parties/api"] = "server.csproj" };
            SourceMapping mapping = original.Mapping with { Entries = [.. original.Mapping.Entries, new SourceMappingEntry("Hexalith.Parties", "source", directory)] };
            PlatformCompositionOptions options = original with { Mapping = mapping, ActiveModule = "parties", DeclarationPaths = [domain, unrelated], Projects = projects, Configuration = new Dictionary<string, string> { ["parties/api/Sample:Setting"] = "bound" } };
            IDistributedApplicationBuilder builder = Builder();
            PlatformAppModel.Compose(builder, options);
            PlatformServerAnnotation[] servers = builder.Resources.SelectMany(r => r.Annotations.OfType<PlatformServerAnnotation>()).ToArray();
            servers.Length.ShouldBe(7);
            servers.ShouldNotContain(s => PlatformDeclarationSet.Id(s.Binding.Module.Declaration) == "unrelated");
            PlatformServerBinding party = servers.Single(s => s.Binding.ResourceId == "parties-api-host").Binding;
            party.Tasks.Single().GetProperty("scope").GetString().ShouldBe("per-start-verify");
            party.Tasks.Single().GetProperty("authorityClass").GetString().ShouldNotBeNullOrEmpty();
            PlatformAppModel.Prepare(options with { DomainModule = false }).Count.ShouldBe(1);
            IDistributedApplicationBuilder refused = Builder();
            Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(refused, options with { Configuration = new Dictionary<string, string>() })).Message.ShouldContain("parties/api/Sample:Setting");
            refused.Resources.ShouldBeEmpty();
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task ReferenceServersReachDeclaredReadinessInTheFileEntryAsync()
    {
        if (Environment.GetEnvironmentVariable("HEXALITH_PLATFORM_LIVE_SMOKE") != "1") Assert.Skip("Set HEXALITH_PLATFORM_LIVE_SMOKE=1 to run the Docker/Dapr source composition.");
        string apphost = Path.Combine(Root, "apphost.cs");
        try
        {
            CompositionProcessResult start = await CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("aspire", ["start", "--apphost", apphost, "--non-interactive", "--format", "Json"], Root, null), TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);
            start.ExitCode.ShouldBe(0, start.Output);
            foreach (string server in new[] { "eventstore", "tenants", "tenants-api", "memories", "memories-access-telemetry-clock", "memories-access-telemetry" })
            {
                CompositionProcessResult ready = await CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("aspire", ["wait", server, "--apphost", apphost, "--non-interactive", "--timeout", "120"], Root, null), TimeSpan.FromMinutes(3), TestContext.Current.CancellationToken);
                string diagnostic = ready.Output + ready.ErrorOutput;
                if (ready.ExitCode != 0)
                {
                    CompositionProcessResult logs = await CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("aspire", ["logs", server, "--apphost", apphost, "--non-interactive", "--tail", "50"], Root, null), TimeSpan.FromSeconds(30), CancellationToken.None);
                    diagnostic += logs.Output + logs.ErrorOutput;
                }
                ready.ExitCode.ShouldBe(0, server + ": " + diagnostic);
            }
        }
        finally
        {
            CompositionProcessResult stopped = await CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("aspire", ["stop", "--apphost", apphost, "--non-interactive"], Root, null), TimeSpan.FromSeconds(30), CancellationToken.None);
            stopped.TimedOut.ShouldBeFalse(stopped.Output + stopped.ErrorOutput);
            stopped.ExitCode.ShouldBe(0, stopped.Output + stopped.ErrorOutput);
        }
    }

    private static string Fixture(string directory, string module, bool tasks)
    {
        string fixture = File.ReadAllText(Path.Combine(Root, "references", "Hexalith.Builds", "test", "fixtures", "module", "platform", "valid.json")).Replace("sample", module, StringComparison.Ordinal);
        JsonNode document = JsonNode.Parse(fixture)!;
        JsonNode declaration = document["modules"]![0]!;
        declaration["lifecycle"]!["readiness"]![0]!["endpoint"] = "/ready";
        declaration["lifecycle"]!["tasks"] = tasks ? JsonNode.Parse("[{\"name\":\"verify\",\"scope\":\"per-start-verify\",\"authorityClass\":\"operator\",\"executable\":\"verify.sh\",\"arguments\":[]}]") : new JsonArray();
        declaration["lifecycle"]!["recoveryHooks"] = new JsonArray();
        declaration["lifecycle"]!["fenceHooks"] = new JsonArray();
        File.WriteAllText(Path.Combine(directory, "verify.sh"), "#!/bin/sh\nexit 0\n");
        string path = Path.Combine(directory, module + ".json");
        File.WriteAllText(path, document.ToJsonString());
        return path;
    }

    private static IDistributedApplicationBuilder Builder() => DistributedApplication.CreateBuilder(new DistributedApplicationOptions { Args = [], AssemblyName = typeof(PlatformAppModel).Assembly.GetName().Name, DisableDashboard = true, AllowUnsecuredTransport = true });
}
