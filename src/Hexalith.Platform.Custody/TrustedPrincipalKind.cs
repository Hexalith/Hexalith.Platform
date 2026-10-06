namespace Hexalith.Platform.Custody;

/// <summary>The closed human/automation shape; this does not confer domain authority.</summary>
public enum TrustedPrincipalKind
{
    /// <summary>Party-bearing tenant human.</summary>
    User,
    /// <summary>Party-free tenant human administrator.</summary>
    Administrator,
    /// <summary>Party-free system human operator.</summary>
    Platform,
    /// <summary>Closed non-human automation.</summary>
    Workflow,
}
