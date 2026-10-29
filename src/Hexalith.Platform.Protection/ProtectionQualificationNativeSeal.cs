using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Protection;

/// <summary>Actual field-engine source write and its independently readable native locations.</summary>
/// <param name="Enrollment">Independent source original.</param><param name="Source">Original native sealed event/outbox cell.</param>
public sealed record ProtectionQualificationNativeSeal(ErasureRootOccurrenceEnrollment Enrollment, ProtectionQualificationNativeCell Source);
