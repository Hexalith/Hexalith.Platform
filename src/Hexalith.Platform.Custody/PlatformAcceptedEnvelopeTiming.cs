namespace Hexalith.Platform.Custody;

/// <summary>Owner-accepted 2026-10-08 numeric bounds only; issuer/audience/version/validity and independent trust still require exact external binding.</summary>
public static class PlatformAcceptedEnvelopeTiming
{
    /// <summary>Gets the accepted maximum tag lifetime L of five minutes.</summary>
    public static TimeSpan MaximumLifetime => TimeSpan.FromMinutes(5);
    /// <summary>Gets future-issued tolerance S of thirty seconds; expiry remains exclusive without grace.</summary>
    public static TimeSpan ClockSkew => TimeSpan.FromSeconds(30);
    /// <summary>Gets ten-minute routine healthy old-key verification overlap O; compromised/revoked keys never receive overlap.</summary>
    public static TimeSpan RotationOverlap => TimeSpan.FromMinutes(10);
    /// <summary>Gets automatic recovery horizon H of twenty-four hours.</summary>
    public static TimeSpan RecoveryHorizon => TimeSpan.FromHours(24);
    /// <summary>Gets first-seen replay retention R of seven days; this does not mint a replay-registrar credential.</summary>
    public static TimeSpan ReplayRetention => TimeSpan.FromDays(7);
    /// <summary>Applies only the accepted numeric bounds to independently supplied exact identity and validity. Does not register an available profile.</summary>
    public static PlatformSigningProfile Bind(string version, string issuer, string audience, DateTimeOffset notBefore, DateTimeOffset validUntil)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version); ArgumentException.ThrowIfNullOrWhiteSpace(issuer); ArgumentException.ThrowIfNullOrWhiteSpace(audience);
        if (notBefore.Offset != TimeSpan.Zero || validUntil.Offset != TimeSpan.Zero || notBefore >= validUntil) { throw new ArgumentException("Invalid independently supplied profile validity."); }
        return new(version, issuer, audience, notBefore, validUntil, MaximumLifetime, ClockSkew, RotationOverlap, RecoveryHorizon, ReplayRetention);
    }
}
