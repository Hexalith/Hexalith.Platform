namespace Hexalith.Platform.Identity;

/// <summary>Explicit trusted identity configuration, retaining existing authentication.</summary>
public sealed class PlatformIdentityOptions
{
    /// <summary>Gets or sets configured trusted issuer identifiers for operator verification.</summary>
    public string[] TrustedIssuers { get; set; } = [];

    /// <summary>Gets or sets trusted provisioning workloads.</summary>
    public string[] ProvisioningSources { get; set; } = [];

    /// <summary>Gets or sets the narrow identity-service writer workloads.</summary>
    public string[] IdentityWriterSources { get; set; } = [];

    /// <summary>Gets or sets trusted authoritative reader workloads.</summary>
    public string[] ReaderSources { get; set; } = [];

    /// <summary>Gets or sets the private purpose-keyed alias digest secret.</summary>
    public string? AliasKeyBase64 { get; set; }

    /// <summary>Gets or sets the configured identity-service registry capability source.</summary>
    public string? RegistryServiceSourceId { get; set; }

    /// <summary>Gets or sets the current approved trust-policy revision.</summary>
    public long AuthorityRevision { get; set; }

    /// <summary>Gets or sets independently signed, explicitly approved first-operator provenance sources.</summary>
    public string[] BootstrapProvenanceSources { get; set; } = [];

    /// <summary>Gets or sets the narrow verified enrollment-provenance source identifiers.</summary>
    public string[] OperatorProvenanceSources { get; set; } = [];
}
