using Hexalith.Platform.Secrets.Authority;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 64 * 1024);
string directory = builder.Configuration["Authority:StateDirectory"] ?? throw new InvalidOperationException("Separate durable authority storage is required.");
string appId = builder.Configuration["Authority:AppId"] ?? throw new InvalidOperationException("An explicit independent authority app ID is required.");
AuthorityHttp.AddAuthentication(builder.Services, builder.Configuration);
builder.Services.AddSingleton<IAuthorityRepository>(new AuthorityRepository(directory));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(provider => new AuthorityEvaluator(appId, provider.GetRequiredService<IAuthorityRepository>(), provider.GetRequiredService<TimeProvider>()));
WebApplication app = builder.Build();
AuthorityHttp.Map(app);
app.Run();
