using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Hexalith.Platform.Secrets.Authority;

/// <summary>Separately scoped JWT and app-channel authentication for queries and owner administration.</summary>
public static class AuthorityHttp
{
    /// <summary>Requires HTTPS discovery, exact issuer/audience, RS256, operation, and an explicit owner administrator.</summary>
    public static void AddAuthentication(IServiceCollection services, IConfiguration configuration)
    {
        string issuer = configuration["Authority:CallerIssuer"] ?? "";
        if (!Uri.TryCreate(issuer, UriKind.Absolute, out Uri? endpoint) || endpoint.Scheme != "https")
        { throw new ArgumentException("An independently provisioned HTTPS authority issuer is required."); }
        var authentication = services.AddAuthentication();
        foreach (string scheme in new[] { "authority-query", "authority-administrator" })
        {
            authentication.AddJwtBearer(scheme, options =>
            {
                options.Authority = issuer;
                options.Audience = scheme == "authority-query" ? "ext-secrets-1-hmac-authority" : "ext-secrets-1-hmac-authority-admin";
                options.RequireHttpsMetadata = true;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = true, ValidIssuer = issuer,
                    ValidateAudience = true, ValidAudience = options.Audience, IgnoreTrailingSlashWhenValidatingAudience = false,
                    ValidateLifetime = true, ClockSkew = TimeSpan.Zero,
                    ValidateIssuerSigningKey = true, ValidAlgorithms = [SecurityAlgorithms.RsaSha256] };
                if (configuration["Authority:IssuerCaFile"] is { Length: > 0 } caFile)
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
        }
        services.AddAuthorization(options =>
        {
            options.AddPolicy("authority-query", policy => policy.AddAuthenticationSchemes("authority-query").RequireAuthenticatedUser()
                .RequireClaim("eventstore:operation", "hmac:query").RequireAssertion(context => SingleCaller(context.User) is not null));
            options.AddPolicy("authority-administrator", policy => policy.AddAuthenticationSchemes("authority-administrator").RequireAuthenticatedUser()
                .RequireClaim("eventstore:operation", "hmac:administer").RequireAssertion(context =>
                    configuration["Authority:AdministratorClientId"] is { Length: > 0 } administrator && SingleCaller(context.User) == administrator));
        });
    }

    /// <summary>Maps the unchanged query contracts and the distinct owner operation protocol.</summary>
    public static void Map(WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            try
            {
                if (context.Request.Path.Value is "/alive" or "/health" or "/ready") { await next(); return; }
                bool administrator = context.Request.Path.StartsWithSegments("/admin");
                string? expected = administrator
                    ? await File.ReadAllTextAsync(app.Configuration["Authority:AdministratorChannelTokenFile"]!, context.RequestAborted).ConfigureAwait(false)
                    : app.Configuration["APP_API_TOKEN"];
                string received = context.Request.Headers[administrator ? "authority-admin-token" : "dapr-api-token"].ToString();
                byte[] expectedBytes = Encoding.UTF8.GetBytes(expected?.Trim() ?? "");
                byte[] actualBytes = Encoding.UTF8.GetBytes(received);
                if (expectedBytes.Length == 0 || expectedBytes.Length != actualBytes.Length || !CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes))
                { context.Response.StatusCode = 401; return; }
                await next();
            }
            catch (AuthorityConflictException) { context.Response.StatusCode = 409; }
            catch (ArgumentException) { context.Response.StatusCode = 400; }
            catch (JsonException) { context.Response.StatusCode = 400; }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { context.Response.StatusCode = 503; }
        });
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/alive", () => Results.Ok(new { status = "Alive" }));
        app.MapGet("/health", async (IAuthorityRepository repository, CancellationToken token) =>
        {
            AuthorityState state = await repository.ReadAsync(token).ConfigureAwait(false);
            return Results.Ok(new { status = "InstalledAuthority", revision = state.Revision, profilePresent = state.Profile is not null,
                inventoryHostCount = state.Hosts.Count(item => item.Enrolled), exactKeyStatusCount = state.KeyStatuses.Length,
                dependencyAvailable = false, qualificationPassed = false });
        });
        app.MapGet("/ready", async (IAuthorityRepository repository, CancellationToken token) =>
        { await repository.ReadAsync(token).ConfigureAwait(false); return Results.Ok(new { status = "AuthorityStorageReady", dependencyAvailable = false }); });
        app.MapPost("/query-profile", async (HttpContext context, AuthorityEvaluator authority) =>
        {
            JsonObject? body = await Body<JsonObject>(context).ConfigureAwait(false);
            if (body?.Count != 1 || body["profileReference"] is not JsonValue value || !value.TryGetValue(out string? reference) || reference is null)
            { return Results.StatusCode(503); }
            JsonObject? profile = await authority.ProfileAsync(SingleCaller(context.User)!, reference, context.RequestAborted).ConfigureAwait(false);
            return profile is null ? Results.StatusCode(503) : Results.Ok(profile);
        }).RequireAuthorization("authority-query");
        app.MapPost("/query-hmac-revocation", async (HttpContext context, AuthorityEvaluator authority) =>
        {
            AuthorityRevocationQuery? body = await Body<AuthorityRevocationQuery>(context).ConfigureAwait(false);
            JsonObject? proof = body is null ? null : await authority.RevocationAsync(SingleCaller(context.User)!, body, context.RequestAborted).ConfigureAwait(false);
            return proof is null ? Results.StatusCode(503) : Results.Ok(proof);
        }).RequireAuthorization("authority-query");
        app.MapGet("/admin/state", async (IAuthorityRepository repository, CancellationToken token)
            => Results.Ok(await repository.ReadAsync(token).ConfigureAwait(false))).RequireAuthorization("authority-administrator");
        app.MapPost("/admin/operations", async (HttpContext context, IAuthorityRepository repository, TimeProvider clock) =>
        {
            AuthorityAdministration? operation = await Body<AuthorityAdministration>(context).ConfigureAwait(false);
            if (operation is null) { return Results.BadRequest(); }
            AuthorityReceipt receipt = await repository.AdministerAsync(SingleCaller(context.User)!, operation, clock.GetUtcNow(), context.RequestAborted).ConfigureAwait(false);
            return Results.Json(receipt, statusCode: receipt.Outcome == "Applied" ? 200 : 409);
        }).RequireAuthorization("authority-administrator");
    }

    private static string? SingleCaller(System.Security.Claims.ClaimsPrincipal principal)
    {
        string[] callers = principal.FindAll("azp").Select(claim => claim.Value).ToArray();
        return callers.Length == 1 && !string.IsNullOrWhiteSpace(callers[0]) ? callers[0] : null;
    }

    private static async Task<T?> Body<T>(HttpContext context)
    {
        const int maximum = 64 * 1024;
        if (context.Request.ContentLength > maximum) { throw new JsonException(); }
        using var stream = new MemoryStream();
        byte[] buffer = new byte[4096];
        int count;
        while ((count = await context.Request.Body.ReadAsync(buffer, context.RequestAborted).ConfigureAwait(false)) != 0)
        { if (stream.Length + count > maximum) { throw new JsonException(); } stream.Write(buffer, 0, count); }
        using JsonDocument document = JsonDocument.Parse(stream.ToArray(), new JsonDocumentOptions { MaxDepth = 32 });
        if (document.RootElement.ValueKind != JsonValueKind.Object || Duplicate(document.RootElement)) { throw new JsonException(); }
        return document.RootElement.Deserialize<T>(AuthorityJson.Options);
    }

    private static bool Duplicate(JsonElement element)
        => element.ValueKind == JsonValueKind.Object ? element.EnumerateObject().Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() != element.EnumerateObject().Count()
            || element.EnumerateObject().Any(property => Duplicate(property.Value))
            : element.ValueKind == JsonValueKind.Array && element.EnumerateArray().Any(Duplicate);
}
