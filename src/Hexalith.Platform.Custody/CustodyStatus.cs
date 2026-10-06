namespace Hexalith.Platform.Custody;

/// <summary>Content-free failure classes; these do not expose provider diagnostics.</summary>
public enum CustodyStatus
{
    /// <summary>The requested operation succeeded.</summary>
    Succeeded,
    /// <summary>The exact scope or operation is not allowed.</summary>
    Denied,
    /// <summary>Custody could not establish current authoritative state.</summary>
    Unavailable,
    /// <summary>The supplied key version is unknown.</summary>
    UnknownKey,
    /// <summary>The key is emergency-revoked.</summary>
    RevokedKey,
    /// <summary>Policy, key inventory or input is malformed or missing.</summary>
    Invalid,
    /// <summary>The configured profile is not currently valid.</summary>
    StaleProfile,
    /// <summary>The delivery is outside its exclusive validity bound.</summary>
    Expired,
    /// <summary>The delivery was issued beyond the configured skew.</summary>
    FutureIssued,
    /// <summary>The key is outside its retained verification window.</summary>
    OutsideKeyWindow,
    /// <summary>Cryptographic authentication failed.</summary>
    InvalidTag,
    /// <summary>The expected principal, operation or target differs.</summary>
    ScopeMismatch,
}
