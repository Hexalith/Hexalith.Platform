using Hexalith.EventStore;
using Hexalith.Platform.Identity;
using Hexalith.EventStore.Extensions;
using Hexalith.EventStore.HealthChecks;
using Hexalith.EventStore.Middleware;
using Hexalith.EventStore.OpenApi;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.ServiceDefaults;
using Hexalith.EventStore.SignalRHub;

using Microsoft.AspNetCore.Server.Kestrel.Core;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddDaprClient();
builder.Services.AddHealthChecks()
    .AddEventStoreDaprHealthChecks()
    .AddEventStoreAppChannelTokenHealthCheck();
builder.Services.AddEventStore();
builder.Services.AddEventStoreServer(builder.Configuration);
builder.Services.AddEventStoreDomainQueryRouting();
builder.Services.AddEventStoreSignalR(builder.Configuration);
builder.Services.AddPlatformIdentity(builder.Configuration);

WebApplication app = builder.Build();

// Middleware order per architecture: CorrelationIdMiddleware FIRST (before everything)
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.MapDefaultEndpoints();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.UseCloudEvents();

// OpenAPI/Swagger UI (gated by configuration, H13)
if (app.Configuration.GetValue("EventStore:OpenApi:Enabled", true)) {
    _ = app.MapOpenApi();
    _ = app.UseSwaggerUI(options => {
        options.SwaggerEndpoint("/openapi/v1.json", "Hexalith EventStore API v1");
        options.RoutePrefix = "swagger";
    });
}

app.MapControllers();
app.MapErrorReferences();
app.MapApiVersionFallback();
app.MapSubscribeHandler();
app.MapActorsHandlers();
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
