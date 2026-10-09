namespace Hexalith.Platform.Custody;

/// <summary>Separately admitted exact-original aged recovery, without renewing the observation or extending automatic H. The shape alone grants no permission.</summary>
/// <param name="Target">Exact current independently qualified installation/routing credential.</param><param name="Original">Independently retained immutable original first-seen/day/sequence/intent, with no acknowledgement.</param>
/// <param name="OriginalProof">Independent authoritative original NotRecorded or exact Recorded proof at admission; Unknown cannot authorize recovery.</param>
/// <param name="RecoveryAuthorityRevision">Independent current restricted recovery permission, distinct from automatic/human/Workflow credentials.</param>
/// <param name="ObservedAt">Fresh independent permission observation.</param><param name="ValidUntil">Exclusive original recovery permission lifetime, never the observation's renewed time.</param>
public sealed record SecurityObservationRecoveryGrant(ReplicatedSecuritySpoolTarget Target, SecurityObservationRecord Original,
    SecurityEventRecorderLookup OriginalProof, string RecoveryAuthorityRevision, DateTimeOffset ObservedAt, DateTimeOffset ValidUntil);
