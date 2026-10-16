using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hexalith.Platform.Works.Runtime;

/// <summary>A cached, bounded view of SDK recovery health, never a second recovery implementation.</summary>
/// <param name="ObservedAt">UTC sample time.</param>
/// <param name="Status">SDK status, or Unhealthy when missing/stale.</param>
/// <param name="Counts">Fixed gauge names and nonnegative counts.</param>
/// <param name="Reasons">Allowlisted reason counts; unknown labels collapse to other.</param>
public sealed record WorksRecoveryTelemetrySnapshot(
    DateTimeOffset ObservedAt, HealthStatus Status, IReadOnlyDictionary<string, long> Counts, IReadOnlyDictionary<string, long> Reasons);
