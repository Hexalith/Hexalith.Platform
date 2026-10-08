using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Hexalith.Platform.Custody.Tests;

/// <summary>Accepted numeric binding never invents authority, a validity period or an available default profile.</summary>
public sealed class PlatformAcceptedEnvelopeTimingTests
{
    /// <summary>Exact accepted values satisfy both retention inequalities while expiry remains exclusive.</summary>
    [Fact]
    public void AcceptedBoundsRequireExplicitIdentityAndExclusiveValidity()
    {
        var now = new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
        var profile = PlatformAcceptedEnvelopeTiming.Bind("external-v1", "independent-issuer", "exact-audience", now, now.AddDays(1));
        profile.MaximumLifetime.ShouldBe(TimeSpan.FromMinutes(5)); profile.ClockSkew.ShouldBe(TimeSpan.FromSeconds(30));
        profile.RotationOverlap.ShouldBe(TimeSpan.FromMinutes(10)); profile.RecoveryHorizon.ShouldBe(TimeSpan.FromHours(24)); profile.ReplayRetention.ShouldBe(TimeSpan.FromDays(7));
        profile.IsValid(now).ShouldBeTrue(); profile.IsValid(profile.ValidUntil).ShouldBeFalse();
        Should.Throw<ArgumentException>(() => PlatformAcceptedEnvelopeTiming.Bind("", "issuer", "audience", now, now.AddDays(1)));
        Should.Throw<ArgumentException>(() => PlatformAcceptedEnvelopeTiming.Bind("v1", "issuer", "audience", now, now));
    }
    /// <summary>Numeric acceptance alone leaves the standard production profile provider unavailable.</summary>
    [Fact]
    public void AcceptedNumbersDoNotEnableDefaultAuthority()
    {
        using var services = new ServiceCollection().AddPlatformCustody().BuildServiceProvider();
        services.GetRequiredService<IPlatformSigningProfileProvider>().GetCurrent().ShouldBeNull();
    }
}
