namespace Hexalith.Platform.Composition;

using Microsoft.Extensions.Hosting;

/// <summary>Issues the operator relay's upstream credentials. The task's per-run relay credential is never one of them.</summary>
internal interface IPlatformOperatorCredentials
{
    /// <summary>Gets the operator principal named in support-safe failures.</summary>
    string OperatorName { get; }

    /// <summary>Returns a short-lived operator bearer carrying exactly the requested grants.</summary>
    Task<string> OperatorTokenAsync(IReadOnlyList<PlatformOperatorGrant> grants, CancellationToken cancellationToken);

    /// <summary>Returns the Tenants workload assertion for one bound command submission, or null when none can be issued.</summary>
    Task<string?> WorkloadAssertionAsync(IHostEnvironment environment, string audience, string domain, CancellationToken cancellationToken);
}
