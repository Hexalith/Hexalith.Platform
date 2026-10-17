using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Inventory;
using Hexalith.Platform.Secrets.Runtime;

using Microsoft.AspNetCore.Authentication.JwtBearer;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
var daprClient = new Dapr.Client.DaprClientBuilder().Build();
var workload = new InventoryWorkloadToken(builder.Configuration);
var transport = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri("http://127.0.0.1:3500/"), Timeout = TimeSpan.FromSeconds(30) };
var daprInvoker = new AuthenticatedSecretsInvoker(transport, workload, () => builder.Configuration["DAPR_API_TOKEN"]);
builder.Services.AddSingleton(daprClient);
builder.Services.AddSingleton(daprInvoker);
InventoryHttp.AddAuthentication(builder.Services, builder.Configuration);
builder.Services.AddSingleton<IInventorySources, InventorySources>();
SecretsRuntimeConfiguration runtime = SecretsRuntimeConfiguration.Load(builder.Configuration["Inventory:RuntimeReferenceFile"]!)
    ?? throw new InvalidOperationException("A valid public runtime reference file is required.");
JsonObject configuration = InventorySources.ParseObject(File.ReadAllText(builder.Configuration["Inventory:PermittedReferencesFile"]!))
    ?? throw new InvalidOperationException("A valid exact-reference allowlist is required.");
builder.Services.AddSingleton(runtime);

builder.Services.AddSingleton(provider => new CurrentInventoryAuthority(runtime,
    new DaprHmacCurrentTrust(runtime, provider.GetRequiredService<AuthenticatedSecretsInvoker>(), TimeProvider.System),
    provider.GetRequiredService<IInventorySources>(), configuration["permittedReferences"] as JsonArray ?? [], TimeProvider.System));
// Use the completed opt-in exactly as shipped. Missing inputs leave its unavailable defaults intact.
string? missingHmacInput = builder.Services.AddExplicitHmac(runtime, daprInvoker);
builder.Services.AddSingleton<ISecretsDaprInvoker>(daprInvoker);
builder.Services.AddSingleton(provider => new InventoryReadiness(runtime, provider.GetRequiredService<CurrentInventoryAuthority>(),
    configuration["permittedReferences"] as JsonArray ?? [], () => workload.Configured
        && !string.IsNullOrWhiteSpace(builder.Configuration["Inventory:CallerAuthority"])
        && builder.Configuration.GetSection("Inventory:EnrolledCallerIds").Get<string[]>() is { Length: > 0 }));
WebApplication app = builder.Build();
InventoryHttp.Map(app, runtime, configuration, missingHmacInput);
app.Run();
