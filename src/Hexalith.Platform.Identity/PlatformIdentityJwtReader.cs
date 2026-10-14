namespace Hexalith.Platform.Identity;

/// <summary>Explicit independently authenticated JWT service identity admitted only for authoritative reads.</summary>
public sealed class PlatformIdentityJwtReader
{
    /// <summary>The source that must also be enabled in ReaderSources.</summary>
    public string SourceId { get; set; } = string.Empty;

    /// <summary>The exact validated issuer.</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>The exact service account subject; human subjects are not inferred or enrolled.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>The exact validated authorized client.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>The issuer's service client claim type.</summary>
    public string ClientIdClaimType { get; set; } = "azp";

    /// <summary>The identity authentication type produced by the configured JWT bearer validator.</summary>
    public string AuthenticationType { get; set; } = "AuthenticationTypes.Federation";
}
