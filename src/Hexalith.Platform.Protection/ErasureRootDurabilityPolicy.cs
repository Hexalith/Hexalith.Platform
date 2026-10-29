namespace Hexalith.Platform.Protection;

/// <summary>Approved erasure-root copy policy. Additional policies require a separate implementation and approval.</summary>
public enum ErasureRootDurabilityPolicy
{
    /// <summary>A1: one dedicated, snapshot-excluded custody installation; loss of its volume loses every protected root.</summary>
    None = 0,
}
