using Hexalith.EventStore.Contracts.Security;
namespace Hexalith.Platform.Custody;

/// <summary>Independent target-limited recorded guard signing authorization; never human approval, recorder or custodian self-authorization.</summary>
public interface IDeletionCapabilitySigningAuthority : IAnchoredStateTransitionAuthority
{
    /// <summary>Authenticates current private caller/credential for the exact request and named SignDeletionCapability or LookupDeletionCapability; historical artifact reads do not mint issuance.</summary>
    Task<bool> AuthorizeOperationAsync(DeletionBatchCapabilityV1 payload, string signingRequestId, string operation, CancellationToken cancellationToken = default);
    /// <summary>Authenticates exact tenant/batch/seal/manifest/attestation/attempt/intended guard revision/current per-tenant healthy key and audience.</summary>
    Task<bool> AuthorizeAsync(DeletionBatchCapabilityV1 payload, string signingRequestId, CancellationToken cancellationToken = default);
    /// <summary>Authenticates the independently installed exact original operation owner and captured safe outcome digest, including initial absence. Restored missing, unknown or divergent terminal state denies lookup and effects; private caller credentials do not prove durable history.</summary>
    Task<bool> ValidateStateAsync(DeletionBatchCapabilityV1 identity, string signingRequestId, string exactStateDigest, CancellationToken cancellationToken = default);
    /// <summary>Deprecated compatibility-only legacy anchor hook; current recoverable actors do not invoke it.
    /// Qualified implementations must implement the mandatory inherited IAnchoredStateTransitionAuthority admitted-original admission/recovery
    /// and conditional exact transition journal, including independent staging ownership, current permission and final durable-state/anchor confirmation.
    /// Implementing this legacy hook alone never enables an actor; omitted inherited proof defaults deny.</summary>
    Task<bool> RecordStateAsync(DeletionBatchCapabilityV1 identity, string signingRequestId, string expectedStateDigest, string nextStateDigest, CancellationToken cancellationToken = default);

}
