namespace Hexalith.Platform.Protection;

/// <summary>Independently authenticated final retirement of the exact A1 volume/enrollment under the approved full-copy policy. Missing head/transport is never this positive original.</summary>
/// <param name="StoreId">Original physical custody identity.</param><param name="EnrollmentId">Original immutable ERC enrollment.</param><param name="TenantId">Exact tenant whose roots were irreversibly lost.</param>
/// <param name="RetiredVolumeIdentity">Exact independently retired original volume identity.</param><param name="ReceiptId">Immutable independently retained final-loss original.</param>
/// <param name="CopyPolicySha256">Independently accepted full-copy/no-recovery policy governing the retired volume.</param>
public sealed record ErasureRootPermanentLossReceipt(string StoreId, string EnrollmentId, string TenantId, string RetiredVolumeIdentity, string ReceiptId, string CopyPolicySha256);
