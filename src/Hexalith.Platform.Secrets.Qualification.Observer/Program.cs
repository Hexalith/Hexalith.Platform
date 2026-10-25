using Hexalith.Platform.Secrets.Runtime;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(new Dapr.Client.DaprClientBuilder().Build());
builder.Services.AddSingleton(TimeProvider.System);
_ = builder.Services.AddInstalledQualificationRole("observer", builder.Configuration["PLATFORM_QUALIFICATION_INSTALLATION"]);
var app = builder.Build();
SecretsRuntimeConfiguration configuration = args.Length > 0 ? SecretsRuntimeConfiguration.Load(args[0]) ?? new SecretsRuntimeConfiguration() : new SecretsRuntimeConfiguration();
app.MapPost("/ext-secrets-1/v1/observe", (HttpContext context, IServiceProvider services, CancellationToken cancellation) => Handle("observe", context, services, cancellation));
app.Run();

async Task<IResult> Handle(string operation, HttpContext context, IServiceProvider services, CancellationToken cancellation)
{
    QualificationHostResult result = await QualificationHttpHost.HandleAsync("observer", operation, configuration, context,
        builder.Configuration["APP_API_TOKEN"], services.GetService<IQualificationCallerAuthenticator>(),
        services.GetService<IQualificationObserverProvider>(), services.GetService<IQualificationOriginalStore>(), services.GetService<IQualificationPublicProofVerifier>(), cancellation).ConfigureAwait(false);
    return Results.Text(result.Body.ToJsonString(), "application/json", statusCode: result.StatusCode);
}
