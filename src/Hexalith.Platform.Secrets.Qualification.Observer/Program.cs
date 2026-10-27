using Hexalith.Platform.Secrets.Runtime;
using Hexalith.Platform.Protection;

if(args.Length==1 && args[0]=="--serve-phase-source")
{
    Environment.ExitCode=await JointPhaseSourceExecutable.RunAsync(Environment.GetEnvironmentVariable("PLATFORM_JOINT_PHASE_SOURCE_INSTALLATION"));
    return;
}

if (args.Length == 1 && args[0] == "--serve-owner")
{
    Environment.ExitCode = await QualificationOwnerDriverExecutable.RunAsync("observer", Environment.GetEnvironmentVariable("PLATFORM_QUALIFICATION_OWNER_INSTALLATION"));
    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(new Dapr.Client.DaprClientBuilder().Build());
builder.Services.AddSingleton(TimeProvider.System);
_ = builder.Services.AddInstalledQualificationRole("observer", builder.Configuration["PLATFORM_QUALIFICATION_INSTALLATION"]);
var app = builder.Build();
var receiving = new QualificationHttpReceiver();
SecretsRuntimeConfiguration configuration = args.Length > 0 ? SecretsRuntimeConfiguration.Load(args[0]) ?? new SecretsRuntimeConfiguration() : new SecretsRuntimeConfiguration();
app.MapPost("/ext-secrets-1/v1/observe", (HttpContext context, IServiceProvider services, CancellationToken cancellation) => Handle("observe", context, services, cancellation));
app.Run();

async Task<IResult> Handle(string operation, HttpContext context, IServiceProvider services, CancellationToken cancellation)
{
    return await receiving.HandleAsync("observer", operation, configuration, context,
        builder.Configuration["APP_API_TOKEN"], services.GetService<IQualificationCallerAuthenticator>(),
        services.GetService<IQualificationObserverProvider>(), services.GetService<IQualificationOriginalStore>(), services.GetService<IQualificationPublicProofVerifier>(), cancellation).ConfigureAwait(false);
}
