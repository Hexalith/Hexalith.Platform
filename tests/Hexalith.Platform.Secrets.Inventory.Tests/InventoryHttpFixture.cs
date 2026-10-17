using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Shouldly;

namespace Hexalith.Platform.Secrets.Inventory.Tests;

internal sealed class InventoryHttpFixture(WebApplication app, HttpClient client, RSA signing, SecretsRuntimeConfiguration config) : IAsyncDisposable
{
    public HttpClient Client { get; } = client;
    public SecretsRuntimeConfiguration Configuration { get; } = config;
    public string Token(string caller, bool operation = true) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("offline-independent-issuer", "ext-secrets-1-inventory",
        operation ? [new Claim("azp", caller), new Claim("eventstore:operation", "inventory:query")] : [new Claim("azp", caller)],
        DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1), new SigningCredentials(new RsaSecurityKey(signing), SecurityAlgorithms.RsaSha256)));

    public static async Task<InventoryHttpFixture> Start(string scenario = "allowed")
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["APP_API_TOKEN"] = "offline-receiver-channel", ["Inventory:EnrolledCallerIds:0"] = "offline-caller" });
        InventoryHttp.AddAuthentication(builder.Services, builder.Configuration);
        RSA signing = RSA.Create(2048);
        builder.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        { options.TokenValidationParameters = new TokenValidationParameters { ValidIssuer = "offline-independent-issuer", ValidAudience = "ext-secrets-1-inventory", IssuerSigningKey = new RsaSecurityKey(signing), ValidateIssuerSigningKey = true, ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ClockSkew = TimeSpan.Zero }; });
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var profile = PlatformAcceptedEnvelopeTiming.Bind("p", "issuer", "audience", now.AddMinutes(-1), now.AddHours(1));
        var config = InventoryBoundaryTests.Configuration(profile);
        if (scenario == "missing-binding") config = config with { ProviderBinding = null };
        JsonObject? metadata = scenario == "missing-metadata" ? null : InventoryAuthorityTests.Metadata();
        if (scenario == "missing-mapping") metadata!["custom_metadata"]!.AsObject().Remove("ext-secrets-1.versionMappings");
        var sources = new InventoryTestSources(metadata) { DisableAfterFirstRead = scenario == "disabled" };
        var allowed = InventoryBoundaryTests.Permitted();
        var authority = new CurrentInventoryAuthority(config, new InventoryTestProfile(profile), sources, allowed, TimeProvider.System);
        builder.Services.AddSingleton(authority);
        builder.Services.AddSingleton(new InventoryReadiness(config, authority, allowed, () => scenario != "missing-authenticated-transport"));
        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/v1.0/invoke"))
            {
                if (context.Request.Headers["dapr-api-token"] != "offline-dapr-api") { context.Response.StatusCode = 401; return; }
                context.Request.Path = "/" + context.Request.Path.Value!.Split('/')[^1];
                context.Request.Headers["dapr-api-token"] = "offline-receiver-channel";
            }
            await next();
        });
        app.UseRouting();
        InventoryHttp.Map(app, config, new JsonObject { ["permittedReferences"] = allowed }, null);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return new(app, new HttpClient { BaseAddress = new Uri(app.Urls.Single() + "/") }, signing, config);
    }
    public async ValueTask DisposeAsync() { Client.Dispose(); signing.Dispose(); await app.DisposeAsync(); }
}
