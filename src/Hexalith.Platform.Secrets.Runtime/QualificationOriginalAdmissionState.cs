namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Authenticated exact original admission; missing transport is never proof of absence.</summary>
public enum QualificationOriginalAdmissionState
{
    /// <summary>Unknown/outage/conflicting request; lookup only.</summary>
    Unknown,
    /// <summary>Independently signed definitive absence of this exact native workflow admission.</summary>
    Absent,
    /// <summary>Exact immutable request is admitted; only the workflow's published recovery semantics may continue.</summary>
    Admitted,
}
