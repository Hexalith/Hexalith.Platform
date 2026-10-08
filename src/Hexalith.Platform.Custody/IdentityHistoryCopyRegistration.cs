namespace Hexalith.Platform.Custody;

/// <summary>Registration state of one inventoried actor-history copy.</summary>
public enum IdentityHistoryCopyRegistration
{
    /// <summary>The copy obligation is registered and not yet in flight.</summary>
    Registered,

    /// <summary>The copy operation is in flight.</summary>
    InFlight,

    /// <summary>The copy operation completed and has a completion reference.</summary>
    Completed,

    /// <summary>The copy operation was invalidated.</summary>
    Invalidated,
}
