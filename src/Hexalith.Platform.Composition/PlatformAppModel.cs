namespace Hexalith.Platform.Composition;

using System.Globalization;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Aspire.Hosting.Lifecycle;
using YamlDotNet.RepresentationModel;

/// <summary>The single Platform-owned declaration composition used by the file AppHost and Builds.</summary>
public static class PlatformAppModel
{
    /// <summary>Routes the thin file entry while refusing incompatible local topologies.</summary>
    public static void ComposeEntry(IDistributedApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (builder.Configuration.GetValue<bool>("Platform:Hosted:Enabled"))
        {
            PlatformHostedPublishing.ComposeNativeProbe(builder);
            return;
        }
        if (bool.TryParse(builder.Configuration["Platform:Agents:Enabled"], out bool agents) && agents)
        {
            if (!AgentsHostComposition.TryCompose(builder))
                throw new InvalidOperationException("DependencyNotAvailable: EXT-HOST-1. Full Agents composition and accepted owner prerequisites are unavailable.");
            return;
        }
        bool works = bool.TryParse(builder.Configuration["Platform:Works:Enabled"], out bool selected) && selected;
        if (works && builder.Configuration.GetValue<bool>("Platform:Timesheets:Enabled"))
            throw new InvalidOperationException("Hexalith.Timesheets: Platform:Timesheets:Enabled cannot be combined with Platform:Works:Enabled; select one Development composition.");
        if (works) WorksPreviewModel.Compose(builder);
        else Compose(builder);
    }

    /// <summary>Validates and binds the complete set, without adding any resources.</summary>
    public static IReadOnlyList<PlatformServerBinding> Prepare(PlatformCompositionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        PlatformTimesheetsBinding.ValidateMode(options);
        return PlatformServerBindings.Resolve(options, PlatformDeclarationSet.Load(options));
    }

    /// <summary>Composes the thin file AppHost from the command's persisted source mapping.</summary>
    public static void Compose(IDistributedApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        bool timesheets = builder.Configuration.GetValue<bool>("Platform:Timesheets:Enabled");
        if (timesheets && (!builder.Environment.IsDevelopment() || !builder.ExecutionContext.IsRunMode))
            throw new InvalidOperationException("Hexalith.Timesheets: Platform:Timesheets:Enabled requires a Development run. A qualified release and production profile are unavailable.");
        if (!builder.Environment.IsDevelopment())
            throw new InvalidOperationException("Platform declaration composition requires a Development host. A production profile is unavailable.");
        string? active = builder.Configuration["Platform:ActiveModule"];
        if (active is not null && string.IsNullOrWhiteSpace(active)) throw new InvalidDataException("Platform:ActiveModule: module identity is empty.");
        string? declaration = builder.Configuration["Platform:DeclarationPath"];
        string? mappingPath = builder.Configuration["Platform:SourceMapping:Path"] ?? Environment.GetEnvironmentVariable("HEXALITH_SOURCE_MAPPING_PATH");
        string? hash = builder.Configuration["Platform:SourceMapping:Hash"] ?? Environment.GetEnvironmentVariable("HEXALITH_SOURCE_MAPPING_HASH");
        SourceMapping mapping = mappingPath is null
            ? WorkspaceRootResolver.ResolveAsync(declaration ?? Path.Combine(builder.AppHostDirectory, "hexalith-platform.json"), WorkspaceMode.Source, CancellationToken.None).GetAwaiter().GetResult()
            : SourceMapping.LoadVerified(mappingPath, hash ?? string.Empty);
        Compose(builder, new PlatformCompositionOptions { PlatformRoot = builder.AppHostDirectory, Mapping = mapping, ActiveModule = active, DeclarationPaths = declaration is null ? [] : [declaration], TimesheetsEnabled = timesheets, EventStoreSeedProject = builder.Configuration["Platform:EventStoreSeedProject"], EnvironmentId = builder.Configuration["Platform:EnvironmentId"], UsePrebuiltProjects = builder.Configuration.GetValue<bool>("Platform:UsePrebuiltProjects") });
    }

