using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Selected native rotation/retention original, read by the independent invalidation port for one exact authenticated inventory signal.</summary>
public sealed record QualificationRotationOriginal(PlatformKeyInventorySignal Signal,long RotatedAt,long OldVerifyUntil,string PublicAnchorId,string PublicAnchorVersion,string InvalidationReceiptSha256);
