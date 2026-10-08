namespace Hexalith.Platform.Custody;

/// <summary>Closed custody purpose inventory. Decision approval signing is deliberately absent.</summary>
public enum PlatformKeyPurpose
{
    /// <summary>Provider credential, opaque reference only.</summary>
    ProviderCredential,
    /// <summary>Tenant key-encryption purpose.</summary>
    TenantKek,
    /// <summary>Tenant retained intent digest purpose.</summary>
    DigestKey,
    /// <summary>Exact target-tenant migration/directory purpose.</summary>
    MigrationDirectoryCapabilitySigningKey,
    /// <summary>Reserved-system untrusted-observation digest purpose.</summary>
    SecurityObservationDigestKey,
    /// <summary>Per-export envelope key.</summary>
    ExportEnvelopeKey,
    /// <summary>Export manifest signer/public verifier.</summary>
    ExportManifestSigningKey,
    /// <summary>Scoped authenticated command HMAC.</summary>
    TrustedEnvelopeHmacKey,
    /// <summary>Per-tenant destruction capability purpose.</summary>
    DeletionBatchCapabilitySigningKey,
    /// <summary>Independently issued decision verifier only, never an approval signing key.</summary>
    DecisionVerificationAnchor,
    /// <summary>One independently custodied root per complete interaction target.</summary>
    InteractionRootDek,
}
