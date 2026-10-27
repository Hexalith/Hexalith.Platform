namespace Hexalith.Platform.Protection;

/// <summary>Exact independently accepted closed phase binding/target/partition. Values never come from a command's expected outcomes.</summary>
public sealed record JointPhaseCommandScope(string BindingDigest,string PlatformCommit,int Phase,IReadOnlyList<string> Cases);
