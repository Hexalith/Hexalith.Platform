namespace Hexalith.Platform.Custody;

/// <summary>Content-free outcome of a single independently inventoried actor-history cleanup attempt.</summary>
public enum IdentityHistoryCleanupOutcome
{
    /// <summary>The exact retention unit is not yet expired; custody was not contacted.</summary>
    NotExpired,

    /// <summary>The supplied scope, policy or custody evidence cannot authorize cleanup.</summary>
    Invalid,

    /// <summary>Custody has not confirmed irreversible destruction and fresh read denial.</summary>
    Pending,

    /// <summary>The trusted provider confirmed destruction and a fresh lifecycle check denied reads.</summary>
    ProviderConfirmedDestroyed,
}
