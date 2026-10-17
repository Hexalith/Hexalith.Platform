using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Inventory.Tests;

internal sealed class InventoryTestProfile(PlatformSigningProfile? profile) : IHmacCurrentTrust
{
    public bool ChangeRevision { get; init; }
    public TimeProvider Clock { get; init; } = TimeProvider.System;
    public DateTimeOffset? Expires { get; init; }
    private int _reads;

    public Task<CurrentHmacTrust?> ReadAsync(PlatformHmacScope? scope, string? version, CancellationToken cancellationToken)
    {
        _reads++;
        DateTimeOffset now = Clock.GetUtcNow();
        string revision = ChangeRevision && _reads > 2 ? "changed-current-revision" : "offline-independent-current-revision";
        return Task.FromResult(profile is null ? null : new CurrentHmacTrust(profile, revision, now.AddSeconds(-1), Expires ?? now.AddSeconds(20)));
    }
}
