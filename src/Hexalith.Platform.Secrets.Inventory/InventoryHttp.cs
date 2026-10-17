using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Hexalith.Platform.Secrets.Inventory;

/// <summary>Authenticated HTTP receiver shared by production and isolated HTTP verification.</summary>
public static class InventoryHttp
{
    /// <summary>Verifies the independently supplied issuer and explicitly enrolled workload caller.</summary>
    public static void AddAuthentication(IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.Authority = configuration["Inventory:CallerAuthority"];
            options.Audience = "ext-secrets-1-inventory";
            options.RequireHttpsMetadata = true;
            options.MapInboundClaims = false;
        });
        services.AddAuthorization(options => options.AddPolicy("enrolled-inventory-caller", policy =>
            policy.RequireAuthenticatedUser().RequireAssertion(context =>
                configuration.GetSection("Inventory:EnrolledCallerIds").Get<string[]>() is { Length: > 0 } callers
                && callers.Contains(context.User.FindFirst("azp")?.Value, StringComparer.Ordinal)
                && context.User.HasClaim("eventstore:operation", "inventory:query"))));
    }

    /// <summary>Maps the existing adapter contracts without bypassing channel or bearer authentication.</summary>
    public static void Map(WebApplication app, SecretsRuntimeConfiguration runtime, JsonObject configuration, string? missingHmacInput)
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.Value is "/alive" or "/ready" or "/health") { await next(); return; }
            string? expected = app.Configuration["APP_API_TOKEN"];
            string received = context.Request.Headers["dapr-api-token"].ToString();
            byte[] expectedBytes = Encoding.UTF8.GetBytes(expected ?? "");
            byte[] receivedBytes = Encoding.UTF8.GetBytes(received);
            if (string.IsNullOrWhiteSpace(expected) || expectedBytes.Length != receivedBytes.Length
                || !CryptographicOperations.FixedTimeEquals(expectedBytes, receivedBytes))
            { context.Response.StatusCode = 401; return; }
            await next();
        });
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/alive", () => Results.Ok(new { status = "Alive" }));
        app.MapGet("/health", () => Results.Ok(new { status = "InstalledUnbound", qualificationPassed = false, dependencyAvailable = false, missingHmacInput,
            independentProfilePresent = runtime.ExpectedProfile is not null, exactPermittedReferenceCount = (configuration["permittedReferences"] as JsonArray)?.Count ?? 0 }));
        app.MapGet("/ready", async (InventoryReadiness readiness, CancellationToken token) =>
            await readiness.ReadyAsync(token) ? Results.Ok(new { status = "Bound" }) : Results.StatusCode(503));
        app.MapPost("/query-profile", (CurrentInventoryAuthority authority, JsonObject request) =>
            QueryText(request, "profileReference", required: true, out string? reference) && authority.GetCurrentProfile(reference!) is { } profile
                ? Results.Ok(new { issuer = profile.Issuer, audience = profile.Audience, version = profile.Version, notBefore = profile.NotBefore, validUntil = profile.ValidUntil })
                : Results.StatusCode(503)).RequireAuthorization("enrolled-inventory-caller");
        app.MapPost("/query-inventory", async (CurrentInventoryAuthority authority, JsonObject request, CancellationToken token) =>
        {
            if (!QueryText(request, "tenantId", required: true, out string? tenant)
                || !QueryText(request, "purpose", required: true, out string? purpose)
                || !Enum.TryParse(purpose, out PlatformHmacPurpose parsed) || parsed.ToString() != purpose
                || !QueryText(request, "authorityReference", required: true, out string? reference)
                || !QueryText(request, "storeName", required: true, out string? store) || store != runtime.ProviderBinding
                || !QueryText(request, "credentialReference", required: true, out string? credential) || credential != runtime.HmacCredentialReference
                || !QueryText(request, "version", required: false, out string? version)) { return Results.StatusCode(503); }
            DaprPlatformHmacInventory? proof = await authority.ResolveAsync(reference!, new(tenant!, parsed), version, token);
            return proof is null ? Results.StatusCode(503) : Results.Ok(new
            {
                authorityReference = proof.AuthorityReference, revision = proof.Revision, tenantId = proof.Scope.TenantId,
                purpose = proof.Scope.Purpose.ToString(), version = proof.Metadata.Version, keyState = proof.Metadata.State.ToString(),
                notBefore = proof.Metadata.NotBefore, verifyUntil = proof.Metadata.VerifyUntil, retiredAt = proof.Metadata.RetiredAt,
                storeName = proof.StoreName, credentialReference = proof.CredentialReference, secretReference = proof.SecretReference,
                secretVersion = proof.SecretVersion, secretField = proof.SecretField, observedAt = proof.ObservedAt, validUntil = proof.ValidUntil,
            });
        }).RequireAuthorization("enrolled-inventory-caller");
    }

    private static bool QueryText(JsonObject request, string name, bool required, out string? text)
    {
        text = null;
        JsonNode? node = request[name];
        if (node is null) { return !required; }
        return node is JsonValue value && value.TryGetValue(out text);
    }
}
