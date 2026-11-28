namespace Hexalith.Platform.Composition.Tests;

using Microsoft.Extensions.Hosting;

/// <summary>Credentials whose operator token is never available, as when the generated realm refuses the operator.</summary>
internal sealed class UnavailableOperatorCredentials : IPlatformOperatorCredentials
{
    public string OperatorName => PlatformRealmOperator.ClientId;

    public Task<string> OperatorTokenAsync(IReadOnlyList<PlatformOperatorGrant> grants, CancellationToken cancellationToken) =>
        Task.FromException<string>(new InvalidOperationException($"The generated local realm refused operator client '{PlatformRealmOperator.ClientId}' (HTTP 401)."));

    public Task<string?> WorkloadAssertionAsync(IHostEnvironment environment, string audience, string domain, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);
}
