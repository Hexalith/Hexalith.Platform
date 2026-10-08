namespace Hexalith.Platform.Custody;

/// <summary>Independent published profile/anchor resolution; missing, stale or revoked trust disables new signing/result retention.</summary>
/// <remarks>The deployment must independently authenticate this authority. Neither the signer result nor a custodian's self-published key qualifies it.</remarks>
public interface IDeletionCapabilitySigningTrustProvider
{
    /// <summary>Resolves the current published verifier for the exact tenant, deletion-only purpose and version.</summary>
    Task<DeletionCapabilityPublishedTrust?> ResolveAsync(string tenantId, string keyFamily, string keyVersion, CancellationToken cancellationToken = default);
}
