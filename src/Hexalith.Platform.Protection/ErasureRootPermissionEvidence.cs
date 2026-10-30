namespace Hexalith.Platform.Protection;

/// <summary>Exact-current independently signed permission body; the transport separately binds nonce, principal, trust revision and request bytes.</summary>
/// <param name="TenantId">Exact tenant.</param><param name="Identity">Exact original.</param><param name="Operation">Exact phase.</param>
/// <param name="RequestDigest">Complete request digest.</param><param name="ReceiptId">Actual independent authority receipt.</param><param name="Permitted">Current decision.</param>
public sealed record ErasureRootPermissionEvidence(string TenantId, string Identity, string Operation, string RequestDigest, string ReceiptId, bool Permitted);
