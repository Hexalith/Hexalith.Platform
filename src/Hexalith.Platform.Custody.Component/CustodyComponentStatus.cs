namespace Hexalith.Platform.Custody.Component;

/// <summary>Disposable component observation. Absence is not destruction.</summary>
internal enum CustodyComponentStatus
{
    /// <summary>Witness, binding, or live epoch is missing. Nothing was written.</summary>
    Unavailable = 0,
    /// <summary>Admission or witness outage. Not an authoritative absence.</summary>
    Unknown = 1,
    /// <summary>Exact hold or manifest does not match the retained original.</summary>
    Conflict = 2,
    /// <summary>An active pin blocked destruction. No copy was destroyed.</summary>
    BlockedByPin = 3,
    /// <summary>Exact hold is retained.</summary>
    Pinned = 4,
    /// <summary>Exact hold is released.</summary>
    Unpinned = 5,
    /// <summary>Every targeted copy was destroyed and the outside witness retained the barrier.</summary>
    Destroyed = 6,
    /// <summary>Authoritative witness absence of this exact original. Not an outage.</summary>
    NotStarted = 7,
}
