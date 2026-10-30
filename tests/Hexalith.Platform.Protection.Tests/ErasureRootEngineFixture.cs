using System.Reflection;
using Dapr.Actors.Runtime;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;
using Hexalith.EventStore.Server.Security;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Platform.Custody;
using NSubstitute;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Actual engine, registry actor, ERC authority and native-operation fixture composition. No installed services or physical-erasure qualification.</summary>
internal sealed class ErasureRootEngineFixture : IDisposable
{
    internal ErasureRootStoreFixture Store { get; }
    internal ErasureRootAuthorizationFixture Permissions { get; }
    internal ErasureRootSourceFixture Source { get; } = new();
    internal InMemoryStateManager RegistryState { get; } = new();
    internal ErasureRootCustody Custody { get; }
    internal ErasureRootOccurrenceBindings Bindings { get; }
    internal ErasureRootOccurrenceAuthority Authority { get; }
    internal ErasureRootRegistryFixture Registry { get; }
    internal ErasureRootOccurrenceKeyProvider Keys { get; }
    internal ErasureRootManifestProvider Manifests { get; }
    internal InteractionPayloadProtectionService Engine { get; }
    internal ErasureRootDigestKeyResolver Digests { get; }
    internal bool DigestAvailable { get; set; } = true;
    private readonly DaprClient _dapr = Substitute.For<DaprClient>();
    private readonly IPlatformKeyInventoryActor _inventory = Substitute.For<IPlatformKeyInventoryActor>();
    private readonly IPlatformKeyInventoryAuthority _inventoryAuthority = Substitute.For<IPlatformKeyInventoryAuthority>();
    internal ErasureRootEngineFixture(ErasureRootConsumptionFixture? shared = null)
    {
        Store = shared?.Store ?? new(); Permissions = shared?.Permissions ?? new();
        var installation = new ErasureRootInstallation(shared is null ? "erc-test" : "erc-v23", "enrollment", "transit", "heads", new Dictionary<string, string> { ["tenant-a"] = "epoch" });
        string registryScope = ErasureRootOccurrenceAuthority.Scope("tenant-a"), genesis = ErasureRootCustody.Hash(new InteractionOccurrenceRegistrySnapshot("tenant-a", "registry-1", 0, []));
        if (shared is null) { Store.Enroll(installation, "tenant-a", registryScope, genesis); }
        else { var head = Store.HeadAsync("tenant-a").GetAwaiter().GetResult(); head.Anchors.Add(registryScope, new(0, genesis, [], [])); Store.ReplaceHead("tenant-a", head); }
        Custody = shared?.Custody ?? new(Store, installation, Permissions);
        Permissions.EffectBoundaryReader = (tenant, call, boundary, token) => Custody.CaptureEffectBoundaryAsync(tenant, call, boundary, false, token);
        _inventoryAuthority.AuthorizeOperationAsync(Arg.Any<PlatformKeyVersion>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _inventoryAuthority.ValidateStateAsync(Arg.Any<PlatformKeyVersion>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _inventory.ReadAsync(Arg.Any<PlatformKeyVersion>()).Returns(call => { var key = call.Arg<PlatformKeyVersion>(); return Task.FromResult(new PlatformKeyInventorySnapshot(key.TenantId, key.Purpose, key.KeyAlias, 1, [new(key, PlatformHmacKeyState.Active, 1)], [])); });
        _dapr.GetSecretAsync("digest", "tenant-key", Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(DigestAvailable ? new Dictionary<string, string> { ["value"] = Convert.ToBase64String(Enumerable.Repeat((byte)73, 32).ToArray()) } : new Dictionary<string, string>()));
        Digests = new(_dapr, _inventory, _inventoryAuthority);
        Bindings = new(Custody, Permissions, Source, Digests);
        Authority = new(Custody, Bindings, Source, Permissions, new Dictionary<string, string> { ["tenant-a"] = "registry-1" });
        var actor = new InteractionOccurrenceRegistryActor(ActorHost.CreateForTest<InteractionOccurrenceRegistryActor>(new ActorTestOptions { ActorId = new(InteractionOccurrenceRegistryActor.GetActorId("tenant-a")) }), Authority);
        typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance)!.SetValue(actor, RegistryState);
        Registry = new(actor);
        Keys = new(Custody, Bindings, Registry, Digests, Permissions);
        Manifests = new(Custody, Keys);
        Engine = new(Registry, Keys, Bindings, TimeProvider.System);
    }
    internal ErasureRootOccurrenceEnrollment Enroll(string ownerId = "directory", string reference = "01ARZ3NDEKTSV4RRFFQ69G5FAV", ulong sequence = 1)
    {
        var identity = new InteractionOccurrenceIdentity(new("tenant-a", "interaction-a", "alias-a"), new AggregateIdentity("tenant-a", "conversations", ownerId), PayloadProtectionPayloadKind.Event,
            sequence, "Event.Type", "root-v1", "candidate-hkdf-sha256-v1");
        var binding = new InteractionProtectionBinding(identity, "digest-v1", 1, reference, reference, 1);
        var digest = new CustodyInteractionDigestReference("digest", "tenant-key", "7", "value", new("tenant-a", PlatformKeyPurpose.DigestKey, "intent", "digest-v1", "openbao", "independent", null, null));
        var enrollment = new ErasureRootOccurrenceEnrollment(binding, new("tenant-a", "interaction-a", PlatformKeyPurpose.InteractionRootDek, "alias-a", "root-v1", "lifecycle", "erc-test", "custody-1"),
            "original-interaction-root", 0, digest, "registry-1");
        Source.Enrollments.Add(ErasureRootOccurrenceBindings.Id(identity.Owner, new(identity.Sequence, identity.Kind, identity.PayloadTypeId)), enrollment);
        return enrollment;
    }
    public void Dispose() => _dapr.Dispose();
}
