namespace Hexalith.Platform.Composition;

using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Hexalith.EventStore.Aspire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Aspire.Hosting.Lifecycle;
using YamlDotNet.RepresentationModel;

/// <summary>The single Platform-owned declaration composition used by the file AppHost and Builds.</summary>
public static class PlatformAppModel
{
    /// <summary>Validates and binds the complete set, without adding any resources.</summary>
    public static IReadOnlyList<PlatformServerBinding> Prepare(PlatformCompositionOptions options) => PlatformServerBindings.Resolve(options, PlatformDeclarationSet.Load(options));

    /// <summary>Composes the thin file AppHost from the command's persisted source mapping.</summary>
    public static void Compose(IDistributedApplicationBuilder builder)
    {
        string? active = builder.Configuration["Platform:ActiveModule"];
        string? declaration = builder.Configuration["Platform:DeclarationPath"];
        string? mappingPath = builder.Configuration["Platform:SourceMapping:Path"] ?? Environment.GetEnvironmentVariable("HEXALITH_SOURCE_MAPPING_PATH");
        string? hash = builder.Configuration["Platform:SourceMapping:Hash"] ?? Environment.GetEnvironmentVariable("HEXALITH_SOURCE_MAPPING_HASH");
        SourceMapping mapping = mappingPath is null
            ? WorkspaceRootResolver.ResolveAsync(declaration ?? Path.Combine(builder.AppHostDirectory, "references", PlatformServerBindings.Repository(active ?? "eventstore"), "module", "hexalith.module-manifest.v2.json"), WorkspaceMode.Source, CancellationToken.None).GetAwaiter().GetResult()
            : SourceMapping.LoadVerified(mappingPath, hash ?? string.Empty);
        Compose(builder, new PlatformCompositionOptions { PlatformRoot = builder.AppHostDirectory, Mapping = mapping, ActiveModule = active, DeclarationPaths = declaration is null ? [] : [declaration], EventStoreSeedProject = builder.Configuration["Platform:EventStoreSeedProject"] });
    }

    /// <summary>Composes a v2 signed Builds plan, keeping its package-mode gateway seed.</summary>
    public static void ComposeRun(IDistributedApplicationBuilder builder, CompositionRunPlan plan, string signingKey, string? seedProject)
    {
        SourceMapping mapping = plan.SourceMapping ?? throw new InvalidDataException("Platform v2 composition requires the command-resolved source mapping.");
        string root = mapping.PlatformIdentity?.PlatformPath ?? mapping.Entries.Single(e => e.Identity == "Hexalith.Platform").Path!;
        Dictionary<string, string> configuration = new(StringComparer.Ordinal);
        foreach (string server in new[] { "eventstore/gateway", "tenants/domain", "tenants/api", "memories/server", "memories/clock", "memories/telemetry" })
        {
            configuration[server + "/Authentication:JwtBearer:SigningKey"] = signingKey;
            configuration[server + "/Authentication:JwtBearer:Issuer"] = CompositionEnvironment.TokenIssuer;
            configuration[server + "/Authentication:JwtBearer:Audience"] = CompositionEnvironment.TokenAudience;
            configuration[server + "/Authentication:JwtBearer:ValidAudiences"] = JsonSerializer.Serialize(new[] { CompositionEnvironment.TokenAudience });
        }
        configuration["tenants/api/EventStore:Authentication:SigningKey"] = signingKey;
        configuration["tenants/api/EventStore:Authentication:Issuer"] = CompositionEnvironment.TokenIssuer;
        configuration["tenants/api/EventStore:Authentication:Audience"] = CompositionEnvironment.TokenAudience;
        configuration["tenants/api/Jwt:SigningKey"] = signingKey;
        Compose(builder, new PlatformCompositionOptions
        {
            PlatformRoot = root, Mapping = mapping, DeclarationPaths = plan.PlatformDeclarationPaths,
            ActiveModule = plan.PlatformActiveModule, EventStoreSeedProject = seedProject, Configuration = configuration, RunPlan = plan,
        });
    }

