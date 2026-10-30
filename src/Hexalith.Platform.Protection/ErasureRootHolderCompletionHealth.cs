namespace Hexalith.Platform.Protection;

/// <summary>Content-free health of this process's actual disposed restrictive holder backlog.</summary>
public sealed record ErasureRootHolderCompletionHealth(int OutstandingCount, double OldestPendingSeconds, string FailureClass);
