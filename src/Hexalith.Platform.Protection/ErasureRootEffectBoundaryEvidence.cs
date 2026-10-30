namespace Hexalith.Platform.Protection;

/// <summary>Independent permission owner's exact content-free native/custody boundary original; executor strings confer no proof.</summary>
public sealed record ErasureRootEffectBoundaryEvidence(string TenantId, string StoreId, string EnrollmentId, string CallDigest, string Boundary,
    string CustodyStateSha256, ErasureRootNativePolicy NativePolicy, string Fingerprint, string ReceiptId);
