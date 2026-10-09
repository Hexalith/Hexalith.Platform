namespace Hexalith.Platform.Custody;

/// <summary>Distinguishes current authoritative absence from unavailable/divergent original lookup; only authoritative absence permits first observation.</summary>
/// <param name="IsAvailable">Exact current source and independent private lookup authority were confirmed.</param><param name="Record">Original immutable safe observation, or null for authoritative absence.</param>
public sealed record SecurityObservationOriginalLookupResult(bool IsAvailable, SecurityObservationRecord? Record = null);
