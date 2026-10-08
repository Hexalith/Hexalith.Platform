namespace Hexalith.Platform.Custody;

/// <summary>Authoritative recorder lookup classification; Unknown never permits blind append or acknowledgement.</summary>
public enum SecurityEventRecorderLookupState
{
    /// <summary>Complete exact source proof confirms no prior record.</summary>
    NotRecorded,
    /// <summary>Exact original record is durably present.</summary>
    Recorded,
    /// <summary>Current exact source/authorization is uncertain.</summary>
    Unknown,
}
