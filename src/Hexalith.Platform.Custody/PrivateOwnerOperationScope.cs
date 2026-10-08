namespace Hexalith.Platform.Custody;

/// <summary>Private candidate technical-owner operation; never a general command or human/Workflow capability.</summary>
/// <param name="TenantId">Exact owner namespace; replay registration is reserved system.</param>
/// <param name="ResourceId">Exact stream/batch/delivery/registry resource.</param>
/// <param name="Method">Exact private owner method.</param>
/// <param name="Contract">Concrete closed request schema.</param>
/// <param name="PayloadFingerprint">Retained-key HMAC of the exact request, independently computed by the owner adapter.</param>
/// <param name="DigestKeyVersion">Recorded tenant/system digest version.</param>
/// <param name="AuthenticatedTargetTenantId">Authenticated business target, separately bound for system replay/security owners.</param>
public sealed record PrivateOwnerOperationScope(string TenantId, string ResourceId, string Method, string Contract,
    string PayloadFingerprint, string DigestKeyVersion, string AuthenticatedTargetTenantId);
