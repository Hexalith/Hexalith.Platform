namespace Hexalith.Platform.Custody;

/// <summary>Closed content-free signer outcomes. Signed is an artifact, never proof of guard issue/dispatch or consumption authority.</summary>
public enum DeletionCapabilitySigningState
{
    /// <summary>Exact signature is durably retained.</summary>
    Signed,
    /// <summary>A durable request exists, but no exact signer result is known.</summary>
    Unknown,
    /// <summary>Recorded current signing authorization denied before provider invocation.</summary>
    Denied,
    /// <summary>Required authority/backend is unavailable.</summary>
    Unavailable,
    /// <summary>Immutable signer identity/result differs.</summary>
    Conflict,
}
