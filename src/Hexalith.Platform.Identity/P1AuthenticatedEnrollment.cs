namespace Hexalith.Platform.Identity;

/// <summary>Offline observation of a signed bootstrap under an independently supplied root pin; it is not operational adoption.</summary>
public sealed class P1AuthenticatedEnrollment
{
    internal P1AuthenticatedEnrollment(P1ReceiptEnrollment enrollment, string bootstrapSha256,
        string rootKeyFingerprint, string principalMappingRevision)
    {
        Enrollment = enrollment with
        {
            ReceiptPublicKeySpki = enrollment.ReceiptPublicKeySpki.ToArray(),
            StatusPublicKeySpki = enrollment.StatusPublicKeySpki.ToArray(),
        };
        BootstrapSha256 = bootstrapSha256;
        RootKeyFingerprint = rootKeyFingerprint;
        PrincipalMappingRevision = principalMappingRevision;
    }

    /// <summary>Digest of the exact verified bootstrap payload.</summary>
    public string BootstrapSha256 { get; }

    /// <summary>Fingerprint of the separately pinned root.</summary>
    public string RootKeyFingerprint { get; }

    /// <summary>Revision of the signed credential-to-principal map.</summary>
    public string PrincipalMappingRevision { get; }

    internal P1ReceiptEnrollment Enrollment { get; }
}
