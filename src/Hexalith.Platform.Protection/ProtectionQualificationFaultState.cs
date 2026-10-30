namespace Hexalith.Platform.Protection;

/// <summary>Independently pre-enrolled isolated native fault-control cell. Missing state cannot be initialized by a running role.</summary>
/// <param name="EnrollmentId">Exact fixture-control enrollment.</param><param name="Armed">Exact operation/boundary originals.</param><param name="Consumed">Original faults already applied once.</param>
public sealed record ProtectionQualificationFaultState(string EnrollmentId, Dictionary<string, string> Armed, Dictionary<string, string> Consumed)
{
    /// <summary>Exact immutable arm-call results retained in the same native control CAS as the mutation.</summary>
    public Dictionary<string, System.Text.Json.Nodes.JsonObject> CallOriginals { get; init; } = new(StringComparer.Ordinal);
}
