namespace Hexalith.Platform.Custody;

/// <summary>Forward-only actor-history lifecycle state.</summary>
public enum IdentityHistoryLifecycleState
{
    /// <summary>The unit is admitted and not yet expired or queued for destruction.</summary>
    Active,

    /// <summary>The exclusive deadline has passed and destruction has not started.</summary>
    Expired,

    /// <summary>Irreversible destruction has a stable operation identifier.</summary>
    PendingDestruction,

    /// <summary>Destruction completed and the final receipt is not yet linked.</summary>
    Destroyed,

    /// <summary>The original content-free receipt reference is linked.</summary>
    ReceiptFinal,
}
