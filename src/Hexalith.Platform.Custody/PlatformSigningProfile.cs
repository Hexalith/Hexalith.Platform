namespace Hexalith.Platform.Custody;

/// <summary>Explicit owner configuration, with no production numeric defaults.</summary>
/// <param name="Version">Accepted configuration version.</param>
/// <param name="Issuer">Exact authenticated issuer.</param>
/// <param name="Audience">Exact verifier audience.</param>
/// <param name="NotBefore">Inclusive validity start.</param>
/// <param name="ValidUntil">Exclusive validity end.</param>
/// <param name="MaximumLifetime">Maximum tag lifetime L.</param>
/// <param name="ClockSkew">Allowed future skew S.</param>
/// <param name="RotationOverlap">Envelope verification overlap O.</param>
/// <param name="RecoveryHorizon">Maximum retry/recovery horizon H.</param>
/// <param name="ReplayRetention">Required replay retention R.</param>
public sealed record PlatformSigningProfile(string Version, string Issuer, string Audience,
    DateTimeOffset NotBefore, DateTimeOffset ValidUntil, TimeSpan MaximumLifetime, TimeSpan ClockSkew,
    TimeSpan RotationOverlap, TimeSpan RecoveryHorizon, TimeSpan ReplayRetention)
{
    /// <summary>Checks numeric constraints, overflow and current exclusive validity.</summary>
    public bool IsValid(DateTimeOffset now)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(Version) && !string.IsNullOrWhiteSpace(Issuer)
                && !string.IsNullOrWhiteSpace(Audience) && NotBefore <= now && now < ValidUntil
                && MaximumLifetime > TimeSpan.Zero && RecoveryHorizon > TimeSpan.Zero
                && ReplayRetention > TimeSpan.Zero && ClockSkew >= TimeSpan.Zero && RotationOverlap >= TimeSpan.Zero
                && ReplayRetention >= MaximumLifetime + ClockSkew + RotationOverlap && ReplayRetention >= RecoveryHorizon;
        }
        catch (OverflowException)
        {
            return false;
        }
    }
}
