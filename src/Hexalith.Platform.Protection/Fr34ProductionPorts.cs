using Hexalith.EventStore.PayloadProtection;

namespace Hexalith.Platform.Protection;

/// <summary>Concrete actual FR-34 engine and independently anchored binding/key ports. Construction establishes no installation, admission or physical erasure.</summary>
public sealed record Fr34ProductionPorts(InteractionPayloadProtectionService Engine,InteractionProtectionBindingProvider Bindings,
    CustodyInteractionOccurrenceKeyProvider Keys,CustodyInteractionProtectionBindingSource BindingSource);
