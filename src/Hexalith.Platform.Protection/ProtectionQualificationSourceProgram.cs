namespace Hexalith.Platform.Protection;

/// <summary>Source-owned ordered native program; installation supplies values for its closed operations.</summary>
/// <param name="Before">Preparatory native calls.</param><param name="Effect">Primary native effect.</param>
/// <param name="Race">Competing native effect.</param><param name="After">Follow-up native calls.</param>
internal sealed record ProtectionQualificationSourceProgram(string[] Before, string Effect, string? Race = null, string[]? After = null);