    /// <summary>Adds exactly the prepared environment, with owner readiness and lifecycle metadata.</summary>
    public static void Compose(IDistributedApplicationBuilder builder, PlatformCompositionOptions options)
    {
        IReadOnlyList<PlatformServerBinding> servers = Prepare(options);
        foreach (PlatformServerBinding server in servers.Where(s => s.Image is not null))
        {
            System.Diagnostics.ProcessStartInfo probe = CompositionProcess.CreateStartInfo("docker", ["manifest", "inspect", server.Image!], options.PlatformRoot, null);
            CompositionProcessResult result = options.ImageProbeRunner(probe, TimeSpan.FromSeconds(20), CancellationToken.None).GetAwaiter().GetResult();
            if (result.ExitCode != 0 || result.TimedOut) throw new InvalidDataException($"{server.Module.Source}: {PlatformDeclarationSet.Id(server.Module.Declaration)}/{server.ServerId}: image '{server.Image}' is unavailable from its released registry.");
        }
        string components = options.ComponentsDirectory ?? Path.Combine(options.PlatformRoot, "DaprComponents", "reference");
        string[] files = ["statestore.yaml", "pubsub.yaml", "secretstore.yaml", "llm.yaml", "telemetry-state.yaml", "telemetry-config.yaml", "accesscontrol.eventstore.yaml", "accesscontrol.tenants.yaml"];
        foreach (string file in files) if (!File.Exists(Path.Combine(components, file))) throw new FileNotFoundException("Platform shared local Dapr component is unavailable.", Path.Combine(components, file));

        // Render only connection endpoints and local signing material; literal component names remain owner-compatible until 1.7.
        string rendered = options.RunPlan is null ? Path.Combine(Path.GetTempPath(), "hexalith-platform", Guid.NewGuid().ToString("N")) : Path.Combine(options.RunPlan.Workspace, "platform-components");
        CompositionWorkspace.CreatePrivateDirectory(Path.GetDirectoryName(rendered)!);
        CompositionWorkspace.CreatePrivateDirectory(rendered);
        try
        {
            CompositionRunPorts localPorts = AllocateSecondaryPorts(Math.Max(1, servers.Count(server => server.ResourceId != "eventstore")), options.RunPlan?.Ports);
            int redisPort = options.RunPlan?.Ports.Redis ?? localPorts.Redis;
            int vectorPort = localPorts.UiHttp;
            string mappingWorkspace = options.RunPlan?.Workspace ?? rendered;
            SourceMappingMaterializer.WriteAsync(options.Mapping, mappingWorkspace, CancellationToken.None).GetAwaiter().GetResult();
            using ECDsa clockKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            string secrets = Path.Combine(rendered, "secrets.json");
            File.WriteAllText(secrets, JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["access-telemetry-clock-key"] = Convert.ToBase64String(clockKey.ExportPkcs8PrivateKey()),
                ["access-telemetry-marker-key"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            }));
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(secrets, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            foreach (string file in files)
            {
                string content = File.ReadAllText(Path.Combine(components, file)).Replace("localhost:6379", "127.0.0.1:" + redisPort, StringComparison.Ordinal).Replace("127.0.0.1:6379", "127.0.0.1:" + redisPort, StringComparison.Ordinal);
                if (file == "telemetry-state.yaml") content = content.Replace("127.0.0.1:" + redisPort, "127.0.0.1:" + vectorPort, StringComparison.Ordinal);
                if (file == "secretstore.yaml") content = RenderSecretStore(content, secrets);
                if (file.StartsWith("accesscontrol.", StringComparison.Ordinal) && options.RunPlan is not null) content = MergeRunDiscovery(content, File.ReadAllText(options.RunPlan.DaprConfigPath));
                File.WriteAllText(Path.Combine(rendered, file), content);
            }
            File.WriteAllText(Path.Combine(rendered, "telemetry-secrets.yaml"), File.ReadAllText(Path.Combine(rendered, "secretstore.yaml")).Replace("name: secretstore", "name: access-telemetry-secrets", StringComparison.Ordinal));

            string[] redisReference = CompositionToolchainPins.RedisImageReference.Split('@', 2);
            int colon = redisReference[0].LastIndexOf(':');
            IResourceBuilder<ContainerResource> redis = builder.AddContainer("redis", redisReference[0][..colon], redisReference[0][(colon + 1)..])
                .WithImageSHA256(redisReference[1]["sha256:".Length..])
                .WithEndpoint(port: redisPort, targetPort: 6379, name: "tcp", scheme: "tcp", isProxied: false);
            if (options.RunPlan is { } run) redis.WithContainerName(run.RedisContainerName).WithContainerRuntimeArgs("--label", $"{CompositionEnvironment.ContainerRunLabel}={run.RunId}");
            IResourceBuilder<IDaprComponentResource> secretStore = builder.AddDaprComponent("secretstore", "secretstores.local.file", new DaprComponentOptions { LocalPath = Path.Combine(rendered, "secretstore.yaml") });
            IResourceBuilder<IDaprComponentResource> telemetrySecrets = builder.AddDaprComponent("access-telemetry-secrets", "secretstores.local.file", new DaprComponentOptions { LocalPath = Path.Combine(rendered, "telemetry-secrets.yaml") });
            IResourceBuilder<IDaprComponentResource> llm = builder.AddDaprComponent("llm", "conversation.echo", new DaprComponentOptions { LocalPath = Path.Combine(rendered, "llm.yaml") });
            IResourceBuilder<IDaprComponentResource> telemetryState = builder.AddDaprComponent("access-telemetry-store", "state.redis", new DaprComponentOptions { LocalPath = Path.Combine(rendered, "telemetry-state.yaml") });
            IResourceBuilder<IDaprComponentResource> telemetryConfig = builder.AddDaprComponent("access-telemetry-config", "configuration.redis", new DaprComponentOptions { LocalPath = Path.Combine(rendered, "telemetry-config.yaml") });
            IResourceBuilder<ContainerResource>? vectors = null;
            IResourceBuilder<ContainerResource>? graph = null;
            if (servers.Any(s => PlatformDeclarationSet.Id(s.Module.Declaration) == "memories"))
            {
                vectors = builder.AddContainer("memories-vectors", "redis/redis-stack", "latest").WithEndpoint(port: vectorPort, targetPort: 6379, name: "tcp", scheme: "tcp", isProxied: false);
                graph = builder.AddContainer("memories-graph", "falkordb/falkordb", "latest").WithEndpoint(targetPort: 6379, name: "tcp", scheme: "tcp");
                if (options.RunPlan is { } scoped)
                {
                    vectors.WithContainerRuntimeArgs("--label", $"{CompositionEnvironment.ContainerRunLabel}={scoped.RunId}");
                    graph.WithContainerRuntimeArgs("--label", $"{CompositionEnvironment.ContainerRunLabel}={scoped.RunId}");
                }
            }
            Dictionary<string, IResourceBuilder<ProjectResource>> projects = new(StringComparer.Ordinal);
            Dictionary<string, IResourceBuilder<ContainerResource>> containers = new(StringComparer.Ordinal);
            foreach (PlatformServerBinding server in servers)
            {
                if (server.ProjectPath is { } projectPath)
                {
                    IResourceBuilder<ProjectResource> project = builder.AddProject(server.ResourceId, projectPath, launchProfileName: null)
                        .WithHttpEndpoint(name: "http")
                        .WithHttpHealthCheck(server.ReadinessEndpoint)
                        .WithAnnotation(new PlatformServerAnnotation(server))
                        .WithAnnotation(new ExecutableAnnotation { Command = "dotnet", WorkingDirectory = Path.GetDirectoryName(projectPath)! })
                        .WithArgs("run", "--project", projectPath, "--configuration", options.Mapping.Mode == WorkspaceMode.Source ? "Debug" : "Release", "--no-launch-profile");
                    ConfigureEnvironment(project, server, options, clockKey);
                    if (server.ResourceId == "eventstore") project.WithEnvironment("ASPNETCORE_CONTENTROOT", PlatformGatewayConfiguration.Create(projectPath, rendered, servers));
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
                    if (PlatformDeclarationSet.Id(server.Module.Declaration) == "memories" && vectors is not null && graph is not null)
                        container.WithEnvironment("ConnectionStrings__redis", ReferenceExpression.Create($"{vectors.GetEndpoint("tcp").Property(EndpointProperty.Host)}:{vectors.GetEndpoint("tcp").Property(EndpointProperty.Port)}")).WithEnvironment("ConnectionStrings__falkordb", ReferenceExpression.Create($"{graph.GetEndpoint("tcp").Property(EndpointProperty.Host)}:{graph.GetEndpoint("tcp").Property(EndpointProperty.Port)}")).WaitFor(vectors).WaitFor(graph);
                    if (options.RunPlan is not null) container.WithContainerRuntimeArgs("--label", $"{CompositionEnvironment.ContainerRunLabel}={options.RunPlan.RunId}");
                    containers.Add(server.ResourceId, container);
                }
            }
            HexalithEventStoreResources? gateway = null;
            if (projects.TryGetValue("eventstore", out IResourceBuilder<ProjectResource>? eventStore))
            {
                gateway = builder.AddHexalithEventStore(eventStore, adminServer: null, adminUI: null,
                    eventStoreDaprConfigPath: Path.Combine(rendered, "accesscontrol.eventstore.yaml"), adminServerDaprConfigPath: null, resiliencyConfigPath: null,
                    stateStoreComponentPath: Path.Combine(rendered, "statestore.yaml"), eventStoreDaprHttpPort: options.RunPlan?.Ports.EventStoreDaprHttp ?? localPorts.EventStoreDaprHttp,
                    daprPlacementHostAddress: options.RunPlan is null ? null : "127.0.0.1:" + options.RunPlan.Ports.Placement,
                    daprSchedulerHostAddress: options.RunPlan is null ? null : "127.0.0.1:" + options.RunPlan.Ports.Scheduler,
                    pubSubComponentPath: Path.Combine(rendered, "pubsub.yaml"));
                eventStore.WaitFor(redis);
                if (servers.Any(s => s.AppId == "tenants"))
                    eventStore.WithEnvironment("EventStore__DomainServices__Registrations__wildcard_tenants_v1__AppId", "tenants")
                        .WithEnvironment("EventStore__DomainServices__Registrations__wildcard_tenants_v1__MethodName", "process")
                        .WithEnvironment("EventStore__DomainServices__Registrations__wildcard_tenants_v1__TenantId", "*")
                        .WithEnvironment("EventStore__DomainServices__Registrations__wildcard_tenants_v1__Domain", "tenants")
                        .WithEnvironment("EventStore__DomainServices__Registrations__wildcard_tenants_v1__Version", "v1");
            }
            int sidecarIndex = 0;
            foreach (PlatformServerBinding server in servers)
            {
                CompositionDaprSidecarPorts ports = server.ResourceId == "eventstore"
                    ? new CompositionDaprSidecarPorts(options.RunPlan?.Ports.EventStoreDaprHttp ?? localPorts.EventStoreDaprHttp, options.RunPlan?.Ports.EventStoreDaprGrpc ?? localPorts.EventStoreDaprGrpc, options.RunPlan?.Ports.EventStoreDaprInternalGrpc ?? localPorts.EventStoreDaprInternalGrpc, options.RunPlan?.Ports.EventStoreDaprMetrics ?? localPorts.EventStoreDaprMetrics)
                    : localPorts.ModuleDapr[sidecarIndex++];
                if (server.ResourceId != "eventstore")
                {
                    void Attach<T>(IResourceBuilder<T> resource) where T : IResourceWithEnvironment, IResourceWithEndpoints, IResourceWithWaitSupport
                    {
                        resource.WithDaprSidecar(sidecar =>
                        {
                            sidecar.WithOptions(new DaprSidecarOptions { AppId = server.AppId, Config = server.AppId == "tenants" ? Path.Combine(rendered, "accesscontrol.tenants.yaml") : options.RunPlan?.DaprConfigPath,
                                DaprHttpPort = ports.Http, DaprGrpcPort = ports.Grpc, DaprInternalGrpcPort = ports.InternalGrpc, MetricsPort = ports.Metrics,
                                EnableAppHealthCheck = false,
                                PlacementHostAddress = options.RunPlan is null ? null : "127.0.0.1:" + options.RunPlan.Ports.Placement,
                                SchedulerHostAddress = options.RunPlan is null ? null : "127.0.0.1:" + options.RunPlan.Ports.Scheduler });
                            if (gateway is not null) sidecar.WithReference(gateway.StateStore).WithReference(gateway.PubSub);
                            sidecar.WithReference(secretStore).WithReference(telemetrySecrets).WithReference(llm).WithReference(telemetryState).WithReference(telemetryConfig);
                        }).WaitFor(redis);
                        if (gateway is not null && PlatformDeclarationSet.Id(server.Module.Declaration) != "eventstore") resource.WithEnvironment("EventStore__CommandGateway__BaseAddress", projects["eventstore"].GetEndpoint("http"));
                    }
                    if (projects.TryGetValue(server.ResourceId, out IResourceBuilder<ProjectResource>? project)) Attach(project);
                    else Attach(containers[server.ResourceId]);
                }
                IResource resource = projects.TryGetValue(server.ResourceId, out IResourceBuilder<ProjectResource>? p) ? p.Resource : containers[server.ResourceId].Resource;
                IDaprSidecarResource sidecarResource = resource.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar;
                DaprSidecarOptionsAnnotation sidecarOptions = sidecarResource.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single();
                sidecarResource.Annotations[sidecarResource.Annotations.IndexOf(sidecarOptions)] = new DaprSidecarOptionsAnnotation(sidecarOptions.Options with { DaprHttpPort = ports.Http, DaprGrpcPort = ports.Grpc, DaprInternalGrpcPort = ports.InternalGrpc, MetricsPort = ports.Metrics });
                if (projects.TryGetValue(server.ResourceId, out IResourceBuilder<ProjectResource>? tokenProject)) tokenProject.WithEnvironment("DAPR_HTTP_ENDPOINT", "http://127.0.0.1:" + ports.Http).WithEnvironment("DAPR_GRPC_ENDPOINT", "http://127.0.0.1:" + ports.Grpc);
            }
            foreach (PlatformServerBinding server in servers)
            foreach (JsonElement dependency in server.Module.Declaration.GetProperty("identity").GetProperty("dependencies").EnumerateArray())
            foreach (PlatformServerBinding target in servers.Where(s => PlatformDeclarationSet.Id(s.Module.Declaration) == dependency.GetString()))
            {
                if (projects.TryGetValue(server.ResourceId, out IResourceBuilder<ProjectResource>? sourceProject))
                {
                    if (projects.TryGetValue(target.ResourceId, out IResourceBuilder<ProjectResource>? targetProject)) sourceProject.WaitFor(targetProject);
                    else sourceProject.WaitFor(containers[target.ResourceId]);
                }
                else if (projects.TryGetValue(target.ResourceId, out IResourceBuilder<ProjectResource>? targetProject)) containers[server.ResourceId].WaitFor(targetProject);
                else containers[server.ResourceId].WaitFor(containers[target.ResourceId]);
            }
            builder.Services.AddSingleton<IDistributedApplicationEventingSubscriber>(new PlatformDaprEndpointSubscriber(servers));
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
                builder.Services.Insert(0, ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService>(services => new PlatformGatewayStartup(services, runId, binding.Environment["Authentication__JwtBearer__SigningKey"])));
            }
        }
        catch
        {
            if (options.RunPlan is null) CompositionWorkspace.TryDeleteAsync(rendered, CancellationToken.None).GetAwaiter().GetResult();
            throw;
        }
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
        YamlStream acl = new();
        acl.Load(new StringReader(accessControl));
        YamlStream run = new();
        run.Load(new StringReader(configuration));
        YamlMappingNode aclSpec = (YamlMappingNode)((YamlMappingNode)acl.Documents[0].RootNode).Children[new YamlScalarNode("spec")];
        YamlMappingNode runSpec = (YamlMappingNode)((YamlMappingNode)run.Documents[0].RootNode).Children[new YamlScalarNode("spec")];
        aclSpec.Children[new YamlScalarNode("nameResolution")] = runSpec.Children[new YamlScalarNode("nameResolution")];
        StringWriter writer = new(CultureInfo.InvariantCulture);
        acl.Save(writer, assignAnchors: false);
        return writer.ToString();
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
