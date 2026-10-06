namespace Hexalith.Platform.Custody;


/// <summary>Explicit current issuance and retained verification lifecycle.</summary>
public enum PlatformHmacKeyState
{
    /// <summary>The sole current issuance key.</summary>
    Active,
    /// <summary>A non-issuance key retained for previously recorded digests or overlap verification.</summary>
    Retained,
    /// <summary>Emergency-revoked; no signing, digest computation or verification is permitted.</summary>
    Revoked,
}
