using System.Text.Json.Nodes;
using Hexalith.Platform.Secrets.Custody;
using Hexalith.Platform.Secrets.Inventory;
using Hexalith.Platform.Secrets.Runtime;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 64 * 1024);
SecretsRuntimeConfiguration runtime = SecretsRuntimeConfiguration.Load(builder.Configuration["Custody:RuntimeReferenceFile"]!)
    ?? throw new InvalidOperationException("A valid public runtime reference is required.");
JsonArray permitted = [];
builder.Services.AddSingleton<IHostedService>(new PermittedReferencesStartup(
    builder.Configuration["Custody:PermittedReferencesFile"], permitted, new PermittedReferencesFileReader()));
var workload = new InventoryWorkloadToken(builder.Configuration);
var transport = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
    { BaseAddress = new Uri("http://127.0.0.1:3500/"), Timeout = TimeSpan.FromSeconds(30) };
var invoker = new AuthenticatedSecretsInvoker(transport, workload, () => builder.Configuration["DAPR_API_TOKEN"]);
builder.Services.AddSingleton(new Dapr.Client.DaprClientBuilder().Build());
CustodyHttp.AddAuthentication(builder.Services, builder.Configuration);
CustodyBinding binding = CustodyBinding.Compose(builder.Services, runtime, invoker,
    permitted, () => workload.Configured);
builder.Services.AddSingleton(binding);
WebApplication app = builder.Build();
CustodyHttp.Map(app, binding);
await app.RunAsync();
