namespace Hexalith.Platform.Custody;

/// <summary>Independent current qualification of the exact target, separate from the engine's own identity claims.</summary>
/// <param name="Target">Exact committed target.</param><param name="AuthorityRevision">Current nonrollback qualification revision.</param>
/// <param name="ObservedAt">Fresh observation.</param><param name="ValidUntil">Exclusive authority validity.</param>
public sealed record Fr34CanaryAuthorization(Fr34ProtectionTarget Target, string AuthorityRevision, DateTimeOffset ObservedAt, DateTimeOffset ValidUntil);
