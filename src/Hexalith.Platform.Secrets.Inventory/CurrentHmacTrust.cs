using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Inventory;

/// <summary>Fresh independent profile, revocation and installed-host enrollment observation.</summary>
/// <param name="Profile">Exact currently published profile.</param>
/// <param name="Revision">Stable independent trust/revocation/enrollment revision.</param>
/// <param name="ObservedAt">Actual independent observation time.</param>
/// <param name="ValidUntil">Exclusive observation validity.</param>
public sealed record CurrentHmacTrust(PlatformSigningProfile Profile, string Revision, DateTimeOffset ObservedAt, DateTimeOffset ValidUntil);
