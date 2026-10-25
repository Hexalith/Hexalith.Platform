namespace Hexalith.Platform.Protection;

/// <summary>Exact independently installed source-proof locator/revision. Loading this record never publishes or approves evidence.</summary>
public sealed record QualificationGuardEvidenceEnrollment(string StateKey,long Revision,Dictionary<string,string>? Metadata=null);
