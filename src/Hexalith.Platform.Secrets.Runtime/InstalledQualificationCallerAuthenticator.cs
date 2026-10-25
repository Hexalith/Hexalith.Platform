using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Workload bearer hash is checked by the independent current scope owner; request content grants nothing.</summary>
public sealed class InstalledQualificationCallerAuthenticator(IndependentDaprAuthority scopes) : IQualificationCallerAuthenticator
{
    /// <inheritdoc />
    public Task<QualificationCallerContext?> AuthenticateAsync(HttpContext context, string role, CancellationToken cancellationToken)
    {
        var values = context.Request.Headers.Authorization;
        if (values.Count != 1 || values[0] is not { Length: > 7 and <= 8199 } value || !value.StartsWith("Bearer ", StringComparison.Ordinal))
            return Task.FromResult<QualificationCallerContext?>(null);
        byte[] bearer = Encoding.UTF8.GetBytes(value[7..]);
        try { return scopes.InvokeAsync<QualificationCallerContext>("qualification-authenticate", new { role, bearerSha256 = Convert.ToHexString(SHA256.HashData(bearer)) }, cancellationToken); }
        finally { CryptographicOperations.ZeroMemory(bearer); }
    }
}
