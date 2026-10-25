using Hexalith.EventStore.PayloadProtection;

namespace Hexalith.Platform.Protection;

/// <summary>Immutable independent claim binds a fresh canary owner to an unchanged original native protection target.</summary>
public sealed record Fr34OccurrenceClaim(string RecipeId,string SlotId,string CanaryId,InteractionProtectionBinding Binding,
    CustodyInteractionMaterialProfile Write,IReadOnlyList<CustodyInteractionMaterialProfile> Reads);
