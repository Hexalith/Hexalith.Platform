namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>One independently reserved exact spool publication and its completion; survives archive and head acknowledgment loss.</summary>
public sealed record IndependentSpoolPublication(string TransitionSha256,string TargetDigest,long TargetRevision,long ReservedBytes);
