namespace Hexalith.Platform.Composition;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

using CommunityToolkit.Aspire.Hosting.Dapr;

using Hexalith.EventStore.Aspire;
using Hexalith.Platform.Composition;
using Hexalith.Platform.Works.Runtime;

using Microsoft.Extensions.Hosting;

/// <summary>The preserved opt-in Works preview.</summary>
public static class WorksPreviewModel
{
    /// <summary>The explicit development/test EventStore rate limits composed for the Works preview.</summary>
    public static IReadOnlyList<(string Key, string Value)> DevelopmentRateLimits { get; } =
    [
        ("EventStore__RateLimiting__PermitLimit", "8192"),
        ("EventStore__RateLimiting__ConsumerPermitLimit", "8192"),
        ("EventStore__RateLimiting__TenantPermitLimits__anonymous", "8192"),
        ("EventStore__RateLimiting__ConsumerPermitLimits__anonymous", "8192"),
    ];

    /// <summary>Adds the existing Works graph.</summary>
    public static void Compose(IDistributedApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        // The Works migration lane resolves sibling source projects until producer packages and a clean-checkout
        // deployment contract exist. A missing source tree is a failed composition, never an empty green host.
        string worksRoot = Path.GetFullPath(builder.Configuration["Platform:Works:SourceRoot"]
            ?? Path.Combine(builder.AppHostDirectory, "..", "works"));
        string eventStoreRoot = Path.GetFullPath(builder.Configuration["Platform:Works:EventStoreSourceRoot"]
            ?? Path.Combine(worksRoot, "references", "Hexalith.EventStore"));
        string worksProject = Path.Combine(worksRoot, "src", "Hexalith.Works", "Hexalith.Works.csproj");
        bool identityEnabled = bool.TryParse(builder.Configuration["Platform:Identity:Enabled"], out bool configuredIdentity) && configuredIdentity;
        if (identityEnabled && builder.Configuration["Platform:Works:SyntheticIdentity"] != "true")
            throw new InvalidOperationException("Platform.Identity has test-only provenance in the Works proof; select Platform:Works:SyntheticIdentity=true explicitly.");
        string eventStoreProject = identityEnabled
            ? Path.Combine(builder.AppHostDirectory, "src", "Hexalith.Platform.EventStoreHost", "Hexalith.Platform.EventStoreHost.csproj")
            : Path.Combine(eventStoreRoot, "src", "Hexalith.EventStore", "Hexalith.EventStore.csproj");
        string adminProject = Path.Combine(eventStoreRoot, "src", "Hexalith.EventStore.Admin.Server.Host", "Hexalith.EventStore.Admin.Server.Host.csproj");
        string operationsProject = Path.Combine(eventStoreRoot, "src", "Hexalith.EventStore.Operations", "Hexalith.EventStore.Operations.csproj");
        foreach (string project in new[] { worksProject, eventStoreProject, adminProject, operationsProject })
        {
            if (!File.Exists(project))
            {
                throw new FileNotFoundException("Works Platform composition requires the sibling Works source checkout.", project);
            }
        }

        string? profile = builder.Configuration["Platform:Works:ProductionProfile"];
        if (!string.IsNullOrWhiteSpace(profile)) WorksProductionProfile.Validate(File.ReadAllText(profile));
        if (!builder.Environment.IsDevelopment())
        {
            if (string.IsNullOrWhiteSpace(profile))
                throw new InvalidOperationException("A validated Works production profile is required before production admission.");
            throw new InvalidOperationException(
                "Works Dapr hosting is a development-only preview. A Maintainer time-bounded exception and qualified production providers are unavailable.");
        }

        string components = Path.Combine(builder.AppHostDirectory, "DaprComponents");
        string Component(string relativePath)
        {
            string path = Path.Combine(components, relativePath);
            return File.Exists(path)
                ? path
                : throw new FileNotFoundException("Platform Dapr component is missing.", path);
        }

        string eventStoreAcl = Component("accesscontrol.yaml");
        string worksAcl = Component("accesscontrol.works.yaml");
        string adminAcl = Component("accesscontrol.eventstore-admin.yaml");
        string operationsAcl = Component("accesscontrol.eventstore-operations.yaml");
        string resiliencyPath = Component(Path.Combine("resiliency", "resiliency.yaml"));
        string stateStorePath = Component("statestore.yaml");
        string pubSubPath = Component("pubsub.yaml");
        string configStorePath = Component("configstore.yaml");
        string sentryPath = Component("sentry.yaml");

        string? configuredPlacement = builder.Configuration[AspireDaprLocalServiceEndpoints.PlacementHostAddressKey];
        string? configuredScheduler = builder.Configuration[AspireDaprLocalServiceEndpoints.SchedulerHostAddressKey];
        if (string.IsNullOrWhiteSpace(configuredPlacement) != string.IsNullOrWhiteSpace(configuredScheduler))
        {
            throw new InvalidOperationException("Dapr placement and scheduler must be configured together.");
        }

        var (sentry, certificateDirectory) = DaprSelfHostedMtls.AddSentry(builder, sentryPath);
        IResourceBuilder<ContainerResource>? placement = null;
        IResourceBuilder<ContainerResource>? scheduler = null;
        string placementAddress;
        string schedulerAddress;
        if (string.IsNullOrWhiteSpace(configuredPlacement))
        {
            (placement, scheduler) = DaprSelfHostedMtls.AddControlPlane(builder, sentry, certificateDirectory);
            placementAddress = DaprSelfHostedMtls.PlacementHostAddress;
            schedulerAddress = DaprSelfHostedMtls.SchedulerHostAddress;
        }
        else
        {
            (string? resolvedPlacement, string? resolvedScheduler) = AspireDaprLocalServiceEndpoints.Resolve(
                configuredPlacement!, configuredScheduler!);
            placementAddress = resolvedPlacement ?? throw new InvalidOperationException("Dapr placement endpoint is unavailable.");
            schedulerAddress = resolvedScheduler ?? throw new InvalidOperationException("Dapr scheduler endpoint is unavailable.");
        }

        IResourceBuilder<IDaprComponentResource> resiliency = builder.AddDaprComponent(
            "resiliency", "resiliency", new DaprComponentOptions { LocalPath = resiliencyPath });
        IResourceBuilder<IDaprComponentResource> configStore = builder.AddDaprComponent(
            "configstore", "configuration.redis", new DaprComponentOptions { LocalPath = configStorePath });

        var eventStore = builder.AddProject("eventstore", eventStoreProject)
            .WithEnvironment("MSBUILDDISABLENODEREUSE", "1")
            .WithEnvironment("EventStore__DomainServices__Registrations__wildcard_work_v1__AppId", "works")
            .WithEnvironment("EventStore__DomainServices__Registrations__wildcard_work_v1__MethodName", "process")
            .WithEnvironment("EventStore__DomainServices__Registrations__wildcard_work_v1__TenantId", "*")
            .WithEnvironment("EventStore__DomainServices__Registrations__wildcard_work_v1__Domain", "work")
            .WithEnvironment("EventStore__DomainServices__Registrations__wildcard_work_v1__Version", "v1")
            .WithEnvironment("EventStore__Publisher__TopicOverrides__work", "work.events")
            .WithEnvironment("EventStore__Publisher__DeadLetterTopicPrefix", "commanddeadletter")
            .WithEnvironment("Authentication__DaprInternal__AllowedCallers__0", "works");

        // Development/test limits for this preview only. EventStore's defaults (1000 per minute per tenant,
        // 100 per second per consumer) reject the preview's own internal actor, projection and recovery
        // traffic with 429, and the dropped projection update is an open EventStore defect. These values
        // carry the twelve-source parity load while keeping both SDK limiters finite. Production admission
        // is refused above and must size its own limits.
        foreach ((string key, string value) in DevelopmentRateLimits)
            _ = eventStore.WithEnvironment(key, value);

        var admin = builder.AddProject("eventstore-admin", adminProject)
            .WithEnvironment("MSBUILDDISABLENODEREUSE", "1");

        HexalithEventStoreResources eventStoreResources = builder.AddHexalithEventStore(
            eventStore,
            admin,
            adminUI: null,
            eventStoreDaprConfigPath: eventStoreAcl,
            adminServerDaprConfigPath: adminAcl,
            resiliencyConfigPath: resiliencyPath,
            stateStoreComponentPath: stateStorePath,
            daprPlacementHostAddress: placementAddress,
            daprSchedulerHostAddress: schedulerAddress,
            pubSubComponentPath: pubSubPath);

        var works = builder.AddProject("works", worksProject)
            .WithHttpEndpoint()
            .WithEnvironment("Works__Projection__UseSdkDelivery", "true")
            .WithEnvironment("Works__Recovery__UseSdkRuntime", "true")
            .WithEnvironment("Works__Query__GatewayWorkloadId", "eventstore")
            .WithEnvironment("Authentication__Workload__Audience", "works")
            .WithEnvironment("Authentication__Workload__AllowedCallers__0", "eventstore")
            .WithEnvironment("Authentication__Workload__AllowedCallers__1", "eventstore-admin")
            .WithEnvironment("Authentication__WorkloadIssuer__Workload", "works")
            .WithEnvironment("WORKS_PLATFORM_RUNTIME", "1")
            .WithEnvironment("DOTNET_STARTUP_HOOKS", typeof(WorksRecoveryHostingStartup).Assembly.Location)
            .WithEnvironment("ASPNETCORE_HOSTINGSTARTUPASSEMBLIES", typeof(WorksRecoveryHostingStartup).Assembly.GetName().Name!)
            .AddEventStoreDomainModule(
                eventStoreResources,
                "works",
                worksAcl,
                daprPlacementHostAddress: placementAddress,
                daprSchedulerHostAddress: schedulerAddress)
            .WithEnvironment("EventStore__CommandGateway__BaseAddress", eventStore.GetEndpoint("http"))
            .WaitFor(eventStoreResources.StateStore);

        // Gateway startup discovers this module's protected metadata. A readiness wait in the opposite
        // direction prevents either service from starting; keep the SDK's references and sidecar wiring.
        foreach (WaitAnnotation wait in works.Resource.Annotations.OfType<WaitAnnotation>()
            .Where(wait => ReferenceEquals(wait.Resource, eventStore.Resource)).ToArray())
            works.Resource.Annotations.Remove(wait);

        eventStore.WithEnvironment("Authentication__WorkloadIssuer__Workload", "eventstore");
        SdkReadinessHealthCheck.Attach(builder, eventStore);
        SdkReadinessHealthCheck.Attach(builder, works);
        builder.CreateResourceBuilder(SidecarOf(eventStore.Resource)!).WithReference(configStore);

        var operations = builder.AddProject("eventstore-operations", operationsProject)
            .WithHttpEndpoint()
            .WithHttpHealthCheck("/alive")
            .WithEnvironment("DOTNET_ENVIRONMENT", builder.Environment.EnvironmentName)
            .WithEnvironment("DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER", "1")
            .WithEnvironment("MSBUILDDISABLENODEREUSE", "1")
            .WithEnvironment("EventStoreOperations__PubSubName", "pubsub")
            .WithEnvironment("EventStoreOperations__TopicName", "deadletter.work.events")
            .WithEnvironment("EventStoreOperations__CaptureRoute", "/dead-letters/work/events")
            .WithEnvironment("EventStoreOperations__AdminCallerAppId", "eventstore-admin")
            .WithEnvironment("EventStoreOperations__ReplayAppId", "works")
            .WithEnvironment("EventStoreOperations__ReplayMethodName", "work/events")
            .WithReference(works)
            .WaitForStart(works)
            .WaitFor(eventStoreResources.StateStore)
            .WithDaprSidecar(sidecar => sidecar
                .WithOptions(new DaprSidecarOptions
                {
                    AppId = "eventstore-operations",
                    Config = operationsAcl,
                    EnableAppHealthCheck = true,
                    AppHealthCheckPath = "/alive",
                    PlacementHostAddress = placementAddress,
                    SchedulerHostAddress = schedulerAddress,
                })
                .WithReference(eventStoreResources.StateStore)
                .WithReference(eventStoreResources.PubSub));

        _ = admin
            .WithEnvironment("AdminServer__OperationsAppId", "eventstore-operations")
            .WithReference(operations)
            .WaitFor(operations);

        foreach (IDaprSidecarResource sidecar in builder.Resources
            .OfType<ProjectResource>()
            .Select(SidecarOf)
            .OfType<IDaprSidecarResource>()
            .Distinct())
        {
            _ = builder.CreateResourceBuilder(sidecar).WithReference(resiliency);
        }

        foreach (ProjectResource project in builder.Resources
            .OfType<ProjectResource>()
            .Where(static project => SidecarOf(project) is not null).ToArray())
        {
            DaprSelfHostedMtls.ConfigureSidecar(
                builder.CreateResourceBuilder(project),
                SidecarOf(project)!,
                sentry,
                certificateDirectory,
                placement,
                scheduler);
            _ = builder.CreateResourceBuilder(project).WithGeneratedEventStoreAppChannelToken();
        }

        string devSigningKey = builder.Configuration["Works:Authentication:DevSigningKey"] is { Length: > 0 } configuredKey
            ? configuredKey
            : "DevOnlySigningKey-AtLeast32Chars!";
        foreach (IResourceBuilder<ProjectResource> jwtValidator in new[] { eventStore, admin, works })
        {
            _ = jwtValidator
                .WithEnvironment("DOTNET_ENVIRONMENT", builder.Environment.EnvironmentName)
                .WithEnvironment("Authentication__JwtBearer__Authority", string.Empty)
                .WithEnvironment("Authentication__JwtBearer__Issuer", "hexalith-dev")
                .WithEnvironment("Authentication__JwtBearer__Audience", HexalithEventStoreSecurityOptions.DefaultAudience)
                .WithEnvironment("Authentication__JwtBearer__ValidAudiences__0", HexalithEventStoreSecurityOptions.DefaultAudience)
                .WithEnvironment("Authentication__JwtBearer__SigningKey", devSigningKey)
                .WithEnvironment("Authentication__JwtBearer__RequireHttpsMetadata", "false");
        }

        static IDaprSidecarResource? SidecarOf(ProjectResource project)
        {
            DaprSidecarAnnotation[] annotations = [.. project.Annotations.OfType<DaprSidecarAnnotation>()];
            return annotations.Length switch
            {
                0 => null,
                1 => annotations[0].Sidecar,
                _ => throw new InvalidOperationException(
                    $"Project resource '{project.Name}' has {annotations.Length} Dapr sidecars; expected one."),
            };
        }
    }
}
