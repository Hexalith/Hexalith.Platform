namespace Hexalith.Platform.Custody;


/// <summary>Resolves an authorized, fresh exact-purpose key; implementations never cache revocation state.</summary>
public interface IPlatformHmacKeyProvider
{
    /// <summary>Resolves the current version when version is null, or an exact retained version otherwise.</summary>
    ValueTask<PlatformHmacKeyResolution> ResolveAsync(PlatformHmacScope scope, string? version = null,
        CancellationToken cancellationToken = default);
}
