using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Testing.Fakes;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Custody.Tests;

/// <summary>Actual purpose-bound cryptography/conditional lifecycle/serialized end-state source tests; physical fence, all-copy destruction and restore authorities are synthetic.</summary>
public sealed class CustodyKeyLifecycleTests
{
    /// <summary>Complete original export and interaction-root key context survives unwrap with no raw keys in diagnostics.</summary>
    [Theory]
    [InlineData(PlatformKeyPurpose.ExportEnvelopeKey)][InlineData(PlatformKeyPurpose.InteractionRootDek)]
    public void ExactPurposeBoundWrapAndUnwrapReturnOriginalOwnedKey(PlatformKeyPurpose purpose)
    {
        var identity = CustodyKeyLifecycleFixture.Identity(purpose); byte[] root = Enumerable.Range(0, 32).Select(n => (byte)n).ToArray(), kek = new byte[32];
        var wrapped = CustodyKeyWrapCore.Wrap(identity, "tenant-kek-v1", root, kek); byte[] recovered = new byte[32];
        using var key = CustodyKeyWrapCore.Unwrap(identity, "tenant-kek-v1", wrapped, kek); key.CopyTo(recovered); recovered.ShouldBe(root);
        wrapped.ToString().ShouldBe(nameof(WrappedCustodyKey)); key.ToString().ShouldBe(nameof(OwnedExportKey));
        wrapped.Ciphertext.ShouldNotBe(root);
    }
    /// <summary>Tenant/object/purpose/alias/version/lifecycle/store/contract/KEK substitution fails actual authenticated decryption.</summary>
    [Theory]
    [InlineData("tenant")][InlineData("object")][InlineData("purpose")][InlineData("alias")][InlineData("version")][InlineData("lifecycle")][InlineData("store")][InlineData("contract")][InlineData("kek")][InlineData("cipher")]
    public void CompleteContextSubstitutionCannotUnwrap(string vector)
    {
        var original = CustodyKeyLifecycleFixture.Identity(); var expected = vector switch { "tenant" => original with { TenantId = "tenant-b" }, "object" => original with { ObjectId = "object-b" },
            "purpose" => original with { Purpose = PlatformKeyPurpose.InteractionRootDek }, "alias" => original with { KeyAlias = "alias-b" }, "version" => original with { KeyVersion = "key-v2" },
            "lifecycle" => original with { LifecycleVersion = "other-policy" }, "store" => original with { StoreTarget = "other-store" }, "contract" => original with { ContractVersion = "other-contract" }, _ => original };
        var wrapped = CustodyKeyWrapCore.Wrap(original, "tenant-kek-v1", new byte[32], new byte[32]); if (vector == "cipher") { wrapped.Ciphertext[0] ^= 1; }
        Should.Throw<CryptographicException>(() => CustodyKeyWrapCore.Unwrap(expected, vector == "kek" ? "tenant-kek-v2" : "tenant-kek-v1", wrapped, new byte[32]));
    }
    /// <summary>Confirmed hold blocks destruction until exact approved physical release; irreversible original receipt is immutable through restart/revocation.</summary>
    [Fact]
    public async Task PinUnpinDestroyRetainsExactIrreversibleOriginalOutcome()
    {
        var f = new CustodyKeyLifecycleFixture(); (await f.Actor.RegisterWrappedAsync(CustodyKeyLifecycleFixture.Registration())).Status.ShouldBe(CustodyKeyLifecycleStatus.Wrapped);
        var pin = CustodyKeyLifecycleFixture.Request(CustodyKeyLifecycleAction.Pin, 1, "pin-1");
        (await f.Actor.ApplyAsync(pin)).Status.ShouldBe(CustodyKeyLifecycleStatus.Pinned); f.Persisted.Keys.Single().Pins.Single().ShouldBe(pin);
        (await f.Actor.ApplyAsync(CustodyKeyLifecycleFixture.Request(CustodyKeyLifecycleAction.Destroy, 3, "blocked-destroy"))).Status.ShouldBe(CustodyKeyLifecycleStatus.BlockedByPin); f.Effects.ShouldBe(1);
        var unpin = CustodyKeyLifecycleFixture.Request(CustodyKeyLifecycleAction.Unpin, 3, "unpin-1");
        (await f.Actor.ApplyAsync(unpin)).Status.ShouldBe(CustodyKeyLifecycleStatus.Unpinned); f.Persisted.Keys.Single().Pins.ShouldBeEmpty();
        var destroy = CustodyKeyLifecycleFixture.Request(CustodyKeyLifecycleAction.Destroy, 5, "destroy-1"); var result = await f.Actor.ApplyAsync(destroy);
        result.Status.ShouldBe(CustodyKeyLifecycleStatus.Destroyed); result.RestoreBarrierReceiptId.ShouldNotBeNull(); f.PhysicalOutcomes.Count.ShouldBe(3);
        f.Authority.AuthorizeEffectAsync(Arg.Any<CustodyKeyLifecycleRequest>(), Arg.Any<CancellationToken>()).Returns(false);
        var saved = f.Backend.CommittedState.Single(); var backend = new InMemoryStateManager();
        await backend.SetStateAsync(saved.Key, JsonSerializer.Deserialize<CustodyKeyLifecycleLedger>(JsonSerializer.Serialize(saved.Value))!, TestContext.Current.CancellationToken); await backend.SaveStateAsync(TestContext.Current.CancellationToken);
        (await CustodyKeyLifecycleFixture.Create(backend, f.Authority, f).LookupAsync(destroy)).ShouldBe(result); f.Effects.ShouldBe(3);
        JsonSerializer.Serialize(backend.CommittedState.Single().Value).ShouldNotContain("Plaintext");
    }
    /// <summary>Physical lost acknowledgement/unknown lookup retains original reservation and blocks new effects until exact persisted outcome recovery.</summary>
    [Fact]
    public async Task UnknownOriginalEffectRequiresLookupAndCannotRepeat()
    {
        var f = new CustodyKeyLifecycleFixture { LoseResponse = true, UnknownLookup = true }; await f.Actor.RegisterWrappedAsync(CustodyKeyLifecycleFixture.Registration());
        var pin = CustodyKeyLifecycleFixture.Request(CustodyKeyLifecycleAction.Pin, 1, "original-pin");
        (await f.Actor.ApplyAsync(pin)).Status.ShouldBe(CustodyKeyLifecycleStatus.Unknown);
        (await f.Actor.ApplyAsync(pin)).Status.ShouldBe(CustodyKeyLifecycleStatus.Unknown);
        (await f.Actor.ApplyAsync(CustodyKeyLifecycleFixture.Request(CustodyKeyLifecycleAction.Destroy, 2, "new-destroy"))).Status.ShouldBe(CustodyKeyLifecycleStatus.Unavailable);
        f.Persisted.Keys.Single().Pins.Count.ShouldBe(1); f.Effects.ShouldBe(1);
        f.UnknownLookup = false; (await f.Actor.LookupAsync(pin)).Status.ShouldBe(CustodyKeyLifecycleStatus.Pinned);
        f.Persisted.Keys.Single().Outcomes.Single().Status.ShouldBe(CustodyKeyLifecycleStatus.Pinned); f.Effects.ShouldBe(1);
    }
    /// <summary>No destruction proof exists without independent all-copy/nonrollback receipt; interaction roots additionally require exact protection-owner reservation.</summary>
    [Fact]
    public async Task MissingRestoreOrProtectionReservationCannotClaimDestruction()
    {
        var identity = CustodyKeyLifecycleFixture.Identity(PlatformKeyPurpose.InteractionRootDek); var f = new CustodyKeyLifecycleFixture { OmitRestoreProof = true };
        await f.Actor.RegisterWrappedAsync(CustodyKeyLifecycleFixture.Registration(identity)); var request = CustodyKeyLifecycleFixture.Request(CustodyKeyLifecycleAction.Destroy, 1, identity: identity);
        await Should.ThrowAsync<ArgumentException>(() => f.Actor.ApplyAsync(request with { ProtectionReservationReceiptId = null })); f.Effects.ShouldBe(0);
        (await f.Actor.ApplyAsync(request)).Status.ShouldBe(CustodyKeyLifecycleStatus.Unknown); f.Persisted.Keys.Single().Outcomes.Single().RestoreBarrierReceiptId.ShouldBeNull();
        (await f.Actor.LookupAsync(request)).Status.ShouldBe(CustodyKeyLifecycleStatus.Unknown); f.Effects.ShouldBe(1);
    }
    /// <summary>Private current lookup remains required independently of immutable physical effect authority.</summary>
    [Fact]
    public async Task MissingOrWithdrawnPrivateCredentialReleasesNoTerminalOutcome()
    {
        var f = new CustodyKeyLifecycleFixture(); var registration = CustodyKeyLifecycleFixture.Registration();
        (await CustodyKeyLifecycleFixture.Create(f.Backend).RegisterWrappedAsync(registration)).Status.ShouldBe(CustodyKeyLifecycleStatus.Unavailable); f.Backend.CommittedState.ShouldBeEmpty();
        await f.Actor.RegisterWrappedAsync(registration); var request = CustodyKeyLifecycleFixture.Request(CustodyKeyLifecycleAction.Destroy, 1); var original = await f.Actor.ApplyAsync(request);
        f.Authority.AuthorizeOperationAsync(request.Identity, request.OperationId, "LookupKeyLifecycle", Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        (await f.Actor.LookupAsync(request)).Status.ShouldBe(CustodyKeyLifecycleStatus.Unavailable); f.Persisted.Keys.Single().Outcomes.Single().ShouldBe(original);
    }
    /// <summary>Changed phase/target/root identity cannot register a replacement or substitute original lifecycle intent.</summary>
    [Fact]
    public async Task ChangedOriginalWrapOrOperationConflictsWithoutEffect()
    {
        var f = new CustodyKeyLifecycleFixture(); var registration = CustodyKeyLifecycleFixture.Registration(); await f.Actor.RegisterWrappedAsync(registration);
        (await f.Actor.RegisterWrappedAsync(registration with { Identity = registration.Identity with { LifecycleVersion = "changed-policy" } })).Status.ShouldBe(CustodyKeyLifecycleStatus.Conflict);
        var request = CustodyKeyLifecycleFixture.Request(CustodyKeyLifecycleAction.Pin, 1); await f.Actor.ApplyAsync(request);
        (await f.Actor.ApplyAsync(request with { DecisionVersion = "changed-decision" })).Status.ShouldBe(CustodyKeyLifecycleStatus.Conflict); f.Effects.ShouldBe(1); f.Persisted.Keys.Count.ShouldBe(1);
    }
}
