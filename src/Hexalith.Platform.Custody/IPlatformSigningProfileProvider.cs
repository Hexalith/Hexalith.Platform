namespace Hexalith.Platform.Custody;

/// <summary>Provides the current immutable owner-approved profile, never an inferred default.</summary>
public interface IPlatformSigningProfileProvider
{
    /// <summary>Reads the current profile revision; null means unavailable.</summary>
    PlatformSigningProfile? GetCurrent();
}
