using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hexalith.Platform.Custody;

/// <summary>Runs a new full canary on every readiness request; previous success never bypasses this evaluation.</summary>
/// <param name="gate">Explicitly qualified production gate.</param>
public sealed class Fr34ProtectionReadinessCheck(Fr34ProtectionGate gate) : IHealthCheck
{
    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        => await gate.EvaluateAsync(cancellationToken).ConfigureAwait(false) ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("DependencyNotAvailable: EXT-PROTECTION-1 FR-34.");
}
