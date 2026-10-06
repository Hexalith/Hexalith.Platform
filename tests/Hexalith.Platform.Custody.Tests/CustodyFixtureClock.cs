namespace Hexalith.Platform.Custody.Tests;

/// <summary>Deterministic local fixture clock.</summary>
public sealed class CustodyFixtureClock : TimeProvider
{
    /// <summary>Gets or sets fixture time.</summary>
    public DateTimeOffset Now { get; set; } = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => Now;
}