    /// <summary>Composes a v2 signed Builds plan, keeping its package-mode gateway seed.</summary>
    public static void ComposeRun(IDistributedApplicationBuilder builder, CompositionRunPlan plan, string signingKey, string? seedProject)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(plan);
        ComposeRun(builder, plan, signingKey, seedProject, static options => options);
    }

    internal static void ComposeRun(IDistributedApplicationBuilder builder, CompositionRunPlan plan, string signingKey, string? seedProject, Func<PlatformCompositionOptions, PlatformCompositionOptions> configureOptions)
    {
        SourceMapping mapping = plan.SourceMapping ?? throw new InvalidDataException("Platform v2 composition requires the command-resolved source mapping.");
        string root = mapping.PlatformIdentity?.PlatformPath ?? mapping.Entries.Single(e => e.Identity == "Hexalith.Platform").Path!;
        bool timesheets = PlatformTimesheetsBinding.SelectRun(plan, root, builder.Configuration.GetValue<bool>("Platform:Timesheets:Enabled"));
        Dictionary<string, string> configuration = new(StringComparer.Ordinal);
        foreach (string server in new[] { "parties/domain", "eventstore/gateway", "tenants/domain", "tenants/api", "memories/server", "memories/domain", "memories/clock", "memories/telemetry" })
        {
            configuration[server + "/Authentication:JwtBearer:SigningKey"] = signingKey;
            configuration[server + "/Authentication:JwtBearer:Issuer"] = CompositionEnvironment.TokenIssuer;
            configuration[server + "/Authentication:JwtBearer:Audience"] = CompositionEnvironment.TokenAudience;
            configuration[server + "/Authentication:JwtBearer:ValidAudiences"] = JsonSerializer.Serialize(new[] { CompositionEnvironment.TokenAudience });
        }
        foreach (string server in new[] { "tenants/domain", "tenants/api" })
        {
            configuration[server + "/EventStore:Authentication:SigningKey"] = signingKey;
            configuration[server + "/EventStore:Authentication:Issuer"] = CompositionEnvironment.TokenIssuer;
            configuration[server + "/EventStore:Authentication:Audience"] = CompositionEnvironment.TokenAudience;
            configuration[server + "/Jwt:SigningKey"] = signingKey;
        }
        Compose(builder, configureOptions(new PlatformCompositionOptions
        {
            PlatformRoot = root, Mapping = mapping, DeclarationPaths = plan.PlatformDeclarationPaths,
            ActiveModule = plan.PlatformActiveModule, EventStoreSeedProject = seedProject, Configuration = configuration, RunPlan = plan,
            TimesheetsEnabled = timesheets, RetentionProfile = PlatformRetentionProfile.Select(plan, root),
        }));
    }

    /// <summary>Adds exactly the prepared environment, with owner readiness and lifecycle metadata.</summary>
    public static void Compose(IDistributedApplicationBuilder builder, PlatformCompositionOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        if (options.TimesheetsEnabled && (!builder.Environment.IsDevelopment() || !builder.ExecutionContext.IsRunMode))
            throw new InvalidOperationException("Hexalith.Timesheets: Platform:Timesheets:Enabled requires a Development run. A qualified release and production profile are unavailable.");
        PlatformTimesheetsBinding.ValidateMode(options);
        if (options.UsePrebuiltProjects && (!builder.Environment.IsDevelopment() || options.Mapping.Mode != WorkspaceMode.Source))
            throw new InvalidDataException("Prebuilt owner launches require the explicit Development/source verification lane.");
        if (options.RetentionProfile is not null && (!builder.Environment.IsDevelopment() || options.Mapping.Mode != WorkspaceMode.Source || options.RunPlan is null))
            throw new InvalidDataException("The PostgreSQL retention experiment requires a runner-owned Development source run.");
        if (options.RetentionProfile is { } retentionObserver) builder.Services.AddSingleton<ICompositionTestObservationProvider>(retentionObserver);
        HashSet<IResource> existingResources = [.. builder.Resources];
        var declarations = PlatformDeclarationSet.Load(options);
        PlatformDaprBindings dapr = PlatformDaprBindings.Resolve(declarations, options.RetentionProfile);
        IReadOnlyList<PlatformServerBinding> servers = PlatformServerBindings.Resolve(options, declarations, dapr);
        foreach (PlatformServerBinding server in servers.Where(s => s.Image is not null))
        {
            IEnumerable<string> arguments = ["manifest", "inspect", server.Image!];
#if HEXALITH_PLATFORM_LOCAL_PACKAGE_QUALIFICATION
            arguments = PlatformLoopbackRegistryProbe.Arguments(server.Image!, options.Mapping.Mode, builder.Environment.IsDevelopment());
#endif
            System.Diagnostics.ProcessStartInfo probe = CompositionProcess.CreateStartInfo("docker", arguments, options.PlatformRoot, null);
            CompositionProcessResult result = options.ImageProbeRunner(probe, TimeSpan.FromSeconds(20), CancellationToken.None).GetAwaiter().GetResult();
            if (result.ExitCode != 0 || result.TimedOut) throw new InvalidDataException($"{server.Module.Source}: {PlatformDeclarationSet.Id(server.Module.Declaration)}/{server.ServerId}: image '{server.Image}' is unavailable from its released registry.");
        }
        string components = options.ComponentsDirectory ?? Path.Combine(options.PlatformRoot, "DaprComponents", "reference");
        string? selectedDaprHome = options.RunPlan?.DaprHome ?? Environment.GetEnvironmentVariable("HEXALITH_DAPR_HOME");
        string daprHome = selectedDaprHome ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        bool hasMemories = servers.Any(server => PlatformDeclarationSet.Id(server.Module.Declaration) == "memories");
        bool hasGateway = servers.Any(server => server.ResourceId == "eventstore");
        bool hasTimesheets = servers.Any(server => server.ResourceId == "timesheets");
        string rendered = options.RunPlan is null ? Path.Combine(Path.GetTempPath(), "hexalith-platform", Guid.NewGuid().ToString("N")) : Path.Combine(options.RunPlan.Workspace, "platform-components");
        string[] checkouts = options.Mapping.Entries.Select(entry => entry.Path).OfType<string>().ToArray();
        // Refuse a misplaced certificate parent before this attempt claims ports or writes run state.
        _ = DaprSelfHostedMtls.ResolveCertificateParent(builder, options.PlatformRoot, rendered, checkouts);
        CompositionWorkspace.CreatePrivateDirectory(Path.GetDirectoryName(rendered)!);
        CompositionWorkspace.CreatePrivateDirectory(rendered);
        PlatformRoutingCatalogBootstrap? catalog = null;
        try
        {
            CompositionRunPorts localPorts = AllocateOwnedSecondaryPorts(Math.Max(1, servers.Count(server => server.ResourceId != "eventstore")), options.RunPlan);
            int redisPort = options.RunPlan?.Ports.Redis ?? localPorts.Redis;
            int vectorPort = localPorts.UiHttp;
            int sentryPort = localPorts.PlacementHealth;
            int placementPort = options.RunPlan?.Ports.Placement ?? localPorts.Placement;
            int schedulerPort = options.RunPlan?.Ports.Scheduler ?? localPorts.Scheduler;
            string sentryAddress = "localhost:" + sentryPort;
            string placementAddress = "localhost:" + placementPort;
            string schedulerAddress = "localhost:" + schedulerPort;
            string discovery = options.RunPlan is null
                ? CompositionDaprComponentRenderer.RenderConfiguration("platform-" + Path.GetFileName(rendered), rendered)
                : File.ReadAllText(options.RunPlan.DaprConfigPath);
            if (options.RetentionProfile is { } retentionDiscovery) discovery = retentionDiscovery.ConfigureDiscovery(discovery);
            string mappingWorkspace = options.RunPlan?.Workspace ?? rendered;
            SourceMappingMaterializer.WriteAsync(options.Mapping, mappingWorkspace, CancellationToken.None).GetAwaiter().GetResult();
            catalog = PlatformRoutingCatalogBootstrap.Create(options, servers, dapr, rendered);
#if HEXALITH_PLATFORM_SOURCE_CATALOG
            if (catalog is not null && options.RunPlan?.SelectedTest?.ModuleId == "parties")
                builder.Services.AddSingleton<ICompositionRunPreparation>(_ => new PlatformNativeTestPreparation(options.RunPlan, catalog.Directory,
                    servers.Single(server => server.ResourceId == "eventstore").Environment["Authentication__JwtBearer__SigningKey"], catalog.TenantBootstrapAccess,
                    servers.Single(server => server.ResourceId == "memories").Environment["APP_API_TOKEN"]));
#endif
            options.RetentionProfile?.Prepare(rendered, localPorts.Redis);
            using ECDsa clockKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            string secrets = Path.Combine(rendered, "secrets.json");
            File.WriteAllText(secrets, JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["access-telemetry-clock-key"] = Convert.ToBase64String(clockKey.ExportPkcs8PrivateKey()),
                ["access-telemetry-marker-key"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            }));
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(secrets, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            IReadOnlyDictionary<string, string> daprFiles = dapr.Render(components, redisPort, vectorPort, secrets, discovery, sentryAddress);
            string subscriptionsDirectory = Path.Combine(rendered, "subscriptions");
            bool hasSubscriptions = daprFiles.Keys.Any(file => file.StartsWith("subscription.", StringComparison.Ordinal));
            if (hasSubscriptions) CompositionWorkspace.CreatePrivateDirectory(subscriptionsDirectory);
            foreach (var file in daprFiles) File.WriteAllText(Path.Combine(file.Key.StartsWith("subscription.", StringComparison.Ordinal) ? subscriptionsDirectory : rendered, file.Key), file.Value);
            string sentryConfiguration = Path.Combine(rendered, "sentry.yaml");
            File.WriteAllText(sentryConfiguration, "apiVersion: dapr.io/v1alpha1\nkind: Configuration\nmetadata:\n  name: platform-sentry\nspec:\n  mtls:\n    enabled: true\n    workloadCertTTL: 24h\n");
            if (hasTimesheets) File.WriteAllText(Path.Combine(rendered, "timesheets-readmodels.yaml"), File.ReadAllText(Path.Combine(components, "timesheets-readmodels.yaml")).Replace("localhost:6379", "127.0.0.1:" + redisPort, StringComparison.Ordinal));

            string schedulerData = Path.Combine(rendered, "scheduler-data");
            CompositionWorkspace.CreatePrivateDirectory(schedulerData);
            string? gatewayContentRoot = servers.FirstOrDefault(server => server.ResourceId == "eventstore")?.ProjectPath is { } gatewayProject
                ? PlatformGatewayConfiguration.Create(gatewayProject, rendered, servers) : null;

            var (sentry, certificates) = DaprSelfHostedMtls.AddSentry(builder, sentryConfiguration, options.PlatformRoot, rendered, sentryPort, options.RetentionProfile is null ? null : PlatformRetentionProfile.RuntimeVersion, checkouts);
            // An explicitly selected parent can live outside the workspace. Dispose only this run's child.
            if (builder.Configuration["Dapr:Mtls:CertificateDirectory"] is not null)
                builder.Services.AddSingleton<IHostedService>(_ => new PlatformCompositionWorkspace(certificates));
            string redisData = Path.Combine(rendered, "redis-data");
            CompositionWorkspace.CreatePrivateDirectory(redisData);
            string[] redisReference = CompositionToolchainPins.RedisImageReference.Split('@', 2);
            int colon = redisReference[0].LastIndexOf(':');
            IResourceBuilder<ContainerResource> redis = builder.AddContainer("redis", redisReference[0][..colon], redisReference[0][(colon + 1)..])
                .WithImageSHA256(redisReference[1]["sha256:".Length..])
                .WithBindMount(redisData, "/data")
                .WithArgs("redis-server", "--appendonly", "yes", "--dir", "/data")
                .WithEndpoint(port: redisPort, targetPort: 6379, name: "tcp", scheme: "tcp", isProxied: false);
            if (DaprSelfHostedMtls.ResolveContainerUser() is { } redisUser) redis.WithContainerRuntimeArgs("--user", redisUser);
            PlatformRedisHealthCheck.Attach(builder, redis);
            if (options.RunPlan is { } run) redis.WithContainerName(run.RedisContainerName).WithContainerRuntimeArgs("--label", $"{CompositionEnvironment.ContainerRunLabel}={run.RunId}");
            IResourceBuilder<ContainerResource>? telemetryPostgres = options.RetentionProfile?.AddResource(builder);
            Dictionary<string, IResourceBuilder<IDaprComponentResource>> daprComponents = dapr.ComponentNames.ToDictionary(name => name,
                name => builder.AddDaprComponent(name, dapr.Type(name), new DaprComponentOptions { LocalPath = Path.Combine(rendered, dapr.FileName(name)) }), StringComparer.Ordinal);
            IResourceBuilder<IDaprComponentResource>? timesheetsReadModels = hasTimesheets
                ? builder.AddDaprComponent(PlatformTimesheetsBinding.Component, "state.redis", new DaprComponentOptions { LocalPath = Path.Combine(rendered, "timesheets-readmodels.yaml") }) : null;
            IResourceBuilder<ContainerResource>? vectors = null;
            IResourceBuilder<ContainerResource>? graph = null;
            if (hasMemories)
            {
                vectors = builder.AddContainer("memories-vectors", options.RetentionProfile is null ? "redis/redis-stack" : "docker.io/redis/redis-stack-server", options.RetentionProfile is null ? "latest" : "7.4.0-v8").WithEndpoint(port: vectorPort, targetPort: 6379, name: "tcp", scheme: "tcp", isProxied: false);
                graph = builder.AddContainer("memories-graph", "falkordb/falkordb", options.RetentionProfile is null ? "latest" : "v4.12.0").WithEndpoint(targetPort: 6379, name: "tcp", scheme: "tcp");
                foreach (var store in new[] { (Resource: vectors, Name: "vectors-data"), (Resource: graph, Name: "graph-data") })
                {
                    string data = Path.Combine(rendered, store.Name);
                    CompositionWorkspace.CreatePrivateDirectory(data);
                    store.Resource.WithBindMount(data, "/data").WithEnvironment("REDIS_ARGS", "--appendonly yes");
                    if (DaprSelfHostedMtls.ResolveContainerUser() is { } storeUser) store.Resource.WithContainerRuntimeArgs("--user", storeUser);
                }
                PlatformRedisHealthCheck.Attach(builder, vectors);
                graph.WithEnvironment("FALKORDB_DATA_PATH", "/data");
                PlatformRedisHealthCheck.Attach(builder, graph);
                if (options.RetentionProfile is not null)
                {
                    vectors.WithImageSHA256("798ab84d9f266936b034ab11c4d04a2b8e4b441884c5aa7d17ac951eefdf742a");
                    graph.WithImageSHA256("7927eb194df0fadf70e2deb6c20c8178743ec92aacd739c790f8a35ce5dee613");
                }
                if (options.RunPlan is { } scoped)
                {
                    vectors.WithContainerRuntimeArgs("--label", $"{CompositionEnvironment.ContainerRunLabel}={scoped.RunId}");
                    graph.WithContainerRuntimeArgs("--label", $"{CompositionEnvironment.ContainerRunLabel}={scoped.RunId}");
                }
            }
            if (selectedDaprHome is not null) builder.AddDapr(daprOptions => daprOptions.DaprPath = Path.Combine(daprHome, "tools", OperatingSystem.IsWindows() ? "dapr.exe" : "dapr"));
            var (placement, scheduler) = DaprSelfHostedMtls.AddControlPlane(builder, sentry, certificates, schedulerData, placementPort, schedulerPort, options.RetentionProfile is null ? null : PlatformRetentionProfile.RuntimeVersion);
            if (options.RunPlan is { } securityRun)
                foreach (var control in new[] { sentry, placement, scheduler }) control.WithContainerRuntimeArgs("--label", $"{CompositionEnvironment.ContainerRunLabel}={securityRun.RunId}");
            Dictionary<string, IResourceBuilder<ProjectResource>> projects = new(StringComparer.Ordinal);
            Dictionary<string, IResourceBuilder<ContainerResource>> containers = new(StringComparer.Ordinal);
            foreach (PlatformServerBinding server in servers)
            {
                if (server.ProjectPath is { } projectPath)
                {
                    IResourceBuilder<ProjectResource> project = builder.AddProject(server.ResourceId, projectPath, launchProfileName: null)
                        .WithAnnotation(new PlatformServerAnnotation(server))
                        .WithAnnotation(new ExecutableAnnotation { Command = "dotnet", WorkingDirectory = Path.GetDirectoryName(projectPath)! })
                        .WithArgs("run", "--project", projectPath, "--configuration", options.Mapping.Mode == WorkspaceMode.Source ? "Debug" : "Release", "--no-launch-profile", "-p:GeneratePackageOnBuild=false");
                    if (options.UsePrebuiltProjects) project.WithArgs("--no-build", "--no-restore");
                    if (options.RetentionProfile is not null)
                        project.WithArgs("--artifacts-path", RetentionArtifactsPath(options, server));
                    else if (options.RunPlan?.SelectedTest is not null)
                        project.WithArgs("--artifacts-path", Path.Combine(options.RunPlan.Workspace, "project-builds", server.ResourceId));
                    if (server.ResourceId == "timesheets")
                    {
                        project.WithHttpEndpoint(name: "public")
                            .WithHttpEndpoint(name: "internal", isProxied: false)
                            .WithEndpoint("internal", endpoint => { endpoint.IsExternal = false; endpoint.ExcludeReferenceEndpoint = true; })
                            .WithHttpHealthCheck(server.ReadinessEndpoint, endpointName: "public")
                            .WithEnvironment("Timesheets__InternalSurface__Port", project.GetEndpoint("internal").Property(EndpointProperty.TargetPort))
                            .WithArgs("-p:HexalithEventStoreRoot=" + options.Mapping.Entries.Single(entry => entry.Identity.Equals("Hexalith.EventStore", StringComparison.OrdinalIgnoreCase)).Path);
                    }
                    else project.WithHttpEndpoint(name: "http").WithHttpHealthCheck(server.ReadinessEndpoint);
                    ConfigureEnvironment(project, server, options, clockKey);
                    catalog?.Configure(project, server);
                    if (server.ResourceId == "eventstore") project.WithEnvironment("ASPNETCORE_CONTENTROOT", gatewayContentRoot!);
                    if (PlatformDeclarationSet.Id(server.Module.Declaration) == "memories" && vectors is not null && graph is not null)
                        project.WithEnvironment("ConnectionStrings__redis", ReferenceExpression.Create($"{vectors.GetEndpoint("tcp").Property(EndpointProperty.Host)}:{vectors.GetEndpoint("tcp").Property(EndpointProperty.Port)}")).WithEnvironment("ConnectionStrings__falkordb", ReferenceExpression.Create($"{graph.GetEndpoint("tcp").Property(EndpointProperty.Host)}:{graph.GetEndpoint("tcp").Property(EndpointProperty.Port)}")).WaitFor(vectors).WaitFor(graph);
                    foreach (KeyValuePair<string, string> pair in SourceMappingMaterializer.Environment(options.Mapping, mappingWorkspace)) project.WithEnvironment(pair.Key, pair.Value);
                    projects.Add(server.ResourceId, project);
                }
                else
                {
                    int separator = server.Image!.LastIndexOf(':');
                    IResourceBuilder<ContainerResource> container = builder.AddContainer(server.ResourceId, server.Image[..separator], server.Image[(separator + 1)..])
                        .WithHttpEndpoint(targetPort: 8080, name: "http")
                        .WithHttpHealthCheck(server.ReadinessEndpoint)
                        .WithAnnotation(new PlatformServerAnnotation(server));
                    ConfigureEnvironment(container, server, options, clockKey);
                    catalog?.Configure(container, server);
                    if (catalog?.Participates(server) == true) container.WithBindMount(rendered, rendered);
                    if (PlatformDeclarationSet.Id(server.Module.Declaration) == "memories" && vectors is not null && graph is not null)
                        container.WithEnvironment("ConnectionStrings__redis", ReferenceExpression.Create($"{vectors.GetEndpoint("tcp").Property(EndpointProperty.Host)}:{vectors.GetEndpoint("tcp").Property(EndpointProperty.Port)}")).WithEnvironment("ConnectionStrings__falkordb", ReferenceExpression.Create($"{graph.GetEndpoint("tcp").Property(EndpointProperty.Host)}:{graph.GetEndpoint("tcp").Property(EndpointProperty.Port)}")).WaitFor(vectors).WaitFor(graph);
                    if (options.RunPlan is not null) container.WithContainerRuntimeArgs("--label", $"{CompositionEnvironment.ContainerRunLabel}={options.RunPlan.RunId}");
                    containers.Add(server.ResourceId, container);
                }
            }
            if (projects.TryGetValue("eventstore", out IResourceBuilder<ProjectResource>? eventStore))
            {
                eventStore.WaitFor(redis);
                if (servers.Any(s => s.AppId == "tenants"))
                    eventStore.WithEnvironment("EventStore__DomainServices__Registrations__wildcard_tenants_v1__AppId", "tenants")
                        .WithEnvironment("EventStore__DomainServices__Registrations__wildcard_tenants_v1__MethodName", "process")
                        .WithEnvironment("EventStore__DomainServices__Registrations__wildcard_tenants_v1__TenantId", "*")
                        .WithEnvironment("EventStore__DomainServices__Registrations__wildcard_tenants_v1__Domain", "tenants")
                        .WithEnvironment("EventStore__DomainServices__Registrations__wildcard_tenants_v1__Version", "v1");
                if (servers.Any(s => s.AppId == "parties"))
                    eventStore.WithEnvironment("EventStore__DomainServices__Registrations__wildcard_party_v1__AppId", "parties")
                        .WithEnvironment("EventStore__DomainServices__Registrations__wildcard_party_v1__MethodName", "process")
                        .WithEnvironment("EventStore__DomainServices__Registrations__wildcard_party_v1__TenantId", "*")
                        .WithEnvironment("EventStore__DomainServices__Registrations__wildcard_party_v1__Domain", "party")
                        .WithEnvironment("EventStore__DomainServices__Registrations__wildcard_party_v1__Version", "v1");
                if (hasTimesheets)
                    eventStore.WithEnvironment("EventStore__DomainServices__Registrations__*|timesheets|v1__AppId", "timesheets")
                        .WithEnvironment("EventStore__DomainServices__Registrations__*|timesheets|v1__MethodName", "process")
                        .WithEnvironment("EventStore__DomainServices__Registrations__*|timesheets|v1__TenantId", "*")
                        .WithEnvironment("EventStore__DomainServices__Registrations__*|timesheets|v1__Domain", "timesheets")
                        .WithEnvironment("EventStore__DomainServices__Registrations__*|timesheets|v1__Version", "v1");
            }
            int sidecarIndex = 0;
            foreach (PlatformServerBinding server in servers)
            {
                CompositionDaprSidecarPorts ports = server.ResourceId == "eventstore"
                    ? new CompositionDaprSidecarPorts(options.RunPlan?.Ports.EventStoreDaprHttp ?? localPorts.EventStoreDaprHttp, options.RunPlan?.Ports.EventStoreDaprGrpc ?? localPorts.EventStoreDaprGrpc, options.RunPlan?.Ports.EventStoreDaprInternalGrpc ?? localPorts.EventStoreDaprInternalGrpc, options.RunPlan?.Ports.EventStoreDaprMetrics ?? localPorts.EventStoreDaprMetrics)
                    : localPorts.ModuleDapr[sidecarIndex++];
                {
                    void Attach<T>(IResourceBuilder<T> resource) where T : IResourceWithEnvironment, IResourceWithEndpoints, IResourceWithWaitSupport
                    {
                        resource.WithDaprSidecar(sidecar =>
                        {
                            sidecar.WithOptions(new DaprSidecarOptions { AppId = server.AppId, Config = Path.Combine(rendered, "accesscontrol." + server.AppId + ".yaml"),
                                ResourcesPaths = hasSubscriptions ? ImmutableHashSet.Create(subscriptionsDirectory) : ImmutableHashSet<string>.Empty,
                                AppEndpoint = server.ResourceId == "timesheets" ? "internal" : null,
                                DaprHttpPort = ports.Http, DaprGrpcPort = ports.Grpc, DaprInternalGrpcPort = ports.InternalGrpc, MetricsPort = ports.Metrics,
                                EnableAppHealthCheck = false,
                                PlacementHostAddress = placementAddress,
                                SchedulerHostAddress = schedulerAddress });
                            if (server.ResourceId == "timesheets") sidecar.WithReference(timesheetsReadModels!);
                            foreach (var component in daprComponents.Where(component => dapr.Consumes(component.Key, server.AppId))) sidecar.WithReference(component.Value);
                        }).WaitFor(redis);
                        if (telemetryPostgres is not null && PlatformDeclarationSet.Id(server.Module.Declaration) == "memories") resource.WaitFor(telemetryPostgres);
                        if (hasGateway && server.ResourceId == "memories")
                            resource.WithEnvironment("EventStore__CommandGateway__BaseAddress", "http://127.0.0.1:" + ports.Http);
                        else if (hasGateway && PlatformDeclarationSet.Id(server.Module.Declaration) != "eventstore")
                            resource.WithEnvironment("EventStore__CommandGateway__BaseAddress", projects["eventstore"].GetEndpoint("http"));
                        if (hasGateway && server.ResourceId == "tenants-api") resource.WithEnvironment("EventStore__GatewayReadiness__BaseAddress", projects["eventstore"].GetEndpoint("http"));
                        if (catalog is not null && server.ResourceId == "parties") resource.WithEnvironment("Tenants__ReadinessBaseAddress", projects["tenants"].GetEndpoint("http"));
                    }
                    if (projects.TryGetValue(server.ResourceId, out IResourceBuilder<ProjectResource>? project)) Attach(project);
                    else Attach(containers[server.ResourceId]);
                }
                IResource resource = projects.TryGetValue(server.ResourceId, out IResourceBuilder<ProjectResource>? p) ? p.Resource : containers[server.ResourceId].Resource;
                IDaprSidecarResource sidecarResource = resource.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar;
                DaprSidecarOptionsAnnotation sidecarOptions = sidecarResource.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single();
                sidecarResource.Annotations[sidecarResource.Annotations.IndexOf(sidecarOptions)] = new DaprSidecarOptionsAnnotation(sidecarOptions.Options with { RuntimePath = daprHome, DaprHttpPort = ports.Http, DaprGrpcPort = ports.Grpc, DaprInternalGrpcPort = ports.InternalGrpc, MetricsPort = ports.Metrics });
                if (projects.TryGetValue(server.ResourceId, out IResourceBuilder<ProjectResource>? tokenProject)) tokenProject.WithEnvironment("DAPR_HTTP_ENDPOINT", "http://127.0.0.1:" + ports.Http).WithEnvironment("DAPR_GRPC_ENDPOINT", "http://127.0.0.1:" + ports.Grpc);
                if (options.RetentionProfile is { } retained && PlatformDeclarationSet.Id(server.Module.Declaration) == "memories") retained.Track((IResourceWithEndpoints)resource, ports.Http, server.Environment);
                if (projects.TryGetValue(server.ResourceId, out var securedProject)) DaprSelfHostedMtls.ConfigureSidecar(securedProject, sidecarResource, sentry, certificates, placement, scheduler);
                else DaprSelfHostedMtls.ConfigureSidecar(containers[server.ResourceId], sidecarResource, sentry, certificates, placement, scheduler);
            }
            foreach (PlatformServerBinding server in servers)
            foreach (JsonElement dependency in server.Module.Declaration.GetProperty("identity").GetProperty("dependencies").EnumerateArray())
            foreach (PlatformServerBinding target in servers.Where(s => PlatformDeclarationSet.Id(s.Module.Declaration) == dependency.GetString()))
            {
                // Catalog participants must start together. Workers that wait for the committed
                // gateway cannot be startup prerequisites of a participant in that same cohort.
                if (catalog?.Participates(server) == true && !catalog.Participates(target)) continue;
                if (projects.TryGetValue(server.ResourceId, out IResourceBuilder<ProjectResource>? sourceProject))
                {
                    if (projects.TryGetValue(target.ResourceId, out IResourceBuilder<ProjectResource>? targetProject))
                    {
                        if (catalog?.Participates(server) == true && catalog.Participates(target)) sourceProject.WaitForStart(targetProject);
                        else sourceProject.WaitFor(targetProject);
                    }
                    else sourceProject.WaitFor(containers[target.ResourceId]);
                }
                else if (projects.TryGetValue(target.ResourceId, out IResourceBuilder<ProjectResource>? targetProject)) containers[server.ResourceId].WaitFor(targetProject);
                else containers[server.ResourceId].WaitFor(containers[target.ResourceId]);
            }
            string? selectedModule = options.ActiveModule ?? options.RunPlan?.SelectedTest?.ModuleId;
            string? infrastructureOwner = declarations.Where(module => PlatformDeclarationSet.Id(module.Declaration) == selectedModule)
                .Select(module => PlatformDeclarationSet.Id(module.Declaration)).SingleOrDefault();
            foreach (IResource resource in builder.Resources.Where(resource => !existingResources.Contains(resource)))
            {
                string name = resource.Name.EndsWith("-dapr-cli", StringComparison.Ordinal) ? resource.Name[..^"-dapr-cli".Length] : resource.Name;
                PlatformServerBinding? declaringServer = servers.SingleOrDefault(server => server.ResourceId == name)
                    ?? builder.Resources.FirstOrDefault(candidate => candidate.Annotations.OfType<DaprSidecarAnnotation>().Any(annotation => ReferenceEquals(annotation.Sidecar, resource)))?.Annotations.OfType<PlatformServerAnnotation>().SingleOrDefault()?.Binding;
                string? owner = declaringServer is not null ? PlatformDeclarationSet.Id(declaringServer.Module.Declaration)
                    : name is "memories-vectors" or "memories-graph" or "memories-retention-postgresql" ? declarations.Where(module => PlatformDeclarationSet.Id(module.Declaration) == "memories").Select(module => PlatformDeclarationSet.Id(module.Declaration)).SingleOrDefault()
                    : infrastructureOwner;
                resource.Annotations.Add(new PlatformResourceOwnerAnnotation(owner));
                if (resource.Annotations.OfType<DaprSidecarAnnotation>().SingleOrDefault()?.Sidecar is { } ownedSidecar
                    && !builder.Resources.Contains(ownedSidecar))
                    ownedSidecar.Annotations.Add(new PlatformResourceOwnerAnnotation(owner));
            }
            builder.Services.AddSingleton<IDistributedApplicationEventingSubscriber>(new PlatformDaprEndpointSubscriber(servers, certificates, sentryAddress, rendered));
            if (options.RetentionProfile is not null)
            {
                PlatformServerBinding owner = servers.First(server => PlatformDeclarationSet.Id(server.Module.Declaration) == "memories");
                string diagnostics = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(RetentionArtifactsPath(options, owner)))!, "diagnostics", "native-sidecars");
                builder.Services.AddSingleton<PlatformDaprDiagnostics>(_ => new(diagnostics, servers.Select(server => server.ResourceId).ToArray(),
                    servers.SelectMany(server => server.Environment).Where(pair => pair.Value.Length >= 8).Select(pair => pair.Value).ToArray()));
                builder.Services.AddSingleton<IDistributedApplicationEventingSubscriber>(services => services.GetRequiredService<PlatformDaprDiagnostics>());
            }
            if (options.RunPlan is null)
            {
                builder.Services.AddSingleton(_ => new PlatformCompositionWorkspace(rendered));
                builder.Services.AddSingleton<Microsoft.Extensions.Hosting.IHostedService>(services => services.GetRequiredService<PlatformCompositionWorkspace>());
            }
            if (options.RunPlan is null && projects.TryGetValue("eventstore", out IResourceBuilder<ProjectResource>? localGateway))
            {
                PlatformServerBinding binding = servers.Single(server => server.ResourceId == "eventstore");
                string runId = "platform-" + Path.GetFileName(rendered);
                // DCP startup can wait on dependent resources; this operation must start before that wait.
                builder.Services.Insert(0, ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService>(services => new PlatformGatewayStartup(services, runId, binding.Environment["Authentication__JwtBearer__SigningKey"], rendered)));
            }
        }
        catch (Exception failure)
        {
            // The private workspace belongs to this attempt. Retain startup diagnostics for inspection.
            PlatformCompositionWorkspace.MarkFailed(rendered, failure.Message);
            throw;
        }
    }

    internal static string RetentionArtifactsPath(PlatformCompositionOptions options, PlatformServerBinding server)
    {
        string? runId = options.RunPlan?.RunId;
        string resource = server.ResourceId;
        if (!CompositionRunPlanFactory.IsRunId(runId) || string.IsNullOrEmpty(resource) || resource.Length > 64
            || !char.IsAsciiLetterOrDigit(resource[0]) || resource.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
            throw new InvalidDataException("Selected retention build outputs require a valid run identity and resource path segment.");
        string owner = CompositionDeclarationClosure.TaskRepositoryRoot(options.Mapping, server.Module);
        return Path.Combine(Path.GetFullPath(owner), "artifacts", "retention", runId!, "build", resource);
    }

    internal static CompositionRunPorts AllocateOwnedSecondaryPorts(int count, CompositionRunPlan? plan, Func<int, CompositionRunPorts>? allocator = null)
    {
        if (plan is not { ResourceClaimStateDirectory: { } claims })
            return AllocateSecondaryPorts(count, plan?.Ports, allocator);
        CompositionResourceClaimStore store = new(claims);
        for (int attempt = 0; attempt < 64; attempt++)
        {
            CompositionRunPorts accepted = AllocateSecondaryPorts(count, plan.Ports, allocator ?? (slots => CompositionPortAllocator.Allocate(slots, includeSecondEventStore: false, excludedPorts: store.ClaimedPorts())));
            try
            {
                store.Acquire(plan.RunId, accepted.ToList().Select(port => "tcp:127.0.0.1:" + port.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                return accepted;
            }
            catch (CompositionResourceClaimConflictException) when (attempt < 63) { }
        }
        throw new InvalidOperationException("No independent secondary runner port allocation is available.");
    }

    internal static CompositionRunPorts AllocateSecondaryPorts(int count, CompositionRunPorts? persisted, Func<int, CompositionRunPorts>? allocator = null)
    {
        allocator ??= slots => CompositionPortAllocator.Allocate(slots);
        HashSet<int> excluded = persisted?.ToList().ToHashSet() ?? [];
        for (int attempt = 0; attempt < 64; attempt++)
        {
            CompositionRunPorts allocated = allocator(count);
            if (!allocated.ToList().Any(excluded.Contains)) return allocated;
        }
        throw new InvalidOperationException("Secondary Platform ports overlap the persisted run ports.");
    }

    internal static string RenderSecretStore(string content, string secrets) =>
        content.Replace(JsonSerializer.Serialize("DaprComponents/secrets.json"), JsonSerializer.Serialize(secrets), StringComparison.Ordinal);

    internal static string MergeRunDiscovery(string accessControl, string configuration)
    {
        System.Text.Json.Nodes.JsonNode acl = System.Text.Json.Nodes.JsonNode.Parse(accessControl)!;
        YamlStream run = new();
        run.Load(new StringReader(configuration));
        var spec = (YamlMappingNode)((YamlMappingNode)run.Documents[0].RootNode).Children[new YamlScalarNode("spec")];
        acl["spec"]!["nameResolution"] = ToJson(spec.Children[new YamlScalarNode("nameResolution")]);
        return acl.ToJsonString();

        static System.Text.Json.Nodes.JsonNode ToJson(YamlNode node) => node switch
        {
            YamlMappingNode mapping => new System.Text.Json.Nodes.JsonObject(mapping.Children.Select(pair =>
                new KeyValuePair<string, System.Text.Json.Nodes.JsonNode?>(pair.Key.ToString(), ToJson(pair.Value)))),
            YamlScalarNode scalar => System.Text.Json.Nodes.JsonValue.Create(scalar.Value)!,
            _ => throw new InvalidDataException("Run discovery must contain mappings and scalar values."),
        };
    }

    private static void ConfigureEnvironment<T>(IResourceBuilder<T> resource, PlatformServerBinding server, PlatformCompositionOptions options, ECDsa clockKey) where T : IResourceWithEnvironment
    {
        resource.WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development").WithEnvironment("DOTNET_ENVIRONMENT", "Development");
        resource.WithEnvironment("UseSharedCompilation", "false").WithEnvironment("MSBUILDDISABLENODEREUSE", "1").WithEnvironment("DOTNET_CLI_USE_MSBUILD_SERVER", "0");
        if (options.RunPlan is not null) resource.WithEnvironment(CompositionEnvironment.RunId, options.RunPlan.RunId);
        foreach (KeyValuePair<string, string> pair in server.Environment) resource.WithEnvironment(pair.Key, pair.Value);
        if (PlatformDeclarationSet.Id(server.Module.Declaration) == "memories") resource.WithEnvironment("AccessTelemetryLifecycle__AttestationVerificationKey", Convert.ToBase64String(clockKey.ExportSubjectPublicKeyInfo()));
    }
}
