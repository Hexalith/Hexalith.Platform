namespace Hexalith.Platform.Custody;

/// <summary>Closed HMAC purposes; approval-signing authority is deliberately absent.</summary>
public enum PlatformHmacPurpose
{
    /// <summary>Authenticates a trusted delivery envelope.</summary>
    TrustedEnvelope,
    /// <summary>Fingerprints sensitive tenant content using retained versions.</summary>
    ContentDigest,
    /// <summary>Digests untrusted observations only in the reserved system scope.</summary>
    SecurityObservation,
}
