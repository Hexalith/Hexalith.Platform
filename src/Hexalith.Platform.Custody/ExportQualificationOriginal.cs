namespace Hexalith.Platform.Custody;

/// <summary>Actual encrypted export artifact or detached public signature original; it contains no root, KEK or private signing bytes.</summary>
public sealed record ExportQualificationOriginal(string CaseId,string TenantId,string ExportId,string KekVersion,string SigningKeyVersion,long ManifestVersion,
    string ManifestSha256,byte[]? Nonce,byte[]? Ciphertext,byte[]? Tag,string? DetachedJws,string? PublicAnchorSha256,bool OwnedBuffersCleared)
{
    /// <summary>Versioned independent anchor facts; absent legacy facts remain unknown.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? AnchorBindingVersion { get; init; }
    /// <summary>Actual independent public anchor identity at signing.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? PublicAnchorId { get; init; }
    /// <summary>Actual independent public anchor version at signing.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? PublicAnchorVersion { get; init; }
}
