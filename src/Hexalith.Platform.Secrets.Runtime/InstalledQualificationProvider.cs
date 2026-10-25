using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Actual role owner calls with separate current authorization and signed public results; no expected result is generated here.</summary>
public abstract class InstalledQualificationProvider(IndependentDaprAuthority scopes, IndependentDaprAuthority owner, string role) : IQualificationRoleProvider
{
    /// <inheritdoc />
    public string Role => role;
    /// <inheritdoc />
    public async Task<bool> AuthorizeAsync(JsonObject request, QualificationCallerContext caller, CancellationToken cancellationToken)
        => await scopes.InvokeAsync<Permission>("qualification-authorize", new { role, request, caller }, cancellationToken).ConfigureAwait(false) is { Allowed: true };
    /// <inheritdoc />
    public Task<JsonObject?> InvokeAsync(string operation, JsonObject original, CancellationToken cancellationToken)
        => operation is "execute" or "lookup" or "observe" or "enroll"
            ? owner.InvokeAsync<JsonObject>("qualification-" + role + "-" + operation, original, cancellationToken) : Task.FromResult<JsonObject?>(null);
    private sealed record Permission(bool Allowed);
}
