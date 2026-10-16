using System.Collections.ObjectModel;
using System.Diagnostics.Metrics;

using Hexalith.EventStore.DomainService;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hexalith.Platform.Works.Runtime;

/// <summary>Exports SDK recovery health using its canonical meter and fixed cardinality alerts.</summary>
public sealed class WorksRecoveryTelemetryPublisher : IHealthCheckPublisher
{
    private static readonly string[] _countKeys = ["unresolved", "pendingDispositions", "pendingPublications", "pendingTransitions", "pendingObservations", "retainedCandidates", "complete", "capacityExceeded", "stateAvailable"];
    private static readonly string[] _reasonKeys = ["candidate-offset-invalid", "candidate-page-missing", "candidate-page-unreadable", "candidate-tail-unlinked", "candidate-tail-unreadable", "capacity-exceeded", "catalog-unavailable", "hosted-pass-failed", "read-worker-exhausted", "reconciler-stopped", "reconciliation-disabled", "source-incomplete", "source-state-unavailable", "sweep-record-budget", "tenant-offset-invalid", "tenant-page-missing", "tenant-page-unreadable", "tenant-tail-unlinked", "tenant-tail-unreadable", "other"];
    private readonly TimeProvider _time;
    private WorksRecoveryTelemetrySnapshot _snapshot;

    /// <summary>Creates instruments on the SDK's already enrolled canonical domain meter.</summary>
    /// <param name="diagnostics">Existing keyed SDK diagnostics for work.</param>
    /// <param name="time">Time source used to refuse stale samples.</param>
    public WorksRecoveryTelemetryPublisher(EventStoreDomainDiagnostics diagnostics, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        if (diagnostics.Domain != "work") throw new ArgumentException("Works recovery telemetry requires the canonical work meter.", nameof(diagnostics));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _snapshot = Unavailable(DateTimeOffset.MinValue);
        foreach (string key in _countKeys)
            _ = diagnostics.Meter.CreateObservableGauge("eventstore.domain.recovery." + key, () => GetSnapshot().Counts.GetValueOrDefault(key), description: "Cached SDK process recovery health value.");
        _ = diagnostics.Meter.CreateObservableGauge("eventstore.domain.recovery.ready", () => GetSnapshot().Status == HealthStatus.Healthy ? 1L : 0L);
        _ = diagnostics.Meter.CreateObservableGauge("eventstore.domain.recovery.alert", () => GetSnapshot().Status == HealthStatus.Healthy ? 0L : 1L, description: "Alert when SDK recovery is degraded, unavailable, or stale.");
        _ = diagnostics.Meter.CreateObservableGauge("eventstore.domain.recovery.reason", ObserveReasons);
    }

    /// <summary>Gets immutable cached health; a missing sample or 15 seconds of silence alerts.</summary>
    /// <returns>A bounded snapshot containing no tenant, item, reason payload, or credential labels.</returns>
    public WorksRecoveryTelemetrySnapshot GetSnapshot()
    {
        WorksRecoveryTelemetrySnapshot snapshot = Volatile.Read(ref _snapshot);
        return snapshot.ObservedAt > _time.GetUtcNow() || _time.GetUtcNow() - snapshot.ObservedAt > TimeSpan.FromSeconds(15)
            ? Unavailable(snapshot.ObservedAt) : snapshot;
    }

    /// <inheritdoc/>
    public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        cancellationToken.ThrowIfCancellationRequested();
        DateTimeOffset now = _time.GetUtcNow();
        if (!report.Entries.TryGetValue("eventstore-processes-unresolved", out HealthReportEntry entry))
        {
            Volatile.Write(ref _snapshot, Unavailable(now));
            return Task.CompletedTask;
        }
        Dictionary<string, long> counts = _countKeys.ToDictionary(key => key, key => Number(entry.Data.GetValueOrDefault(key)), StringComparer.Ordinal);
        counts["stateAvailable"] = entry.Data.GetValueOrDefault("stateAvailable") is false ? 0 : entry.Data.ContainsKey("complete") ? 1 : 0;
        Dictionary<string, long> reasons = _reasonKeys.ToDictionary(key => key, _ => 0L, StringComparer.Ordinal);
        if (entry.Data.GetValueOrDefault("reasonCounts") is IReadOnlyDictionary<string, int> supplied)
        {
            foreach ((string key, int value) in supplied)
            {
                string bounded = reasons.ContainsKey(key) ? key : "other";
                reasons[bounded] = Math.Min(int.MaxValue, reasons[bounded] + Math.Max(0, value));
            }
        }
        HealthStatus status = counts["stateAvailable"] == 0 ? HealthStatus.Unhealthy : entry.Status;
        if (status == HealthStatus.Healthy && (counts["complete"] != 1 || counts["unresolved"] > 0 || counts["capacityExceeded"] > 0
            || counts["pendingDispositions"] > 0 || counts["pendingPublications"] > 0 || counts["pendingTransitions"] > 0 || counts["pendingObservations"] > 0))
            status = HealthStatus.Degraded;
        Volatile.Write(ref _snapshot, new WorksRecoveryTelemetrySnapshot(now, status,
            new ReadOnlyDictionary<string, long>(counts), new ReadOnlyDictionary<string, long>(reasons)));
        return Task.CompletedTask;
    }

    private static long Number(object? value) => value switch { true => 1, false => 0, int number => Math.Max(0, number), long number => Math.Max(0, number), _ => 0 };

    private static WorksRecoveryTelemetrySnapshot Unavailable(DateTimeOffset at)
        => new(at, HealthStatus.Unhealthy,
            new ReadOnlyDictionary<string, long>(_countKeys.ToDictionary(key => key, _ => 0L, StringComparer.Ordinal)),
            new ReadOnlyDictionary<string, long>(_reasonKeys.ToDictionary(key => key, _ => 0L, StringComparer.Ordinal)));

    private IEnumerable<Measurement<long>> ObserveReasons()
        => GetSnapshot().Reasons.Select(pair => new Measurement<long>(pair.Value, new KeyValuePair<string, object?>("reason", pair.Key)));
}
