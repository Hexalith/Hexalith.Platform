namespace Hexalith.Platform.Custody;

/// <summary>Exact public export canary identity and independently enrolled manifest. It grants no delivery, destruction or approval authority.</summary>
public sealed record ExportQualificationInput(string TenantId,string ExportId,string KekVersion,string SigningKeyVersion,long ManifestVersion,byte[] Manifest,byte[]? ForeignPublicAnchor=null);
