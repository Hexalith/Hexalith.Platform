namespace Hexalith.Platform.Custody;

/// <summary>Independently current public export verifier identity. This contains no private signing key.</summary>
public sealed record ExportQualificationTrust(string TenantId,string SigningKeyVersion,string PublicAnchorId,string PublicAnchorVersion,byte[] SubjectPublicKeyInfo);
