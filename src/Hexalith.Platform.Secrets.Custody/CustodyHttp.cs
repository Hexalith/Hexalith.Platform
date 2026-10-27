using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hexalith.Platform.Custody;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Hexalith.Platform.Secrets.Custody;

/// <summary>A bounded authenticated receiving surface over the existing custody service.</summary>
public static class CustodyHttp
{
    /// <summary>Requires the provisioned HTTPS issuer, exact audience, enrolled caller and operation.</summary>
    public static void AddAuthentication(IServiceCollection services, IConfiguration configuration)
    {
        string issuer = configuration["Custody:CallerIssuer"] ?? "";
        if (!Uri.TryCreate(issuer, UriKind.Absolute, out Uri? endpoint) || endpoint.Scheme != "https")
        { throw new ArgumentException("An explicit HTTPS custody caller issuer is required."); }
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.Authority = issuer;
            options.Audience = "ext-secrets-1-custody";
            options.RequireHttpsMetadata = true;
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = true, ValidIssuer = issuer,
                ValidateAudience = true, ValidAudience = options.Audience, IgnoreTrailingSlashWhenValidatingAudience = false,
                ValidateLifetime = true, ClockSkew = TimeSpan.Zero,
                ValidateIssuerSigningKey = true, ValidAlgorithms = [SecurityAlgorithms.RsaSha256] };
            if (configuration["Custody:IssuerCaFile"] is { Length: > 0 } caFile)
            {
                var certificates = new X509Certificate2Collection();
                certificates.ImportFromPemFile(caFile);
                var policy = new X509ChainPolicy { TrustMode = X509ChainTrustMode.CustomRootTrust, RevocationMode = X509RevocationMode.NoCheck };
                policy.CustomTrustStore.AddRange(certificates);
                var handler = new SocketsHttpHandler { AllowAutoRedirect = false };
                handler.SslOptions.CertificateChainPolicy = policy;
                options.Backchannel = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
            }
        });
        services.AddAuthorization(options => options.AddPolicy("custody-digest", policy => policy.RequireAuthenticatedUser()
            .RequireClaim("eventstore:operation", "custody:digest").RequireAssertion(context =>
                context.User.FindAll("azp").Select(claim => claim.Value).ToArray() is { Length: 1 } callers
                && configuration.GetSection("Custody:EnrolledCallerIds").Get<string[]>() is { } enrolled
                && enrolled.Contains(callers[0], StringComparer.Ordinal))));
    }

    /// <summary>Health never claims dependency availability or executes cryptographic qualification.</summary>
    public static void Map(WebApplication app, CustodyBinding binding)
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.Value is "/alive" or "/health" or "/ready") { await next(); return; }
            byte[] expected = Encoding.UTF8.GetBytes(app.Configuration["APP_API_TOKEN"] ?? "");
            byte[] received = Encoding.UTF8.GetBytes(context.Request.Headers["dapr-api-token"].ToString());
            if (expected.Length == 0 || expected.Length != received.Length || !CryptographicOperations.FixedTimeEquals(expected, received))
            { context.Response.StatusCode = 401; return; }
            try { await next(); }
            catch (JsonException) { context.Response.StatusCode = 400; }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { context.Response.StatusCode = 503; }
        });
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/alive", () => Results.Ok(new { status = "Alive" }));
        app.MapGet("/health", () => Results.Ok(new { status = "InstalledCustody", missingHmacInput = binding.MissingInput,
            dependencyAvailable = false, qualificationPassed = false, lifecycleProviderInstalled = false, decisionSignerInstalled = false }));
        app.MapGet("/ready", async (CancellationToken token) => await binding.ReadyAsync(token)
            ? Results.Ok(new { status = "MetadataBound", dependencyAvailable = false }) : Results.StatusCode(503));
        app.MapPost("/digest", async (HttpContext context, PlatformHmacService hmac) =>
        {
            DigestRequest? request = await ReadAsync(context);
            if (request is null || request.Components is null || request.Components.Length > 128 || request.Components.Any(value => value?.Length > 4096)
                || string.IsNullOrWhiteSpace(request.TenantId) || !binding.Permits(request.TenantId, PlatformHmacPurpose.ContentDigest))
            { return Results.BadRequest(); }
            if (!context.User.HasClaim("eventstore:tenant", request.TenantId)) { return Results.StatusCode(403); }
            PlatformHmacResult result = await hmac.DigestAsync(new(request.TenantId, PlatformHmacPurpose.ContentDigest),
                request.Components, request.RecordedVersion, context.RequestAborted);
            return result.Status == CustodyStatus.Succeeded ? Results.Ok(result) : Results.StatusCode(503);
        }).RequireAuthorization("custody-digest");
    }

    private static async Task<DigestRequest?> ReadAsync(HttpContext context)
    {
        const int maximum = 64 * 1024;
        if (context.Request.ContentLength > maximum) { throw new JsonException(); }
        using var stream = new MemoryStream();
        byte[] buffer = new byte[4096];
        int count;
        while ((count = await context.Request.Body.ReadAsync(buffer, context.RequestAborted)) != 0)
        { if (stream.Length + count > maximum) { throw new JsonException(); } stream.Write(buffer, 0, count); }
        using JsonDocument document = JsonDocument.Parse(stream.ToArray(), new JsonDocumentOptions { MaxDepth = 16 });
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || document.RootElement.EnumerateObject().Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() != document.RootElement.EnumerateObject().Count())
        { throw new JsonException(); }
        return document.RootElement.Deserialize<DigestRequest>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
            { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, PropertyNameCaseInsensitive = false, RespectRequiredConstructorParameters = true });
    }

    private sealed record DigestRequest(string TenantId, string?[] Components, string? RecordedVersion = null);
}
