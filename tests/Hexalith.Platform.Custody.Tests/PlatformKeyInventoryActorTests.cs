using System.Reflection;
using System.Security.Cryptography;
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
    {
        var authority = Substitute.For<IPlatformKeyInventoryAuthority>(); long revision = 0;
        string digest = Digest(new PlatformKeyInventorySnapshot("tenant-a", Key().Purpose, Key().KeyAlias, 0, [], []));
        authority.ValidateStateAsync(Arg.Any<PlatformKeyVersion>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => call.Arg<long>() == revision && call.Arg<string>() == digest);
        authority.RecordRevisionAsync(Arg.Any<PlatformKeyVersion>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => {
            if (call.ArgAt<long>(1) != revision || call.ArgAt<long>(2) != revision + 1) { return false; } revision++; digest = call.Arg<string>(); return true;
        });
        authority.AuthorizeOperationAsync(Arg.Any<PlatformKeyVersion>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        authority.AuthorizeAsync(Arg.Any<PlatformKeyInventoryChange>(), Arg.Any<CancellationToken>()).Returns(true); return authority;
    }
    private static string Digest<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
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
        (await Actor(restored, authority).ApplyAsync(first)).ShouldBe(original); (await Actor(restored, authority).ReadAsync(Key())).Signals.Count.ShouldBe(3);
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
    /// <summary>Restoring current metadata before revocation, including a forged equal revision, cannot emit current key health under unchanged read credentials.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RestoredInventoryCannotUndoRevocation(bool equalRevision)
    {
        var backend = new InMemoryStateManager(); var authority = Authority(); var actor = Actor(backend, authority);
        await actor.ApplyAsync(new("install", 0, PlatformKeyInventoryAction.InstallCurrent, Key(), "original-provision"));
        var original = JsonSerializer.Deserialize<PlatformKeyInventorySnapshot>(JsonSerializer.Serialize(backend.CommittedState.Single().Value))!;
        await actor.ApplyAsync(new("revoke", 1, PlatformKeyInventoryAction.Revoke, Key(), "original-revocation")); var latest = backend.CommittedState.Single();
        var divergent = equalRevision ? original with { Revision = 2, Signals = ((PlatformKeyInventorySnapshot)latest.Value).Signals } : original;
        var restored = new InMemoryStateManager(); await restored.SetStateAsync(latest.Key, divergent, TestContext.Current.CancellationToken); await restored.SaveStateAsync(TestContext.Current.CancellationToken);
        var restarted = Actor(restored, authority); await Should.ThrowAsync<InvalidOperationException>(() => restarted.ReadAsync(Key()));
        await restored.SetStateAsync(latest.Key, JsonSerializer.Deserialize<PlatformKeyInventorySnapshot>(JsonSerializer.Serialize(latest.Value))!, TestContext.Current.CancellationToken); await restored.SaveStateAsync(TestContext.Current.CancellationToken);
        (await restarted.ReadAsync(Key())).Versions.Single().State.ShouldBe(PlatformHmacKeyState.Revoked);
    }

    /// <summary>Capacity denial does not advance the independent anchor or persist unreadable metadata; prior original signals remain readable.</summary>
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task InventoryCollectionBoundPreservesOriginalReadableSignals(bool signalBound)
    {
        var backend = new InMemoryStateManager(); var authority = Authority(); var actor = Actor(backend, authority);
        await actor.ApplyAsync(new("install", 0, PlatformKeyInventoryAction.InstallCurrent, Key(), "original-provision")); var saved = backend.CommittedState.Single();
        int count = signalBound ? 10000 : 1000;
        var versions = signalBound ? new[] { new PlatformKeyInventoryEntry(Key(), PlatformHmacKeyState.Active, 1) }
            : Enumerable.Range(1, count).Select(n => new PlatformKeyInventoryEntry(Key("v" + n), n == count ? PlatformHmacKeyState.Active : PlatformHmacKeyState.Retained, n)).ToArray();
        var signals = Enumerable.Range(1, count).Select(n => new PlatformKeyInventorySignal("install-" + n, new string('A', 64), n, PlatformKeyInventoryAction.InstallCurrent, Key("v" + n))).ToArray();
        var state = new PlatformKeyInventorySnapshot("tenant-a", Key().Purpose, Key().KeyAlias, count, versions, signals); string before = Digest(state);
        authority.ValidateStateAsync(Arg.Any<PlatformKeyVersion>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => call.Arg<long>() == count && call.Arg<string>() == before);
        await backend.SetStateAsync(saved.Key, state, TestContext.Current.CancellationToken); await backend.SaveStateAsync(TestContext.Current.CancellationToken);
        (await actor.ApplyAsync(new("next-install", count, PlatformKeyInventoryAction.InstallCurrent, Key("new-version"), "new-provision"))).ShouldBeNull();
        Digest(backend.CommittedState.Single().Value).ShouldBe(before); (await actor.ReadAsync(Key())).Signals.Count.ShouldBe(count);
        await authority.Received(1).RecordRevisionAsync(Arg.Any<PlatformKeyVersion>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

}
