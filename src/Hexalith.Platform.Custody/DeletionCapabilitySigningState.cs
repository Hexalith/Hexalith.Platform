namespace Hexalith.Platform.Custody;

/// <summary>Closed content-free signer outcomes. Signed is an artifact, never proof of guard issue/dispatch or consumption authority.</summary>
public enum DeletionCapabilitySigningState
{
    /// <summary>Exact signature is durably retained.</summary>
    Signed,
    /// <summary>A durable request exists, but no exact signer result is known.</summary>
    Unknown,
    /// <summary>Independently retained known noninvoked original: current signing authorization or post-intent trust denied.</summary>
    Denied,
    /// <summary>Required authority/backend is unavailable.</summary>
    Unavailable,
    /// <summary>Immutable signer identity/result differs.</summary>
    Conflict,
    /// <summary>Original public signature is retained, but the guard durably proved this exact attempt can never issue.</summary>
    SignedAttestationObsoleteUnissued,
}
