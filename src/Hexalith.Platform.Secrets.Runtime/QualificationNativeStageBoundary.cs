namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Facts measured by the independently installed native audit/control reader, never copied from the requested boundary.</summary>
public sealed record QualificationNativeStageBoundary(string Stage,string SourceIncarnation,string SourceRevision,string? ControlOperationId=null,string? ControlOriginalSha256=null);
