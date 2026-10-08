#!/usr/bin/env dotnet
#:sdk Aspire.AppHost.Sdk@13.6.1
#:property ManagePackageVersionsCentrally=false
#:include DaprSelfHostedMtls.cs
#:package Aspire.Hosting.Docker@13.6.1
#:package Aspire.Hosting.Redis@13.6.1
#:package Aspire.Hosting.Keycloak@13.6.1-preview.1.26506.6
#:package CommunityToolkit.Aspire.Hosting.Dapr@13.6.0-preview.1.261001-0243
#:package Hexalith.EventStore.Aspire@3.117.1
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

using CommunityToolkit.Aspire.Hosting.Dapr;

using Hexalith.EventStore.Aspire;

using Microsoft.Extensions.Hosting;

// Hexalith.Platform AppHost — platform-owned composition root.
// Agents DomainService + FrontComposer UI wiring remains Agents Story 5.6 scope.

var builder = DistributedApplication.CreateBuilder(args);

// Full Agents composition requires the accepted owner providers and qualification.
if (bool.TryParse(builder.Configuration["Platform:Agents:Enabled"], out bool enableAgents) && enableAgents)
{
    throw new InvalidOperationException(
        "DependencyNotAvailable: EXT-HOST-1. Full Agents composition and accepted owner prerequisites are unavailable.");
}

// The Platform AppHost is also the clean-checkout Agents composition root. Enable the Works migration
// lane explicitly so its sibling source dependency cannot break that independent contract.
if (bool.TryParse(builder.Configuration["Platform:Works:Enabled"], out bool enableWorks) && enableWorks)
{
// The Works migration lane resolves sibling source projects until producer packages and a clean-checkout
// deployment contract exist. A missing source tree is a failed composition, never an empty green host.
string worksRoot = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "works"));
string eventStoreRoot = Path.Combine(worksRoot, "references", "Hexalith.EventStore");
string worksProject = Path.Combine(worksRoot, "src", "Hexalith.Works", "Hexalith.Works.csproj");
bool identityEnabled = bool.TryParse(builder.Configuration["Platform:Identity:Enabled"], out bool configuredIdentity) && configuredIdentity;
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

if (!builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "Works Dapr hosting is a development-only preview. A production profile and AD-20 exception are required.");
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

var eventStore = builder.AddProject("eventstore", eventStoreProject)
    .WithHttpHealthCheck("/alive")
    .WithEnvironment("MSBUILDDISABLENODEREUSE", "1")
    .WithEnvironment("EventStore__DomainServices__Registrations__wildcard_work_v1__AppId", "works")
    .WithEnvironment("EventStore__DomainServices__Registrations__wildcard_work_v1__MethodName", "process")
    .WithEnvironment("EventStore__DomainServices__Registrations__wildcard_work_v1__TenantId", "*")
    .WithEnvironment("EventStore__DomainServices__Registrations__wildcard_work_v1__Domain", "work")
    .WithEnvironment("EventStore__DomainServices__Registrations__wildcard_work_v1__Version", "v1")
    .WithEnvironment("EventStore__Publisher__TopicOverrides__work", "work.events")
    .WithEnvironment("EventStore__Publisher__DeadLetterTopicPrefix", "commanddeadletter")
    .WithEnvironment("Authentication__DaprInternal__AllowedCallers__0", "works");

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
    .WithHttpHealthCheck("/alive")
    .AddEventStoreDomainModule(
        eventStoreResources,
        "works",
        worksAcl,
        daprPlacementHostAddress: placementAddress,
        daprSchedulerHostAddress: schedulerAddress)
    .WithEnvironment("EventStore__CommandGateway__BaseAddress", eventStore.GetEndpoint("http"))
    .WaitFor(eventStoreResources.StateStore);

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
    .WaitFor(works)
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
    .Where(static project => SidecarOf(project) is not null))
{
    DaprSelfHostedMtls.ConfigureSidecar(
        builder.CreateResourceBuilder(project),
        SidecarOf(project)!,
        sentry,
        certificateDirectory,
        placement,
        scheduler);
}

string devSigningKey = builder.Configuration["Works:Authentication:DevSigningKey"] is { Length: > 0 } configuredKey
    ? configuredKey
    : "DevOnlySigningKey-AtLeast32Chars!";
foreach (IResourceBuilder<ProjectResource> jwtValidator in new[] { eventStore, admin })
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

builder.Build().Run();
