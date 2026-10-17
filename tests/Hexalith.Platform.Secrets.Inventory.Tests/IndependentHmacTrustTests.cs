using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

using Shouldly;

namespace Hexalith.Platform.Secrets.Inventory.Tests;

public sealed class IndependentHmacTrustTests
{
    [Theory]
    [InlineData("authorityReference")]
    [InlineData("publicTrustReference")]
    [InlineData("profileReference")]
    [InlineData("inventoryAuthorityReference")]
    [InlineData("profileVersion")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("tenantId")]
    [InlineData("purpose")]
    [InlineData("version")]
    public async Task EveryIndependentProofEchoIsBound(string field)
    {
        SecretsRuntimeConfiguration config = Configuration();
        var trust = new DaprHmacCurrentTrust(config, new InventoryTestInvoker(config.ExpectedProfile!) { MutatedEcho = field }, TimeProvider.System);
        (await trust.ReadAsync(new("offline-tenant", PlatformHmacPurpose.TrustedEnvelope), "opaque-version", TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public async Task RevokedStaleOrUnenrolledIndependentProofDenies(bool revoked, bool stale, bool hostEnrolled)
    {
        SecretsRuntimeConfiguration configuration = Configuration();
        var invoker = new InventoryTestInvoker(configuration.ExpectedProfile!) { Revoked = revoked, Stale = stale, HostEnrolled = hostEnrolled };
        var trust = new DaprHmacCurrentTrust(configuration, invoker, TimeProvider.System);
        (await trust.ReadAsync(new("offline-tenant", PlatformHmacPurpose.TrustedEnvelope), "opaque-version", TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task CurrentIndependentProofIsRequiredInAdditionToConfiguredReferences()
    {
        SecretsRuntimeConfiguration configuration = Configuration();
        var trust = new DaprHmacCurrentTrust(configuration, new InventoryTestInvoker(configuration.ExpectedProfile!), TimeProvider.System);
        (await trust.ReadAsync(null, null, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        var missing = new DaprHmacCurrentTrust(configuration with { CurrentRevocationReference = null }, new InventoryTestInvoker(configuration.ExpectedProfile!), TimeProvider.System);
        (await missing.ReadAsync(null, null, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    private static SecretsRuntimeConfiguration Configuration()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        PlatformSigningProfile profile = PlatformAcceptedEnvelopeTiming.Bind("offline-profile", "offline-issuer", "offline-audience", now.AddMinutes(-1), now.AddHours(1));
        return new() { CurrentInventoryAuthorityReference = "offline-inventory", ProfileReference = "offline-profile-authority",
            Issuer = profile.Issuer, Audience = profile.Audience, ProfileVersion = profile.Version, ProfileNotBefore = profile.NotBefore,
            ProfileValidUntil = profile.ValidUntil, ExpectedProfile = profile, CallTimeout = TimeSpan.FromSeconds(5),
            PublicTrustReference = "offline-independent-trust", CurrentRevocationReference = "offline-independent-revocation" };
    }
}
