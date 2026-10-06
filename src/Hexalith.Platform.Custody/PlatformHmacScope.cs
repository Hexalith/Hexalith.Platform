namespace Hexalith.Platform.Custody;

/// <summary>An exact tenant and purpose, never inferred from an opaque key version.</summary>
/// <param name="TenantId">The authoritative tenant; security observations use system.</param>
/// <param name="Purpose">The independent key family.</param>
public sealed record PlatformHmacScope(string TenantId, PlatformHmacPurpose Purpose)
{
    /// <summary>Gets whether this exact scope is structurally permitted.</summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(TenantId) && Enum.IsDefined(Purpose)
        && (Purpose != PlatformHmacPurpose.SecurityObservation || TenantId == "system");
}
