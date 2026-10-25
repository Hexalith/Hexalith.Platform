namespace Hexalith.Platform.Custody;

/// <summary>Actual encrypted export artifact or detached public signature original; it contains no root, KEK or private signing bytes.</summary>
public sealed record ExportQualificationOriginal(string CaseId,string TenantId,string ExportId,string KekVersion,string SigningKeyVersion,long ManifestVersion,
    string ManifestSha256,byte[]? Nonce,byte[]? Ciphertext,byte[]? Tag,string? DetachedJws,string? PublicAnchorSha256,bool OwnedBuffersCleared);
