namespace Hexalith.Platform.Custody;

/// <summary>Default refuses key use; no development key becomes a production fallback.</summary>
public sealed class UnavailablePlatformHmacKeyProvider : IPlatformHmacKeyProvider
{
    /// <inheritdoc/>
    public ValueTask<PlatformHmacKeyResolution> ResolveAsync(PlatformHmacScope scope, string? version = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new PlatformHmacKeyResolution(CustodyStatus.Unavailable));
    }
}
