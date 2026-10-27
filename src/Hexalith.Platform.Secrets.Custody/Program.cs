using System.Text.Json.Nodes;
using Hexalith.Platform.Secrets.Custody;
using Hexalith.Platform.Secrets.Inventory;
using Hexalith.Platform.Secrets.Runtime;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 64 * 1024);
SecretsRuntimeConfiguration runtime = SecretsRuntimeConfiguration.Load(builder.Configuration["Custody:RuntimeReferenceFile"]!)
    ?? throw new InvalidOperationException("A valid public runtime reference is required.");
JsonObject allowed = InventorySources.ParseObject(File.ReadAllText(builder.Configuration["Custody:PermittedReferencesFile"]!))
    ?? throw new InvalidOperationException("An exact public reference allowlist is required.");
var workload = new InventoryWorkloadToken(builder.Configuration);
var transport = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
    { BaseAddress = new Uri("http://127.0.0.1:3500/"), Timeout = TimeSpan.FromSeconds(30) };
var invoker = new AuthenticatedSecretsInvoker(transport, workload, () => builder.Configuration["DAPR_API_TOKEN"]);
builder.Services.AddSingleton(new Dapr.Client.DaprClientBuilder().Build());
CustodyHttp.AddAuthentication(builder.Services, builder.Configuration);
CustodyBinding binding = CustodyBinding.Compose(builder.Services, runtime, invoker,
    allowed["permittedReferences"] as JsonArray ?? [], () => workload.Configured);
builder.Services.AddSingleton(binding);
WebApplication app = builder.Build();
CustodyHttp.Map(app, binding);
app.Run();
