using System.Reflection;
using System.Text.Json;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Testing.Fakes;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Custody.Tests;

/// <summary>Actual durable opaque inventory with synthetic independent authority; no physical custody/publication qualification.</summary>
public sealed class PlatformKeyInventoryActorTests
{
    private static PlatformKeyVersion Key(string version = "v1") => new("tenant-a", PlatformKeyPurpose.DeletionBatchCapabilitySigningKey, "deletion-signing", version,
        "qualified-backend-candidate", "independent-profile", "anchor", "anchor-" + version);
    private static PlatformKeyInventoryActor Actor(InMemoryStateManager state, IPlatformKeyInventoryAuthority? authority = null)
    {
        var actor = new PlatformKeyInventoryActor(ActorHost.CreateForTest<PlatformKeyInventoryActor>(new ActorTestOptions { ActorId = new(PlatformKeyInventoryActor.GetActorId(Key())) }), authority);
        typeof(Dapr.Actors.Runtime.Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance)!.SetValue(actor, state); return actor;
    }
    private static IPlatformKeyInventoryAuthority Authority()
    { var authority = Substitute.For<IPlatformKeyInventoryAuthority>(); authority.AuthorizeOperationAsync(Arg.Any<PlatformKeyVersion>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true); authority.AuthorizeAsync(Arg.Any<PlatformKeyInventoryChange>(), Arg.Any<CancellationToken>()).Returns(true); return authority; }
    /// <summary>Routine rotation retains old verifier metadata; emergency revocation is irreversible and exact outcomes survive serialized restart.</summary>
    [Fact]
    public async Task RotationRetainsVersionsAndRevocationCannotBeUndone()
    {
        var state = new InMemoryStateManager(); var authority = Authority(); var actor = Actor(state, authority);
        var first = new PlatformKeyInventoryChange("install-v1", 0, PlatformKeyInventoryAction.InstallCurrent, Key(), "provision-1");
        var original = await actor.ApplyAsync(first); await actor.ApplyAsync(new("install-v2", 1, PlatformKeyInventoryAction.InstallCurrent, Key("v2"), "provision-2"));
        var revoke = new PlatformKeyInventoryChange("revoke-v1", 2, PlatformKeyInventoryAction.Revoke, Key(), "authenticated-revocation-1"); await actor.ApplyAsync(revoke);
        var snapshot = await actor.ReadAsync(Key()); snapshot.Versions.Single(v => v.Key.Version == "v1").State.ShouldBe(PlatformHmacKeyState.Revoked);
        snapshot.Versions.Single(v => v.Key.Version == "v2").State.ShouldBe(PlatformHmacKeyState.Active); snapshot.Versions.Single(v => v.Key.Version == "v1").Key.PublicAnchorId.ShouldBe("anchor");
        (await actor.ApplyAsync(first)).ShouldBe(original); (await actor.ApplyAsync(new("reinstall-v1", 3, PlatformKeyInventoryAction.InstallCurrent, Key(), "self-restore"))).ShouldBeNull();
        var saved = state.CommittedState.Single(); var restored = new InMemoryStateManager(); await restored.SetStateAsync(saved.Key,
            JsonSerializer.Deserialize<PlatformKeyInventorySnapshot>(JsonSerializer.Serialize(saved.Value))!, TestContext.Current.CancellationToken); await restored.SaveStateAsync(TestContext.Current.CancellationToken);
        (await Actor(restored, Authority()).ApplyAsync(first)).ShouldBe(original); (await Actor(restored, Authority()).ReadAsync(Key())).Signals.Count.ShouldBe(3);
    }
    /// <summary>Missing independent provision authority never installs configured metadata or a key.</summary>
    [Fact]
    public async Task MissingAuthorityDoesNotInstall()
    {
        var state = new InMemoryStateManager(); (await Actor(state).ApplyAsync(new("op", 0, PlatformKeyInventoryAction.InstallCurrent, Key(), "evidence"))).ShouldBeNull(); state.CommittedState.ShouldBeEmpty();
        Enum.GetNames<PlatformKeyPurpose>().ShouldNotContain("DecisionApprovalSigningKey");
    }
    /// <summary>Changed exact input/tenant/purpose/backend/anchor or stale compare cannot overwrite original version.</summary>
    [Fact]
    public async Task ScopeAndChangedIdentityFailClosed()
    {
        var state = new InMemoryStateManager(); var actor = Actor(state, Authority()); var change = new PlatformKeyInventoryChange("op", 0, PlatformKeyInventoryAction.InstallCurrent, Key(), "evidence"); await actor.ApplyAsync(change);
        await Should.ThrowAsync<ArgumentException>(() => actor.ApplyAsync(change with { AuthenticatedEvidenceId = "changed" }));
        await Should.ThrowAsync<ArgumentException>(() => actor.ReadAsync(Key() with { TenantId = "tenant-b" }));
        await Should.ThrowAsync<ArgumentException>(() => actor.ReadAsync(Key() with { Purpose = PlatformKeyPurpose.ExportManifestSigningKey }));
        (await actor.ApplyAsync(new("op2", 1, PlatformKeyInventoryAction.Revoke, Key() with { ProviderTarget = "changed" }, "evidence2"))).ShouldBeNull();
        (await actor.ApplyAsync(new("op3", 0, PlatformKeyInventoryAction.InstallCurrent, Key("v2"), "evidence3"))).ShouldBeNull();
        (await actor.ReadAsync(Key())).Revision.ShouldBe(1);
    }

    /// <summary>Configured or retained metadata never bypasses current exact private inventory-read credentials.</summary>
    [Fact]
    public async Task InventoryReadRequiresCurrentSeparatePrivateCredential()
    {
        var state = new InMemoryStateManager(); var authority = Authority(); var actor = Actor(state, authority);
        await actor.ApplyAsync(new("op", 0, PlatformKeyInventoryAction.InstallCurrent, Key(), "evidence"));
        await Should.ThrowAsync<UnauthorizedAccessException>(() => Actor(state).ReadAsync(Key()));
        authority.AuthorizeOperationAsync(Key(), "ReadKeyInventory", Arg.Any<CancellationToken>()).Returns(false);
        await Should.ThrowAsync<UnauthorizedAccessException>(() => actor.ReadAsync(Key()));
        state.CommittedState.Single().Value.ShouldBeOfType<PlatformKeyInventorySnapshot>().Revision.ShouldBe(1);
    }
}
