namespace Hexalith.Platform.Custody.Tests;

/// <summary>Local profile inventory; numeric values are fixtures, not approved production settings.</summary>
public sealed class CustodyFixtureProfileProvider(CustodyFixtureClock clock) : IPlatformSigningProfileProvider
{
    /// <summary>Gets or sets the local current profile.</summary>
    public PlatformSigningProfile? Profile { get; set; } = new("fixture-r1", "fixture-issuer", "fixture-audience",
        clock.Now.AddDays(-1), clock.Now.AddDays(1), TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(10),
        TimeSpan.FromMinutes(1), TimeSpan.FromHours(1), TimeSpan.FromHours(1));
    /// <inheritdoc/>
    public PlatformSigningProfile? GetCurrent() => Profile;
}
