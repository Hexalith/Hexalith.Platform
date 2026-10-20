using Hexalith.Platform.Secrets.Runtime;

var builder = WebApplication.CreateBuilder(args);
// A deployment composes role-specific providers and an independently provisioned durable store.
// No production provider or credential is inferred from a configuration reference.
var app = builder.Build();
SecretsRuntimeConfiguration configuration = args.Length > 0 ? SecretsRuntimeConfiguration.Load(args[0]) ?? new SecretsRuntimeConfiguration() : new SecretsRuntimeConfiguration();
app.MapPost("/ext-secrets-1/v1/execute", (HttpContext context, IServiceProvider services, CancellationToken cancellation) => Handle("execute", context, services, cancellation));
app.MapPost("/ext-secrets-1/v1/lookup", (HttpContext context, IServiceProvider services, CancellationToken cancellation) => Handle("lookup", context, services, cancellation));
app.Run();

async Task<IResult> Handle(string operation, HttpContext context, IServiceProvider services, CancellationToken cancellation)
{
    QualificationHostResult result = await QualificationHttpHost.HandleAsync("executor", operation, configuration, context,
        builder.Configuration["APP_API_TOKEN"], services.GetService<IQualificationCallerAuthenticator>(),
        services.GetService<IQualificationExecutorProvider>(), services.GetService<IQualificationOriginalStore>(), services.GetService<IQualificationPublicProofVerifier>(), cancellation).ConfigureAwait(false);
    return Results.Text(result.Body.ToJsonString(), "application/json", statusCode: result.StatusCode);
}
