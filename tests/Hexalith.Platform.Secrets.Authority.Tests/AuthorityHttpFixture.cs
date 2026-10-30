using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Tokens;

namespace Hexalith.Platform.Secrets.Authority.Tests;

internal sealed class AuthorityHttpFixture(AuthorityTestState state, WebApplication app, RSA signing, HttpClient client,
    string administratorFile, ConcurrentDictionary<string, TaskCompletionSource> completed, CapturedAuthorityLogs logs) : IAsyncDisposable
{
    public AuthorityTestState State { get; } = state;
    public HttpClient Client { get; } = client;
    public string AdministratorFile { get; } = administratorFile;
    public string Diagnostics => string.Join('\n', logs.Messages);
    public Task RequestCompleted(string id) => completed.GetOrAdd(id, _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;

    public string Token(string caller = AuthorityTestState.Caller, string? operation = "hmac:query", string audience = "ext-secrets-1-hmac-authority",
        string issuer = AuthorityTestState.Issuer, bool expired = false, bool wrongSignature = false)
    {
        using RSA other = RSA.Create(2048);
        List<Claim> claims = [new("azp", caller)];
        if (operation is not null) { claims.Add(new("eventstore:operation", operation)); }
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(issuer, audience, claims,
            DateTime.UtcNow.AddMinutes(-2), expired ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddMinutes(1),
            new SigningCredentials(new RsaSecurityKey(wrongSignature ? other : signing), SecurityAlgorithms.RsaSha256)));
    }

    public string AdministratorToken => Token(AuthorityTestState.Administrator, "hmac:administer", "ext-secrets-1-hmac-authority-admin");

    public static Task<AuthorityHttpFixture> Start(bool seeded = true, AdministratorChannelTokenReader? channelReader = null)
        => StartWithCancellation(seeded, channelReader, TestContext.Current.CancellationToken);

    public static async Task<AuthorityHttpFixture> StartWithCancellation(bool seeded, AdministratorChannelTokenReader? channelReader,
        CancellationToken requestCancellation)
    {
        var state = new AuthorityTestState();
        if (seeded) { await state.Seed(); } else { await state.Repository.ReadAsync(TestContext.Current.CancellationToken); }
        string administratorFile = Path.Combine(state.DirectoryPath, "offline-administrator-channel");
        await File.WriteAllTextAsync(administratorFile, "offline-administrator-channel", TestContext.Current.CancellationToken);
        var builder = WebApplication.CreateBuilder();
        var logs = new CapturedAuthorityLogs();
        var completed = new ConcurrentDictionary<string, TaskCompletionSource>();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(logs);
        builder.Logging.SetMinimumLevel(LogLevel.Trace);
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authority:CallerIssuer"] = AuthorityTestState.Issuer, ["Authority:AdministratorClientId"] = AuthorityTestState.Administrator,
            ["APP_API_TOKEN"] = "offline-receiver-channel", ["Authority:AdministratorChannelTokenFile"] = administratorFile,
        });
        AuthorityHttp.AddAuthentication(builder.Services, builder.Configuration);
        if (channelReader is not null) { builder.Services.AddSingleton(channelReader); }
        RSA signing = RSA.Create(2048);
        foreach (string scheme in new[] { "authority-query", "authority-administrator" })
        {
            builder.Services.PostConfigure<JwtBearerOptions>(scheme, options =>
            {
                var configuration = new OpenIdConnectConfiguration { Issuer = AuthorityTestState.Issuer };
                configuration.SigningKeys.Add(new RsaSecurityKey(signing));
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
            });
        }
        builder.Services.AddSingleton<IAuthorityRepository>(state.Repository);
        builder.Services.AddSingleton<TimeProvider>(state.Clock);
        builder.Services.AddSingleton(state.Evaluator);
        var app = builder.Build();
        // Emulates only Dapr's API/app channel hop. JWT validation and HTTP handlers remain the actual application code.
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/v1.0/invoke"))
            {
                string[] parts = context.Request.Path.Value!.Split('/');
                if (parts[3] != AuthorityTestState.AppId || context.Request.Headers["dapr-api-token"] != "offline-sidecar-channel")
                { context.Response.StatusCode = 401; return; }
                context.Request.Path = "/" + parts[^1];
                context.Request.Headers["dapr-api-token"] = "offline-receiver-channel";
            }
            // Deterministic RequestAborted injection avoids HTTP/1 unread-body disconnect timing in cancellation tests.
            using var requestAborted = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted,
                context.Request.Headers["fixture-cancellable"] == "true" ? requestCancellation : CancellationToken.None);
            context.RequestAborted = requestAborted.Token;
            try { await next(); }
            finally
            {
                if (context.Request.Headers["fixture-request-id"].ToString() is { Length: > 0 } id)
                { completed.GetOrAdd(id, _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult(); }
            }
        });
        app.UseRouting();
        AuthorityHttp.Map(app);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return new(state, app, signing, new HttpClient { BaseAddress = new Uri(app.Urls.Single() + "/") }, administratorFile, completed, logs);
    }
    public async ValueTask DisposeAsync() { Client.Dispose(); signing.Dispose(); await app.DisposeAsync(); State.Dispose(); }
}

internal sealed class CapturedAuthorityLogs : ILoggerProvider
{
    internal ConcurrentQueue<string> Messages { get; } = new();
    public ILogger CreateLogger(string categoryName) => new Capture(Messages);
    public void Dispose() { }
    private sealed class Capture(ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => messages.Enqueue(formatter(state, exception) + exception?.ToString());
    }
}
