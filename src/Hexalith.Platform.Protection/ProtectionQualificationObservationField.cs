namespace Hexalith.Platform.Protection;

/// <summary>A projection of actual native facts. There is no literal/expected-value branch.</summary>
/// <param name="Source">Independently enrolled native source id, or $item inside an array map.</param><param name="Pointer">RFC6901 native field path.</param>
/// <param name="Transform">Identity, Count, OwnerStatus or LifecycleStatus.</param><param name="Items">Optional array-element field projections.</param>
public sealed record ProtectionQualificationObservationField(string Source, string Pointer, string Transform = "Identity",
    Dictionary<string, ProtectionQualificationObservationField>? Items = null);
