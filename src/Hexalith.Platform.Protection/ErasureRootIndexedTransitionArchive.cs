namespace Hexalith.Platform.Protection;

/// <summary>Small immutable committed transition node with exact shared payload and authenticated logarithmic ancestry links.</summary>
internal sealed record ErasureRootIndexedTransitionArchive(string ScopeId, string? Previous, string OriginalArchive,
    long Revision, string Digest, string PredecessorDigest, string[] Ancestors);
