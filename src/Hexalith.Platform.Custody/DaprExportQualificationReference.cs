namespace Hexalith.Platform.Custody;

/// <summary>Exact version-selected Dapr asset and explicit codec, bound to its independently current tenant/purpose inventory key.</summary>
public sealed record DaprExportQualificationReference(string Component,string Key,string Version,string Field,string Codec,PlatformKeyVersion InventoryKey);
