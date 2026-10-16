using Hexalith.EventStore.Client.Processes;

namespace Hexalith.Platform.Works.Runtime;

/// <summary>An attributed synthetic operator attempt, persisted before SDK control-state mutation.</summary>
/// <param name="AttemptId">Opaque random attempt identity, stable across CAS retries.</param>
/// <param name="At">UTC recording time.</param>
/// <param name="Workload">Authenticated operator workload.</param>
/// <param name="Actor">Signed originating actor binding.</param>
/// <param name="Source">Exact canonical source granted by the fixture.</param>
/// <param name="Purpose">Signed bounded operation purpose.</param>
/// <param name="Reason">Operator supplied reason.</param>
public sealed record WorksProcessDispositionAuditRecord(
    string AttemptId, DateTimeOffset At, string Workload, string Actor, ProcessSource Source, string Purpose, string Reason);
