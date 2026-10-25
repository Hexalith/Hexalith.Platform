using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Independently enrolled finite parent recipe. Every native registration, key-use admission, digest reference and key reference is an actual supplied original; no native identity is renamed.</summary>
public sealed record Fr34OccurrenceRecipe(string RecipeId,Fr34ProtectionTarget Target,string PayloadTypeId,string ProtectionProfileVersion,
    string DigestVersion,ulong LifecycleEpoch,IReadOnlyList<Fr34OccurrenceSlot> Slots,
    IReadOnlyDictionary<string,Hexalith.EventStore.PayloadProtection.InteractionProtectionBinding>? ExactOccurrences=null);
