using Hexalith.EventStore;
using Hexalith.Platform.Identity;
using Hexalith.Platform.Protection;
using Hexalith.EventStore.Extensions;
using Hexalith.EventStore.HealthChecks;
using Hexalith.EventStore.Middleware;
using Hexalith.EventStore.OpenApi;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.ServiceDefaults;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.SignalRHub;
using Dapr;
using Microsoft.Extensions.Options;

using Microsoft.AspNetCore.Server.Kestrel.Core;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddDaprClient(options => ErasureRootDaprTransport.Configure(options));
builder.Services.AddHealthChecks()
    .AddEventStoreDaprHealthChecks()
    .AddEventStoreAppChannelTokenHealthCheck();
builder.Services.AddEventStore();
builder.Services.AddEventStoreServer(builder.Configuration);
builder.Services.AddEventStoreDomainQueryRouting();
builder.Services.AddEventStoreSignalR(builder.Configuration);
builder.Services.AddPlatformIdentity(builder.Configuration);
builder.Services.AddPlatformErasureRootProtection(builder.Configuration);

#if HEXALITH_PLATFORM_CATALOG_SDK
if (!string.IsNullOrEmpty(builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:CatalogPath"]))
    Hexalith.EventStore.Server.Routing.RunScopedRouteCatalogGateway.AddEventStoreRunScopedCatalogGateway(builder.Services, builder.Configuration);
#else
if (!string.IsNullOrEmpty(builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:Directory"]))
    throw new InvalidOperationException("Development catalog bootstrap requires the authoritative published owner SDK and adapters before package qualification.");
#endif
// Story 3.2 (AD-13): load the Platform-declared extension packages referenced by the generated bindings,
// verify each assembly identity against its declaration and report them in the eventstore-extensions readiness check.
#if HEXALITH_PLATFORM_EXTENSION_SDK
Hexalith.EventStore.Server.Extensions.EventStoreExtensionServiceCollectionExtensions.AddEventStoreExtensions(builder.Services, builder.Configuration);
#else
if (builder.Configuration.GetSection("EventStore:Extensions:Packages").GetChildren().Any())
    throw new InvalidOperationException("eventstore-extension-api-unavailable: Declared EventStore extensions require an EventStore server SDK that carries the extension API; package-mode qualification awaits its release.");
#endif

WebApplication app = builder.Build();

// Middleware order per architecture: CorrelationIdMiddleware FIRST (before everything)
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.MapDefaultEndpoints();
app.UseRouting();
app.UseAuthentication();
app.UseRateLimiter();
// OpenAPI/Swagger UI (gated by configuration, H13)
if (app.Configuration.GetValue("EventStore:OpenApi:Enabled", true)) {
    _ = app.MapOpenApi().RequireAuthorization(EventStoreHttpHostSecurityExtensions.HumanPolicy);
    _ = app.UseEventStoreHumanSurface("/swagger");
    _ = app.UseSwaggerUI(options => {
        options.SwaggerEndpoint("/openapi/v1.json", "Hexalith EventStore API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseEventStoreHttpHostSecurity();
app.UseAuthorization();
#if HEXALITH_PLATFORM_CATALOG_SDK
if (!string.IsNullOrEmpty(app.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:CatalogPath"]))
{
#if HEXALITH_PLATFORM_TECHNICAL_CATALOG_SDK
    if (Hexalith.EventStore.ServiceDefaults.Routing.TechnicalHttpRouteCatalogReceiver.IsEnabled(app.Configuration))
        Hexalith.EventStore.ServiceDefaults.Routing.TechnicalHttpRouteCatalogReceiver.UseDevelopmentTechnicalHttpCatalogReceiver(app,
            Hexalith.EventStore.Server.Routing.RunScopedRouteCatalogGateway.Operation);
    else
#endif
        Hexalith.EventStore.ServiceDefaults.Routing.FiniteRouteCatalogReceiver.UseFiniteDevelopmentRouteCatalogReceiver(app,
            Hexalith.EventStore.Server.Routing.RunScopedRouteCatalogGateway.Operation);
}
#endif
app.UseCloudEvents();


bool declarativeSubscriptions = app.Configuration.GetValue<bool>("Dapr:DeclarativeSubscriptions");
app.MapControllers().Add(endpoint =>
{
    for (int index = endpoint.Metadata.Count - 1; index >= 0; index--)
    {
        if (endpoint.Metadata[index] is not ITopicMetadata { Name: "*.*.projection-changed" } topic) continue;
        if (declarativeSubscriptions) endpoint.Metadata.RemoveAt(index);
        else endpoint.Metadata[index] = new TopicAttribute(app.Services.GetRequiredService<IOptions<ProjectionChangeNotifierOptions>>().Value.PubSubName, topic.Name);
    }
});
app.MapErrorReferences();
app.MapApiVersionFallback();
if (!declarativeSubscriptions) app.MapSubscribeHandler().RequireEventStoreChannel();
app.MapActorsHandlers().WithMetadata(EventStoreActorCallbackEndpointMetadata.Instance).RequireEventStoreChannel();
app.MapApplicationRuntimeProofEndpoints();

// SignalR hub for real-time projection change notifications (conditional)
SignalROptions? signalROptions = app.Configuration
    .GetSection("EventStore:SignalR")
    .Get<SignalROptions>();

if (signalROptions?.Enabled == true) {
    _ = app.MapHub<ProjectionChangedHub>(ProjectionChangedHub.HubPath);
    _ = app.MapSignalRRuntimeProofEndpoints();
}

// Configure global request body size limit (1MB)
app.Lifetime.ApplicationStarted.Register(() => {
    KestrelServerOptions? kestrelOptions = app.Services.GetService<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>();
    _ = kestrelOptions?.Limits.MaxRequestBodySize = 1_048_576;
});

app.Run();

/// <summary>
/// Entry point class, made partial for WebApplicationFactory test access.
/// </summary>
public partial class Program;
