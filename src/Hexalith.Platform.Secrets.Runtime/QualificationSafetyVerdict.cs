namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Actual independently read safety verdict and the inventory revision that formed its basis.</summary>
public sealed record QualificationSafetyVerdict(string VerdictId,string TenantId,long BasisInventoryRevision,string Status,string NativeReceiptSha256);
