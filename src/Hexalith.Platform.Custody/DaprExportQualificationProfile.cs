namespace Hexalith.Platform.Custody;

/// <summary>Explicit independently enrolled export resource profile. No component, key, version, codec, tenant or public anchor is inferred.</summary>
public sealed record DaprExportQualificationProfile(string TenantId,string ExportId,DateTimeOffset NotBefore,DateTimeOffset ValidUntil,
    DaprExportQualificationReference? ExportKey,DaprExportQualificationReference? Kek,DaprExportQualificationReference? Signer,
    DaprExportQualificationReference PublicAnchor,string PublicAnchorId,string PublicAnchorVersion);
