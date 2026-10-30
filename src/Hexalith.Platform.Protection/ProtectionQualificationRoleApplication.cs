using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Hexalith.Platform.Secrets.Runtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hexalith.Platform.Protection;

/// <summary>Owned bounded HTTPS role endpoint for the shipped executable transport. It dispatches only actual native service operations under one explicit private role credential.</summary>
public static class ProtectionQualificationRoleApplication
{
    /// <summary>Runs an already composed native role. Missing signer, installed scope, private credentials or native enrollment cannot create a server.</summary>
    public static async Task RunAsync(ProtectionQualificationRoleService service, string credentialReference, CancellationToken token = default, IReadOnlyList<IHostedService>? workers = null)
    {
        await using var app = Build(service, credentialReference, workers);
        await app.RunAsync(token).ConfigureAwait(false);
    }
    /// <summary>Builds the actual role route. Credential resolution remains current for every request; redirects, alternate roles and unbounded frames are unavailable.</summary>
    public static WebApplication Build(ProtectionQualificationRoleService service, string credentialReference, IReadOnlyList<IHostedService>? workers = null)
    {
        ArgumentNullException.ThrowIfNull(service);
        if (!System.Text.RegularExpressions.Regex.IsMatch(credentialReference, "^env:[A-Z][A-Z0-9_]*$")
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(credentialReference[4..])))
        { throw new InvalidOperationException("Explicit private role credential required."); }
        var builder = WebApplication.CreateSlimBuilder(); builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 16 * 1024 * 1024);
        foreach (var worker in workers ?? []) { Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<IHostedService>(builder.Services, worker); }
        var app = builder.Build();
        app.MapPost("/platform-protection/v1/role", async (HttpContext context, CancellationToken token) =>
        {
            string? current = Environment.GetEnvironmentVariable(credentialReference[4..]);
            string supplied = context.Request.Headers.Authorization.ToString();
            if (string.IsNullOrWhiteSpace(current) || supplied.Length > 8192 || !supplied.StartsWith("Bearer ", StringComparison.Ordinal)
                || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(supplied[7..])), SHA256.HashData(Encoding.UTF8.GetBytes(current)))
                || context.Request.Headers["X-Hexalith-Protection-Role-Contract"] != "ext-protection-1-role-program/v1")
            { return Results.Unauthorized(); }
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(token); budget.CancelAfter(TimeSpan.FromSeconds(40));
            try
            {
                using var bytes = new MemoryStream(); byte[] buffer = new byte[8192]; int count;
                while ((count = await context.Request.Body.ReadAsync(buffer, budget.Token).ConfigureAwait(false)) > 0)
                { if (bytes.Length + count > 16 * 1024 * 1024) { return Results.StatusCode(413); } bytes.Write(buffer, 0, count); }
                CustodyBindingContract.RejectDuplicateProperties(bytes.ToArray());
                var request = JsonNode.Parse(bytes.ToArray()) as JsonObject ?? throw new InvalidOperationException();
                return Results.Bytes(ProtectionQualificationCanonical.Bytes(await service.HandleAsync(request, budget.Token).ConfigureAwait(false)), "application/json");
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception) { return Results.Json(new { code = "DependencyNotAvailable", record = "EXT-PROTECTION-1" }, statusCode: 503); }
        });
        return app;
    }
}
