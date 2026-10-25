namespace Hexalith.Platform.Composition;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

/// <summary>A dedicated REST owner identity and its secret Aspire parameter.</summary>
public sealed class PlatformOwnerClientBinding
{
    /// <summary>Creates an enrollment from validated metadata and a per-run secret.</summary>
    internal PlatformOwnerClientBinding(string clientId, string audience, string operation, string tenantId,
        string permission, string secret, IResourceBuilder<ParameterResource> secretParameter)
    {
        ClientId = clientId;
        Audience = audience;
        Operation = operation;
        TenantId = tenantId;
        Permission = permission;
        Secret = secret;
        SecretParameter = secretParameter;
    }

    /// <summary>Gets the dedicated OAuth client identifier.</summary>
    public string ClientId { get; }

    /// <summary>Gets the sole receiving audience.</summary>
    public string Audience { get; }

    /// <summary>Gets the sole token request/cache operation, whose receiver enforcement remains owner-defined.</summary>
    public string Operation { get; }

    /// <summary>Gets the explicitly enrolled tenant.</summary>
    public string TenantId { get; }

    /// <summary>Gets the REST permission claim; Memories natively enforces tenant membership instead.</summary>
    public string Permission { get; }

    /// <summary>Gets the secret parameter supplied to the consuming resource.</summary>
    public IResourceBuilder<ParameterResource> SecretParameter { get; }

    /// <summary>Gets the generated value used only when rendering the private realm import.</summary>
    internal string Secret { get; }
}
