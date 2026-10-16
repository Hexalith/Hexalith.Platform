using Hexalith.Platform.Secrets.Runtime;

var builder = WebApplication.CreateBuilder(args);
// A deployment composes role-specific providers and an independently provisioned durable store.
// No production provider or credential is inferred from a configuration reference.
var app = builder.Build();
SecretsRuntimeConfiguration configuration = args.Length > 0 ? SecretsRuntimeConfiguration.Load(args[0]) ?? new SecretsRuntimeConfiguration() : new SecretsRuntimeConfiguration();
app.MapPost("/ext-secrets-1/v1/execute", (HttpRequest request, IServiceProvider services, CancellationToken cancellation) => Handle("execute", request, services, cancellation));
app.MapPost("/ext-secrets-1/v1/lookup", (HttpRequest request, IServiceProvider services, CancellationToken cancellation) => Handle("lookup", request, services, cancellation));
app.Run();

async Task<IResult> Handle(string operation, HttpRequest request, IServiceProvider services, CancellationToken cancellation)
{
    using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
    budget.CancelAfter(TimeSpan.FromSeconds(30));
    var original = await QualificationHost.ReadRequestAsync(request.Body, budget.Token);
    QualificationHostResult result = await QualificationHost.HandleAsync("executor", operation, configuration, original,
        services.GetService<IQualificationExecutorProvider>(), services.GetService<IQualificationOriginalStore>(), services.GetService<IQualificationPublicProofVerifier>(), budget.Token);
    return Results.Text(result.Body.ToJsonString(), "application/json", statusCode: result.StatusCode);
}
