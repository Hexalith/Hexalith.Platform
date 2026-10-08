using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Custody;

/// <summary>Independent authenticated issuer/profile verification, separate from custody's compromised capability signing key.</summary>
/// <remarks>Verify signature and its digest, issuer, audience, tenant, family/version, event, revocation and trust revisions.
/// A provider must not derive trust from the envelope or authenticate an emergency revocation using the compromised key.</remarks>
public interface IDeletionCapabilityRevocationAuthenticator
{
    /// <summary>Authenticates the exact revocation and signed transport evidence under current independent trust; absent authority returns null.</summary>
    Task<DeletionCapabilityRevocationAuthorization?> VerifyAsync(DeletionCapabilityRevocationEnvelope envelope,
        string signedEvidence, CancellationToken cancellationToken = default);
}
