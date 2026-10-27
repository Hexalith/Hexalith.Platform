namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>A durably committed result and frozen index basis, materialized idempotently from its original completion ticket.</summary>
public sealed record IndependentOriginalResultCommit(string Role,string RequestId,string ResultSha256,
    string OriginalSha256,string? IndexRoot,long Revision);
