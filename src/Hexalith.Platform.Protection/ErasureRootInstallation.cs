namespace Hexalith.Platform.Protection;

/// <summary>Reference-only enrollment for an already installed A1 custody. Runtime code never creates tenant genesis or native keys.</summary>
/// <param name="StoreId">Immutable physical custody identity.</param>
/// <param name="EnrollmentId">Independently installed enrollment identity.</param>
/// <param name="TransitMount">Dedicated transit mount.</param>
/// <param name="HeadMount">Dedicated KV v2 mount.</param>
/// <param name="NativeKeys">Exact tenant to native derived transit-key enrollment.</param>
/// <param name="DurabilityPolicy">Accepted A1 copy policy.</param>
public sealed record ErasureRootInstallation(string StoreId, string EnrollmentId, string TransitMount, string HeadMount,
    IReadOnlyDictionary<string, string> NativeKeys, ErasureRootDurabilityPolicy DurabilityPolicy = ErasureRootDurabilityPolicy.None);
