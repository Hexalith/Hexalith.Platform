using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Protection;

/// <summary>Exact existing tenant-digest Dapr secret/version/field protocol and independently current inventory identity. Bytes use strict canonical 32-byte Base64.</summary>
public sealed record CustodyInteractionDigestReference(string ComponentName,string Key,string NativeVersion,string Field,PlatformKeyVersion InventoryKey);
