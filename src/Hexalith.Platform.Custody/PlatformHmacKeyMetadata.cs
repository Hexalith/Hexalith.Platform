namespace Hexalith.Platform.Custody;


/// <summary>Public lifecycle facts for one version, with no secret material.</summary>
/// <param name="Version">The opaque key version.</param>
/// <param name="State">The current lifecycle state.</param>
/// <param name="NotBefore">The inclusive first key-use instant.</param>
/// <param name="VerifyUntil">The exclusive final key-use instant.</param>
/// <param name="RetiredAt">The rotation instant required for a retained key.</param>
public sealed record PlatformHmacKeyMetadata(
    string Version, PlatformHmacKeyState State, DateTimeOffset NotBefore,
    DateTimeOffset VerifyUntil, DateTimeOffset? RetiredAt);
