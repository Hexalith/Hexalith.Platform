namespace Hexalith.Platform.Custody;

/// <summary>Private export-manifest signature context; no decision-approval signing purpose exists.</summary>
/// <param name="TenantId">The exact owner tenant.</param>
/// <param name="ExportId">The immutable export identity.</param>
/// <param name="ManifestVersion">The positive frozen manifest version.</param>
/// <param name="SigningKeyVersion">The exact export-manifest signing-key version.</param>
internal sealed record ExportManifestSignatureContext(string TenantId, string ExportId, long ManifestVersion, string SigningKeyVersion);
