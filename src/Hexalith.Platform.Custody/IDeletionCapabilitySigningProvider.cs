using Hexalith.EventStore.Contracts.Security;
namespace Hexalith.Platform.Custody;

/// <summary>Private qualified per-tenant DeletionBatchCapabilitySigningKey family; exact request/outcome and revocation survive restore.</summary>
/// <remarks>Signing atomically checks current non-revoked exact key purpose/tenant/version and authenticates the published public anchor.
/// Deterministic exact lookup resolves unknown calls; no provider signature or interface itself proves guard issuance.</remarks>
public interface IDeletionCapabilitySigningProvider
{
    /// <summary>Performs first exact signing and retains the immutable artifact for the deterministic request.</summary>
    Task<DeletionCapabilitySigningResult> SignAsync(DeletionBatchCapabilityV1 payload, string signingRequestId, CancellationToken cancellationToken = default);
    /// <summary>Reads exact prior result only; unknown/absent result never permits blind repeat signing.</summary>
    Task<DeletionCapabilitySigningResult> LookupAsync(DeletionBatchCapabilityV1 payload, string signingRequestId, CancellationToken cancellationToken = default);
}
