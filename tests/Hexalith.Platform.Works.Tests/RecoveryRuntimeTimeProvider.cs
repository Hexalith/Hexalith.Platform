namespace Hexalith.Platform.Works.Tests;

/// <summary>Deterministic time for cached gauge freshness tests.</summary>
internal sealed class RecoveryRuntimeTimeProvider : TimeProvider
{
    /// <summary>Gets or sets the controlled current time.</summary>
    internal DateTimeOffset Now { get; set; } = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => Now;
}
