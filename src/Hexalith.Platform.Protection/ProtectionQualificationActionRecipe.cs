namespace Hexalith.Platform.Protection;

/// <summary>Native sequencing for one named owner-plan action. Optional race branches invoke actual native APIs concurrently.</summary>
/// <param name="Before">Preparatory native operations.</param><param name="Effect">Original native operation.</param>
/// <param name="Race">Optional independently submitted competing native operation.</param><param name="After">Native follow-up operations.</param>
public sealed record ProtectionQualificationActionRecipe(ProtectionQualificationNativeCall[] Before, ProtectionQualificationNativeCall Effect,
    ProtectionQualificationNativeCall? Race, ProtectionQualificationNativeCall[] After);
