namespace Hexalith.Platform.Custody;

/// <summary>Closed exact physical-key lifecycle operations.</summary>
public enum CustodyKeyLifecycleAction
{
    /// <summary>Retain exact lifecycle-covered key/copies under an approved hold.</summary>
    Pin = 1,
    /// <summary>Remove only the exact hold with independently approved current release/control.</summary>
    Unpin = 2,
    /// <summary>Irreversibly destroy only after current linearizable fence/store reservation and, for roots, exact protection-owner reservation.</summary>
    Destroy = 3,
}
