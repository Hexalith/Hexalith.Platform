using Hexalith.EventStore.Server.Security;
using Hexalith.Platform.Secrets.Runtime;
using System.Text.Json.Serialization;

namespace Hexalith.Platform.Protection;

/// <summary>Reference-only production host enrollment; loading it never provisions credentials, keys, native genesis or copy-policy evidence.</summary>
/// <param name="Native">Dedicated native ERC identity.</param><param name="Endpoint">Dedicated HTTPS endpoint.</param>
/// <param name="CredentialReference">Environment reference for the native credential.</param><param name="RegistryEpochs">Independent tenant registry epochs.</param>
/// <param name="PrivateAuthority">Independent current private operation authority.</param><param name="SourceAuthority">Independent source/writer authority.</param>
/// <param name="GuardAuthority">Current exact guard authority.</param><param name="RegistrarAuthority">Private compromise registrar.</param>
/// <param name="EvidenceAuthority">Independent actual guard/registrar protocol evidence.</param><param name="CapabilityTrust">Published retained capability anchors.</param>
/// <param name="InventoryConfigurationFile">Existing exact inventory authority configuration.</param><param name="InventoryConfigurationSha256">Accepted configuration bytes.</param>
/// <param name="CopyPolicyFile">Independently qualified full-copy policy evidence.</param><param name="CopyPolicySha256">Accepted policy evidence bytes.</param>
public sealed record ErasureRootHostInstallation(ErasureRootInstallation Native, string Endpoint, string CredentialReference,
    Dictionary<string, string> RegistryEpochs, IndependentDaprAuthorityBinding PrivateAuthority, IndependentDaprAuthorityBinding SourceAuthority,
    IndependentDaprAuthorityBinding GuardAuthority, IndependentDaprAuthorityBinding RegistrarAuthority, IndependentDaprAuthorityBinding EvidenceAuthority,
    PublishedDeletionCapabilityTrust[] CapabilityTrust, string InventoryConfigurationFile, string InventoryConfigurationSha256,
    string CopyPolicyFile, string CopyPolicySha256)
{
    /// <summary>Complete per-tenant independent authority enrollments. Required when more than one tenant is enrolled; legacy single-tenant bindings remain supported.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, ErasureRootTenantAuthorityBindings>? TenantAuthorities { get; init; }
    /// <summary>Optional isolated qualification controls at the real owner/provider boundaries. Omitted from existing serialized installations.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ErasureRootNativeControlsInstallation? QualificationNativeControls { get; init; }
}
