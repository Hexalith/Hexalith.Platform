namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Bounded durable cursor over the pinned immutable index and legacy head. Originals cannot change until conservative liability migration completes.</summary>
public sealed record OriginalCompletionLiabilityMigration(string? IndexRoot,IReadOnlyList<string> Remaining,long Processed,long PendingBytes,string? LegacyAfter=null,bool IndexStarted=false);
