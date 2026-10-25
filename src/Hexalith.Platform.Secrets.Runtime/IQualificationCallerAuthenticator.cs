using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

using Microsoft.AspNetCore.Http;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Installed workload authentication and current caller-scope authority; channel authentication alone is insufficient.</summary>
public interface IQualificationCallerAuthenticator
{
    /// <summary>Validates workload credentials and returns their currently authorized isolated scope.</summary>
    Task<QualificationCallerContext?> AuthenticateAsync(HttpContext context, string role, CancellationToken cancellationToken);
}
