using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;
using Shouldly;

namespace Hexalith.Platform.Secrets.Inventory.Tests;

public sealed class InventoryBoundaryTests
{
    internal static readonly PlatformHmacScope Scope = new("offline-native-tenant", PlatformHmacPurpose.TrustedEnvelope);
    internal static JsonArray Permitted() => [new JsonObject { ["tenantId"] = Scope.TenantId, ["purpose"] = Scope.Purpose.ToString(),
        ["secretReference"] = "ext-secrets-1-isolated/exact-tenant-purpose-key", ["secretField"] = "hmac" }];
    internal static SecretsRuntimeConfiguration Configuration(PlatformSigningProfile profile) => new()
    { CurrentInventoryAuthorityReference = "inventory", ProviderBinding = "provider", HmacCredentialReference = "provider-read-reference",
        TenantInventoryReference = "native-registry", ProfileReference = "independent-profile", ExpectedProfile = profile,
        Issuer = profile.Issuer, Audience = profile.Audience, ProfileVersion = profile.Version, ProfileNotBefore = profile.NotBefore,
        ProfileValidUntil = profile.ValidUntil, CallTimeout = TimeSpan.FromSeconds(5), PublicTrustReference = "offline-trust-port", CurrentRevocationReference = "offline-revocation-port" };

    [Theory]
    [InlineData(5, 20, 25, 5)]
    [InlineData(25, 5, 20, 5)]
    [InlineData(25, 20, 5, 5)]
    public async Task ProofCannotOutliveProfileTrustOrKey(int profileSeconds, int trustSeconds, int keySeconds, int expected)
    {
        var clock = new MutableInventoryClock(DateTimeOffset.UtcNow);
        PlatformSigningProfile profile = PlatformAcceptedEnvelopeTiming.Bind("p", "issuer", "audience", clock.Now.AddMinutes(-1), clock.Now.AddSeconds(profileSeconds));
        JsonObject metadata = InventoryAuthorityTests.Metadata();
        JsonObject map = JsonNode.Parse(metadata["custom_metadata"]!["ext-secrets-1.versionMappings"]!.GetValue<string>())!.AsObject();
        map["opaque-v7"]!["verifyUntil"] = clock.Now.AddSeconds(keySeconds).ToString("O");
        metadata["custom_metadata"]!["ext-secrets-1.versionMappings"] = map.ToJsonString();
        var config = Configuration(profile);
        var authority = new CurrentInventoryAuthority(config, new InventoryTestProfile(profile) { Clock = clock, Expires = clock.Now.AddSeconds(trustSeconds) }, new InventoryTestSources(metadata), Permitted(), clock);
        (await authority.ResolveAsync("inventory", Scope, null, TestContext.Current.CancellationToken))!.ValidUntil.ShouldBe(clock.Now.AddSeconds(expected));
    }

    [Fact]
    public async Task KeyExpiryDuringFinalReadDeniesAtExclusiveBoundary()
    {
        var clock = new MutableInventoryClock(DateTimeOffset.UtcNow);
        PlatformSigningProfile profile = PlatformAcceptedEnvelopeTiming.Bind("p", "issuer", "audience", clock.Now.AddMinutes(-1), clock.Now.AddHours(1));
        JsonObject metadata = InventoryAuthorityTests.Metadata();
        var map = JsonNode.Parse(metadata["custom_metadata"]!["ext-secrets-1.versionMappings"]!.GetValue<string>())!;
        map["opaque-v7"]!["verifyUntil"] = clock.Now.AddSeconds(1).ToString("O");
        metadata["custom_metadata"]!["ext-secrets-1.versionMappings"] = map.ToJsonString();
        var sources = new InventoryTestSources(metadata) { OnMetadataRead = count => { if (count == 2) clock.Now = clock.Now.AddSeconds(1); } };
        var authority = new CurrentInventoryAuthority(Configuration(profile), new InventoryTestProfile(profile) { Clock = clock }, sources, Permitted(), clock);
        (await authority.ResolveAsync("inventory", Scope, null, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("number")]
    public async Task NativeDeletionTimeMustBeExplicitEmptyString(string kind)
    {
        JsonObject metadata = InventoryAuthorityTests.Metadata();
        JsonObject version = metadata["versions"]!["1"]!.AsObject();
        if (kind == "missing") version.Remove("deletion_time"); else version["deletion_time"] = kind == "number" ? JsonValue.Create(0) : null;
        var clock = new MutableInventoryClock(DateTimeOffset.UtcNow);
        var profile = PlatformAcceptedEnvelopeTiming.Bind("p", "issuer", "audience", clock.Now.AddMinutes(-1), clock.Now.AddHours(1));
        var authority = new CurrentInventoryAuthority(Configuration(profile), new InventoryTestProfile(profile), new InventoryTestSources(metadata), Permitted(), clock);
        (await authority.ResolveAsync("inventory", Scope, null, TestContext.Current.CancellationToken)).ShouldBeNull();
    }
}
