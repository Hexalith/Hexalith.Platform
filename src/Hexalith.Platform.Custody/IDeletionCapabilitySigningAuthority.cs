using Hexalith.EventStore.Contracts.Security;
namespace Hexalith.Platform.Custody;

/// <summary>Independent target-limited recorded guard signing authorization; never human approval, recorder or custodian self-authorization.</summary>
public interface IDeletionCapabilitySigningAuthority
{
    /// <summary>Authenticates current private caller/credential for the exact request and named SignDeletionCapability or LookupDeletionCapability; historical artifact reads do not mint issuance.</summary>
    Task<bool> AuthorizeOperationAsync(DeletionBatchCapabilityV1 payload, string signingRequestId, string operation, CancellationToken cancellationToken = default);
    /// <summary>Authenticates exact tenant/batch/seal/manifest/attestation/attempt/intended guard revision/current per-tenant healthy key and audience.</summary>
    Task<bool> AuthorizeAsync(DeletionBatchCapabilityV1 payload, string signingRequestId, CancellationToken cancellationToken = default);
}
