using Hexalith.Platform.Secrets.Runtime;
using Hexalith.Platform.Protection;

if(args.Length==1 && args[0] is "--phase-command" or "--phase-finalize")
{
    Environment.ExitCode=await JointPhaseCommandExecutable.RunAsync(Environment.GetEnvironmentVariable("PLATFORM_JOINT_PHASE_COMMAND_INSTALLATION"),finalizeOnly:args[0]=="--phase-finalize");
    return;
}

if (args.Length == 1 && args[0] == "--serve-owner")
{
    Environment.ExitCode = await QualificationOwnerDriverExecutable.RunAsync("executor", Environment.GetEnvironmentVariable("PLATFORM_QUALIFICATION_OWNER_INSTALLATION"));
    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(new Dapr.Client.DaprClientBuilder().Build());
builder.Services.AddSingleton(TimeProvider.System);
_ = builder.Services.AddInstalledQualificationRole("executor", builder.Configuration["PLATFORM_QUALIFICATION_INSTALLATION"]);
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
