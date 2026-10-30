namespace Hexalith.Platform.Protection;

/// <summary>Exact transition metadata over structurally shared raw target bytes; the complete original digest authenticates reconstruction.</summary>
internal sealed record ErasureRootTransitionPayloadArchive(string Format, string ScopeId, long ExpectedRevision, long TargetRevision,
    string PredecessorDigest, string TargetDigest, string TargetArchive);
