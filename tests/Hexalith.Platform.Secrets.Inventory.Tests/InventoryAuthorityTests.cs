using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;
using Hexalith.Tenants.Contracts.Events;

using Shouldly;

namespace Hexalith.Platform.Secrets.Inventory.Tests;

public sealed class InventoryAuthorityTests
{
    private static readonly PlatformHmacScope Scope = new("offline-native-tenant", PlatformHmacPurpose.TrustedEnvelope);

    [Fact]
    public async Task MissingIndependentTrustDeniesWithoutReadingTheProvider()
    {
        var sources = new InventoryTestSources(Metadata());
        CurrentInventoryAuthority authority = Authority(sources, trusted: false);
        (await authority.ResolveAsync("inventory", Scope, null, TestContext.Current.CancellationToken)).ShouldBeNull();
        sources.EnrollmentReads.ShouldBe(0);
    }

    [Fact]
    public async Task ExactCurrentMappingPreservesOpaqueAndNativeNumericVersions()
    {
        var sources = new InventoryTestSources(Metadata());
        DaprPlatformHmacInventory? result = await Authority(sources).ResolveAsync("inventory", Scope, "opaque-v7", TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.Metadata.Version.ShouldBe("opaque-v7");
        result.SecretVersion.ShouldBe("1");
        result.SecretReference.ShouldBe("ext-secrets-1-isolated/exact-tenant-purpose-key");
        sources.EnrollmentReads.ShouldBe(2);
    }

    [Fact]
    public async Task MissingProviderMappingNeverBecomesSyntheticMetadata()
    {
        (await Authority(new InventoryTestSources(null)).ResolveAsync("inventory", Scope, null, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task TenantDisableDuringTheReadDeniesThePreviouslyCurrentMapping()
    {
        var sources = new InventoryTestSources(Metadata()) { DisableAfterFirstRead = true };
        (await Authority(sources).ResolveAsync("inventory", Scope, null, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task ChangedProviderMetadataDeniesThePreviouslyCurrentMapping()
    {
        var sources = new InventoryTestSources(Metadata()) { ChangeMetadata = true };
        (await Authority(sources).ResolveAsync("inventory", Scope, null, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task WrongOpaqueVersionCannotSelectAnExistingNumericVersion()
        => (await Authority(new InventoryTestSources(Metadata())).ResolveAsync("inventory", Scope, "opaque-foreign-version", TestContext.Current.CancellationToken)).ShouldBeNull();

    [Fact]
    public async Task ChangedIndependentRevocationRevisionDenies()
        => (await Authority(new InventoryTestSources(Metadata()), changingTrust: true).ResolveAsync("inventory", Scope, null, TestContext.Current.CancellationToken)).ShouldBeNull();

    [Theory]
    [InlineData("01", false, "")]
    [InlineData("1", true, "")]
    [InlineData("1", false, "2026-01-01T00:00:00Z")]
    public async Task NoncanonicalDestroyedOrDeletedBackendVersionsDeny(string version, bool destroyed, string deleted)
    {
        (await Authority(new InventoryTestSources(Metadata(version, destroyed, deleted)))
            .ResolveAsync("inventory", Scope, null, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public void DuplicateProviderMappingPropertiesAreRejected()
        => InventorySources.ParseObject("{\"opaque\":1,\"opaque\":2}").ShouldBeNull();

    [Fact]
    public void NativeTenantDisableIsFoldedFromPersistedEvents()
    {
        JsonObject page = Page(new TenantCreated(Scope.TenantId, "Offline native event", null, DateTimeOffset.UtcNow));
        InventorySources.Fold(InventorySources.ParseObject(page.ToJsonString()), Scope.TenantId)!.Active.ShouldBeTrue();
        AddEvent(page, new TenantDisabled(Scope.TenantId, DateTimeOffset.UtcNow));
        InventorySources.Fold(InventorySources.ParseObject(page.ToJsonString()), Scope.TenantId)!.Active.ShouldBeFalse();
        page["metadata"]!["latestSequence"] = 3;
        InventorySources.Fold(InventorySources.ParseObject(page.ToJsonString()), Scope.TenantId).ShouldBeNull();
    }

    private static CurrentInventoryAuthority Authority(InventoryTestSources sources, bool trusted = true, bool changingTrust = false)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        PlatformSigningProfile profile = PlatformAcceptedEnvelopeTiming.Bind("offline-profile", "offline-issuer", "offline-audience", now.AddMinutes(-1), now.AddHours(1));
        var configuration = new SecretsRuntimeConfiguration
        {
            CurrentInventoryAuthorityReference = "inventory", ProviderBinding = "provider", HmacCredentialReference = "provider-read-reference",
            ProfileReference = "independent-profile", ExpectedProfile = profile, PublicTrustReference = trusted ? "offline-trust-port" : null,
            CurrentRevocationReference = "offline-revocation-port",
        };
        JsonArray permitted = [new JsonObject { ["tenantId"] = Scope.TenantId, ["purpose"] = Scope.Purpose.ToString(),
            ["secretReference"] = "ext-secrets-1-isolated/exact-tenant-purpose-key", ["secretField"] = "hmac" }];
        return new(configuration, new InventoryTestProfile(profile) { ChangeRevision = changingTrust }, sources, permitted, TimeProvider.System);
    }

    internal static JsonObject Metadata(string version = "1", bool destroyed = false, string deleted = "")
    {
        DateTimeOffset created = DateTimeOffset.UtcNow.AddMinutes(-2);
        JsonObject mappings = new() { ["opaque-v7"] = new JsonObject { ["secretVersion"] = version, ["state"] = "Active",
            ["notBefore"] = created.AddSeconds(1).ToString("O"), ["verifyUntil"] = created.AddHours(2).ToString("O"), ["retiredAt"] = null } };
        JsonObject metadata = new() { ["current_version"] = 1, ["custom_metadata"] = new JsonObject
            { ["ext-secrets-1.tenantId"] = Scope.TenantId, ["ext-secrets-1.purpose"] = Scope.Purpose.ToString(),
              ["ext-secrets-1.secretField"] = "hmac", ["ext-secrets-1.versionMappings"] = mappings.ToJsonString() },
            ["versions"] = new JsonObject { ["1"] = new JsonObject { ["created_time"] = created.ToString("O"), ["destroyed"] = destroyed, ["deletion_time"] = deleted } } };
        return InventorySources.ParseObject(metadata.ToJsonString())!;
    }

    internal static JsonObject Page(object first)
    {
        var page = new JsonObject { ["tenant"] = "system", ["domain"] = "tenants", ["aggregateId"] = Scope.TenantId,
            ["events"] = new JsonArray(), ["metadata"] = new JsonObject { ["fromSequence"] = 0, ["isTruncated"] = false, ["nextContinuationToken"] = null } };
        AddEvent(page, first);
        return page;
    }

    internal static void AddEvent(JsonObject page, object value)
    {
        var events = (JsonArray)page["events"]!;
        events.Add(new JsonObject { ["eventTypeName"] = value.GetType().FullName, ["sequenceNumber"] = events.Count + 1,
            ["serializationFormat"] = "json", ["payload"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))) });
        page["metadata"]!["latestSequence"] = events.Count;
        page["metadata"]!["eventCount"] = events.Count;
    }
}
