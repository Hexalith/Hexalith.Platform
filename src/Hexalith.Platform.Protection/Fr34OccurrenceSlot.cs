namespace Hexalith.Platform.Protection;

/// <summary>One already installed native root and its bounded independently admitted write/read deliveries, consumed by at most one fresh logical challenge.</summary>
public sealed record Fr34OccurrenceSlot(string SlotId,string KeyReference,CustodyInteractionMaterialProfile Write,IReadOnlyList<CustodyInteractionMaterialProfile> Reads,Fr34NativeManifestBinding? NativeManifest=null);
