namespace Hexalith.Platform.TestExtension;

/// <summary>The single command payload handled by the Platform test extension.</summary>
/// <param name="Marker">An arbitrary probe marker.</param>
public sealed record PlatformTestProbeCommand(string Marker);
