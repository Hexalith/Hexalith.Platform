namespace Hexalith.Platform.Custody;

/// <summary>Default denies until the host installs an approved current profile.</summary>
public sealed class UnavailablePlatformSigningProfileProvider : IPlatformSigningProfileProvider
{
    /// <inheritdoc/>
    public PlatformSigningProfile? GetCurrent() => null;
}
