using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;

namespace Hexalith.Platform.Protection;

/// <summary>Actual sealed source event/outbox cell written with its exact source proof in one native transaction.</summary>
/// <param name="Binding">Original independent binding.</param><param name="Protected">Actual engine output.</param>
public sealed record ProtectionQualificationPersistedEvent(InteractionProtectionBinding Binding, PayloadProtectionResult Protected);
