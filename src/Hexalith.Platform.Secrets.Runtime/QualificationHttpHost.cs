using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

using Microsoft.AspNetCore.Http;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Authenticated workload identity and independently verified scope, never inferred from request content.</summary>
public sealed record QualificationCallerContext(string Identity, string ScopeReference);

/// <summary>Installed workload authentication and current caller-scope authority; channel authentication alone is insufficient.</summary>
public interface IQualificationCallerAuthenticator
{
    /// <summary>Validates workload credentials and returns their currently authorized isolated scope.</summary>
    Task<QualificationCallerContext?> AuthenticateAsync(HttpContext context, string role, CancellationToken cancellationToken);
}

/// <summary>Shared HTTP admission for the three independent role hosts.</summary>
public static class QualificationHttpHost
{
    /// <summary>Checks the repository APP_API_TOKEN/dapr-api-token convention, then authenticates caller scope before dispatch.</summary>
    public static async Task<QualificationHostResult> HandleAsync(string role, string operation, SecretsRuntimeConfiguration configuration,
        HttpContext context, string? appChannelToken, IQualificationCallerAuthenticator? authenticator,
        IQualificationRoleProvider? provider, IQualificationOriginalStore? store, IQualificationPublicProofVerifier? verifier,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(context);
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(configuration.CallTimeout is { } timeout && timeout > TimeSpan.Zero && timeout <= TimeSpan.FromSeconds(30) ? timeout : TimeSpan.FromSeconds(30));
        if (!ValidChannel(context.Request, appChannelToken)) { return QualificationHost.Deny([$"qualification.{role}.appChannelAuthentication"]); }
        if (authenticator is null) { return QualificationHost.Deny([$"qualification.{role}.authenticatedCallerScope"]); }
        bool executorDispatched = false;
        try
        {
            QualificationCallerContext? caller = await authenticator.AuthenticateAsync(context, role, budget.Token).WaitAsync(budget.Token).ConfigureAwait(false);
            if (caller is null || !SecretsProvisioning.IsReference(caller.Identity) || !SecretsProvisioning.IsReference(caller.ScopeReference))
            { return QualificationHost.Deny([$"qualification.{role}.authenticatedCallerScope"]); }
            JsonObject? original = await QualificationHost.ReadRequestAsync(context.Request.Body, budget.Token).ConfigureAwait(false);
            executorDispatched = role == "executor";
            return await QualificationHost.HandleAsync(role, operation, configuration, original, provider, store, verifier, budget.Token, caller).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return QualificationHost.Deny([executorDispatched ? "ExactOriginalLookupRequired" : $"qualification.{role}.authenticatedCallerScope"], executorDispatched); }
    }

    private static bool ValidChannel(HttpRequest request, string? expected)
    {
        // The sidecar presents its independently provisioned role token. Development has no bypass.
        var header = request.Headers["dapr-api-token"];
        if (string.IsNullOrWhiteSpace(expected) || expected.Length > 8192 || header.Count != 1 || header[0] is not { Length: > 0 and <= 8192 } actual) { return false; }
        byte[] left = Encoding.UTF8.GetBytes(expected);
        byte[] right = Encoding.UTF8.GetBytes(actual);
        try { return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right); }
        finally { CryptographicOperations.ZeroMemory(left); CryptographicOperations.ZeroMemory(right); }
    }
}
