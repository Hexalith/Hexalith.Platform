namespace Hexalith.Platform.Works.Runtime;

/// <summary>A bounded append-only per-source qualification audit; a full document refuses further mutation.</summary>
/// <param name="Attempts">Persisted attributed attempts.</param>
public sealed record WorksProcessDispositionAuditDocument(IReadOnlyList<WorksProcessDispositionAuditRecord> Attempts);
