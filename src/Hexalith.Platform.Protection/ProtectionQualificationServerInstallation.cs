using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Protection;

/// <summary>Reference-only native role server installation. Every path is explicit and pinned; no authority genesis, fixture recipes, credentials or signer is created.</summary>
/// <param name="HostInstallationFile">Pinned actual ERC production enrollment.</param><param name="HostInstallationSha256">Exact public enrollment bytes.</param>
/// <param name="ScopeFile">Complete native role recipes and independent source projections.</param><param name="ScopeSha256">Exact scope bytes.</param>
/// <param name="SignerFile">Existing private role P-256 key.</param><param name="PublicAnchorFile">Existing public role verifier.</param>
/// <param name="PublicAnchorSha256">Exact verifier bytes.</param><param name="CredentialReference">Current private role bearer reference.</param>
/// <param name="SourceComponents">Explicit isolated writable non-ERC fixture components.</param><param name="ReadComponents">Explicit independent native observation components.</param>
/// <param name="FaultControl">Independently enrolled native control cell.</param><param name="FaultEnrollmentId">Exact control enrollment.</param>
/// <param name="FaultOperations">Exact operations that may be interrupted.</param><param name="RevocationSubscription">Exact independently enrolled native revocation source.</param>
public sealed record ProtectionQualificationServerInstallation(string HostInstallationFile, string HostInstallationSha256, string ScopeFile, string ScopeSha256,
    string SignerFile, string PublicAnchorFile, string PublicAnchorSha256, string CredentialReference,
    string[] SourceComponents, string[] ReadComponents, ProtectionQualificationNativeCell FaultControl, string FaultEnrollmentId,
    string[] FaultOperations, DeletionCapabilityRevocationSubscription RevocationSubscription)
{
    /// <summary>Environment reference to the independently installed exact workload instance identity. Mandatory for observer permission attestation.</summary>
    public string? WorkloadIdentityReference { get; init; }
}
