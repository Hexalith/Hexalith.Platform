using Microsoft.Extensions.Hosting;

namespace Hexalith.Platform.Custody;

/// <summary>Refuses startup unless a fresh exact production canary succeeds; no default registration enables content.</summary>
/// <param name="gate">Explicitly qualified production gate.</param>
public sealed class Fr34ProtectionStartupGate(Fr34ProtectionGate gate) : IHostedService
{
    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken) => gate.RequireForContentAsync(cancellationToken);
    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
