namespace Hexalith.Platform.Custody;

/// <summary>Safe exact lifecycle outcomes; absence never means destruction.</summary>
public enum CustodyKeyLifecycleStatus
{
    /// <summary>Missing current qualified owner/control authority.</summary>
    Unavailable = 0,
    /// <summary>Original physical result unresolved; no new operation on this key may begin.</summary>
    Unknown = 1,
    /// <summary>Exact immutable wrap/store outcome is retained.</summary>
    Wrapped = 2,
    /// <summary>Original hold and copies are durably pinned.</summary>
    Pinned = 3,
    /// <summary>Exact approved hold is durably released.</summary>
    Unpinned = 4,
    /// <summary>Authenticated irreversible all-copy/nonrollback destruction is retained.</summary>
    Destroyed = 5,
    /// <summary>Independent exact proof that the original physical operation performed no effect.</summary>
    NotPerformed = 6,
    /// <summary>Immutable object/operation conflict.</summary>
    Conflict = 7,
    /// <summary>Current or unresolved hold prevents destruction.</summary>
    BlockedByPin = 8,
}
