using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Protection;

/// <summary>Actual field-engine source write and its independently readable native locations.</summary>
/// <param name="Enrollment">Independent source original.</param><param name="Source">Original native sealed event/outbox cell.</param>
public sealed record ProtectionQualificationNativeSeal(ErasureRootOccurrenceEnrollment Enrollment, ProtectionQualificationNativeCell Source)
{
    /// <summary>Existing sealed source for a content-free restoration action that must advance a distinct writer occurrence before restoring its stale snapshot.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ProtectionQualificationNativeCell? SeedSource { get; init; }
    /// <summary>Closed negative operation: reuse this exact occurrence with a fixed different synthetic payload; no destructive request carries plaintext.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool ChangePayload { get; init; }
}
