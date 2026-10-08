using Hexalith.EventStore.Contracts.Security;
using Dapr.Actors;

namespace Hexalith.Platform.Custody;

/// <summary>Private deterministic durable signing-request/outcome owner, unavailable until host/backend/current authority qualification.</summary>
public interface IDeletionCapabilitySigningActor : IActor
{
    /// <summary>Persists original intent before signing; retries return/lookup that result and never create another logical request.</summary>
    Task<DeletionCapabilitySigningOutcome> SignAsync(DeletionBatchCapabilityV1 payload);
    /// <summary>Authenticates exact original signer outcome after loss without new signing.</summary>
    Task<DeletionCapabilitySigningOutcome> LookupAsync(DeletionBatchCapabilityV1 payload);
    /// <summary>Terminalizes only an exact resolved Signed artifact with independently authenticated irreversible guard no-issue proof; never signs a successor.</summary>
    Task<DeletionCapabilitySigningOutcome> ObsoleteUnissuedAsync(DeletionBatchCapabilityV1 payload);
}
