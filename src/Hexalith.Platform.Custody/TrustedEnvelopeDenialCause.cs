namespace Hexalith.Platform.Custody;

/// <summary>Local verified denial provenance; status alone does not establish its cause.</summary>
public enum TrustedEnvelopeDenialCause
{
    /// <summary>No independently confirmed replay conflict.</summary>
    None = 0,
    /// <summary>Exact retained first-seen evidence binds a different intent to this issuer and nonce.</summary>
    ReplayConflictV1 = 1,
}
