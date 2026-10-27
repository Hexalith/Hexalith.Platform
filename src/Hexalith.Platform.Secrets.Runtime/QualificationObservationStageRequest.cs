namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Versioned requested observation boundary. The canonical case identity is unchanged.</summary>
public sealed record QualificationObservationStageRequest(string Version,string CaseRequestSha256,string Stage,string PredecessorSha256,string? ControlOperationId=null,string? ControlOriginalSha256=null);
