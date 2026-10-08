namespace Hexalith.Platform.Custody;

/// <summary>Independent published current or healthy retained profile/anchor resolution. Fresh signing requires current authority; original exact lost-result recovery permits a retained verifier. Missing trust denies both. A retained revoked public anchor permits only exact independently attested historic issuance lookup; new issuance and consumption remain denied.</summary>
/// <remarks>The deployment must independently authenticate this authority. Neither the signer result nor a custodian's self-published key qualifies it.</remarks>
public interface IDeletionCapabilitySigningTrustProvider
{
    /// <summary>Resolves the independently published verifier and current/retained/revoked status for the exact tenant, deletion-only purpose and version.</summary>
    Task<DeletionCapabilityPublishedTrust?> ResolveAsync(string tenantId, string keyFamily, string keyVersion, CancellationToken cancellationToken = default);
}
