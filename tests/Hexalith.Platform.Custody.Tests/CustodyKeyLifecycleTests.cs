using Hexalith.EventStore.Contracts.Security;
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
    /// <summary>A separately authorized mutation recovers the independently admitted exact stage after restart without the original caller; read-only evidence cannot advance it and physical effects are not duplicated.</summary>
    [Fact]
    public async Task LaterMutationRecoversPreJournalOriginalWithoutItsCaller()
    {
        var f = new CustodyKeyLifecycleFixture(); var original = CustodyKeyLifecycleFixture.Registration();
        AnchoredFixtureJournal.SetAvailable(f.Authority, false);
        (await f.Actor.RegisterWrappedAsync(original)).Status.ShouldBe(CustodyKeyLifecycleStatus.Unavailable);
        var pending = f.Backend.CommittedState.Single().Value.ShouldBeOfType<Hexalith.EventStore.Contracts.Security.AnchoredStateTransition>();
        f.Authority.ClearReceivedCalls(); var restarted = CustodyKeyLifecycleFixture.Create(f.Backend, f.Authority, f);
        (await restarted.LookupAsync(CustodyKeyLifecycleFixture.Request(CustodyKeyLifecycleAction.Pin, 1, "later-pin"))).Status.ShouldBe(CustodyKeyLifecycleStatus.Unavailable);
        await f.Authority.DidNotReceive().RecordTransitionAsync(Arg.Any<Hexalith.EventStore.Contracts.Security.AnchoredStateTransition>(), Arg.Any<CancellationToken>());
        f.Anchor.ShouldBe(0); f.Effects.ShouldBe(0); f.Backend.CommittedState.Single().Value.ShouldBe(pending);
        AnchoredFixtureJournal.SetAvailable(f.Authority, true);
        (await restarted.ApplyAsync(CustodyKeyLifecycleFixture.Request(CustodyKeyLifecycleAction.Pin, 1, "later-pin"))).Status.ShouldBe(CustodyKeyLifecycleStatus.Pinned);
        f.Persisted.Keys.Single().Registration.ShouldBe(original); f.Persisted.Keys.Single().Pins.Single().OperationId.ShouldBe("later-pin");
        f.Persisted.Revision.ShouldBe(3); f.Effects.ShouldBe(1);
    }

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
    /// <summary>At either existing technical bound no next item mutates state/anchor or invokes a physical provider, and exact original terminal evidence remains readable.</summary>
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task LifecycleCollectionBoundPreservesOriginalTerminalOutcome(bool operationBound)
    {
        var f = new CustodyKeyLifecycleFixture(); await f.Actor.RegisterWrappedAsync(CustodyKeyLifecycleFixture.Registration());
        var pin = CustodyKeyLifecycleFixture.Request(CustodyKeyLifecycleAction.Pin, 1, "original-pin"); var original = await f.Actor.ApplyAsync(pin);
        var state = f.Persisted; var keys = state.Keys.ToList();
        for (int n = 1; n < 1000; n++)
        {
            var identity = CustodyKeyLifecycleFixture.Identity() with { ObjectId = "capacity-object-" + n };
            var registration = CustodyKeyLifecycleFixture.Registration(identity) with { OperationId = "capacity-wrap-" + n };
            var requests = operationBound ? Enumerable.Range(0, 10).Select(i => CustodyKeyLifecycleFixture.Request(CustodyKeyLifecycleAction.Destroy, 1, "capacity-op-" + i, identity)).ToArray() : [];
            keys.Add(new(registration, [], requests, requests.Select(req => new CustodyKeyLifecycleOutcome(identity, req.OperationId, CustodyKeyLifecycleFixture.Digest(req), CustodyKeyLifecycleStatus.NotPerformed, "independent-never-performed-" + req.OperationId)).ToArray()));
        }
        if (operationBound)
        {
            var prior = keys[0]; var extra = Enumerable.Range(1, 9).Select(n => CustodyKeyLifecycleFixture.Request(CustodyKeyLifecycleAction.Destroy, 1, "prior-negative-" + n)).ToArray();
            keys[0] = prior with { Requests = prior.Requests.Concat(extra).ToArray(), Outcomes = prior.Outcomes.Concat(extra.Select(req => new CustodyKeyLifecycleOutcome(req.Identity, req.OperationId, CustodyKeyLifecycleFixture.Digest(req), CustodyKeyLifecycleStatus.NotPerformed, "original-negative-" + req.OperationId))).ToArray() };
        }
        state = state with { Revision = 11002, Keys = keys.ToArray() }; var saved = f.Backend.CommittedState.Single();
        await f.Backend.SetStateAsync(saved.Key, state, TestContext.Current.CancellationToken); await f.Backend.SaveStateAsync(TestContext.Current.CancellationToken);
        f.Anchor = state.Revision; f.AnchorDigest = CustodyKeyLifecycleFixture.Digest(state); string before = f.AnchorDigest;
        if (operationBound) { (await f.Actor.ApplyAsync(CustodyKeyLifecycleFixture.Request(CustodyKeyLifecycleAction.Unpin, state.Revision, "next-unpin"))).Status.ShouldBe(CustodyKeyLifecycleStatus.Unavailable); }
        else { (await f.Actor.RegisterWrappedAsync(CustodyKeyLifecycleFixture.Registration(CustodyKeyLifecycleFixture.Identity() with { ObjectId = "one-too-many" }) with { ExpectedRevision = state.Revision })).Status.ShouldBe(CustodyKeyLifecycleStatus.Unavailable); }
        CustodyKeyLifecycleFixture.Digest(f.Persisted).ShouldBe(before); f.AnchorDigest.ShouldBe(before); (await f.Actor.LookupAsync(pin)).ShouldBe(original); f.Effects.ShouldBe(1);
    }
    /// <summary>Failed/lost acknowledgement of a reservation never sends a physical effect from staged cache; precommit anchor divergence remains unavailable.</summary>
    [Theory]
    [InlineData(1, false)][InlineData(1, true)][InlineData(2, false)][InlineData(2, true)]
    public async Task ReservationSaveFailureNeverUsesStagedStateForEffect(int failSave, bool committed)
    {
        var f = new CustodyKeyLifecycleFixture(); await f.Actor.RegisterWrappedAsync(CustodyKeyLifecycleFixture.Registration());
        var request = CustodyKeyLifecycleFixture.Request(CustodyKeyLifecycleAction.Destroy, 1);
        var manager = ActorPendingFixture.Faulting<CustodyKeyLifecycleLedger>(f.Backend, failSave, committed);
        await Should.ThrowAsync<HttpRequestException>(() => CustodyKeyLifecycleFixture.Create(manager, f.Authority, f).ApplyAsync(request));
        f.Effects.ShouldBe(0); f.Anchor.ShouldBe(failSave == 1 ? 1 : 2); await f.Backend.ClearCacheAsync(TestContext.Current.CancellationToken);
        var restarted = CustodyKeyLifecycleFixture.Create(f.Backend, f.Authority, f);
        var recovered = await restarted.ApplyAsync(request);
        if (failSave == 1 && !committed) { recovered.Status.ShouldBe(CustodyKeyLifecycleStatus.Destroyed); f.Effects.ShouldBe(1); }
        else { recovered.Status.ShouldBe(CustodyKeyLifecycleStatus.Unknown); f.Effects.ShouldBe(0); }
        (await restarted.LookupAsync(request)).Status.ShouldBe(recovered.Status);
        f.Persisted.Revision.ShouldBe(failSave == 1 && !committed ? 3 : 2);
    }


    /// <summary>Exact independently proved negative pin removes its provisional hold; negative unpin retains its existing hold through restart and later destruction.</summary>
    [Theory]
    [InlineData(true)][InlineData(false)]
    public async Task NegativePhysicalPinAndUnpinRetainTheCorrectRestrictiveHold(bool negativePin)
    {
        var f = new CustodyKeyLifecycleFixture { NotPerformedPin = negativePin, NotPerformedUnpin = !negativePin, LoseResponse = true };
        await f.Actor.RegisterWrappedAsync(CustodyKeyLifecycleFixture.Registration());
        var pin = CustodyKeyLifecycleFixture.Request(CustodyKeyLifecycleAction.Pin, 1, "pin-negative-schedule");
        (await f.Actor.ApplyAsync(pin)).Status.ShouldBe(CustodyKeyLifecycleStatus.Unknown);
        f.Persisted.Keys.Single().Pins.Count.ShouldBe(1);
        (await f.Actor.LookupAsync(pin)).Status.ShouldBe(negativePin ? CustodyKeyLifecycleStatus.NotPerformed : CustodyKeyLifecycleStatus.Pinned);
        CustodyKeyLifecycleRequest terminal = pin;
        if (!negativePin)
        {
            terminal = CustodyKeyLifecycleFixture.Request(CustodyKeyLifecycleAction.Unpin, 3, "unpin-negative-schedule");
            (await f.Actor.ApplyAsync(terminal)).Status.ShouldBe(CustodyKeyLifecycleStatus.Unknown);
            (await f.Actor.LookupAsync(terminal)).Status.ShouldBe(CustodyKeyLifecycleStatus.NotPerformed);
        }
        var saved = f.Backend.CommittedState.Single(); var restored = new InMemoryStateManager();
        await restored.SetStateAsync(saved.Key, JsonSerializer.Deserialize<CustodyKeyLifecycleLedger>(JsonSerializer.Serialize(saved.Value))!, TestContext.Current.CancellationToken); await restored.SaveStateAsync(TestContext.Current.CancellationToken);
        var restarted = CustodyKeyLifecycleFixture.Create(restored, f.Authority, f);
        (await restarted.LookupAsync(terminal)).Status.ShouldBe(CustodyKeyLifecycleStatus.NotPerformed);
        var persisted = restored.CommittedState.Single().Value.ShouldBeOfType<CustodyKeyLifecycleLedger>(); persisted.Keys.Single().Pins.Count.ShouldBe(negativePin ? 0 : 1);
        f.LoseResponse = false; var destroy = CustodyKeyLifecycleFixture.Request(CustodyKeyLifecycleAction.Destroy, persisted.Revision, "destroy-after-negative");
        // Use the original backend for the physical fixture's assertion; both exact bytes were transported above.
        var result = await f.Actor.ApplyAsync(destroy); result.Status.ShouldBe(negativePin ? CustodyKeyLifecycleStatus.Destroyed : CustodyKeyLifecycleStatus.BlockedByPin);
        f.Effects.ShouldBe(2);
    }
    /// <summary>Well-formed wrapping and physical effect receipts require separate independent original proof; rejected effects remain Unknown and restrictive after transport/restart.</summary>
    [Theory]
    [InlineData("wrap")][InlineData("pin")][InlineData("unpin")][InlineData("destroy")]
    public async Task IndependentReceiptDenialCannotReleaseWellFormedCustodyEvidence(string vector)
    {
        var f = new CustodyKeyLifecycleFixture { RejectRegistration = vector == "wrap" };
        var before = f.AnchorDigest; var registration = CustodyKeyLifecycleFixture.Registration();
        var wrapped = await f.Actor.RegisterWrappedAsync(registration);
        if (vector == "wrap") { wrapped.Status.ShouldBe(CustodyKeyLifecycleStatus.Unavailable); f.Backend.CommittedState.ShouldBeEmpty(); f.Anchor.ShouldBe(0); f.AnchorDigest.ShouldBe(before); f.Effects.ShouldBe(0); return; }
        if (vector == "unpin") { await f.Actor.ApplyAsync(CustodyKeyLifecycleFixture.Request(CustodyKeyLifecycleAction.Pin, 1, "confirmed-pin")); }
        f.RejectOutcome = true;
        var action = vector == "pin" ? CustodyKeyLifecycleAction.Pin : vector == "unpin" ? CustodyKeyLifecycleAction.Unpin : CustodyKeyLifecycleAction.Destroy;
        var request = CustodyKeyLifecycleFixture.Request(action, f.Persisted.Revision, "independently-rejected-effect");
        (await f.Actor.ApplyAsync(request)).Status.ShouldBe(CustodyKeyLifecycleStatus.Unknown); int effects = f.Effects;
        var saved = f.Backend.CommittedState.Single(); var restored = new InMemoryStateManager();
        await restored.SetStateAsync(saved.Key, JsonSerializer.Deserialize<CustodyKeyLifecycleLedger>(JsonSerializer.Serialize(saved.Value))!, TestContext.Current.CancellationToken); await restored.SaveStateAsync(TestContext.Current.CancellationToken);
        var restarted = CustodyKeyLifecycleFixture.Create(restored, f.Authority, f);
        (await restarted.LookupAsync(request)).Status.ShouldBe(CustodyKeyLifecycleStatus.Unknown);
        (await restarted.ApplyAsync(CustodyKeyLifecycleFixture.Request(CustodyKeyLifecycleAction.Destroy, f.Persisted.Revision, "cannot-follow-rejected-effect"))).Status.ShouldBe(CustodyKeyLifecycleStatus.Unavailable);
        restored.CommittedState.Single().Value.ShouldBeOfType<CustodyKeyLifecycleLedger>().Keys.Single().Outcomes.Last().Status.ShouldBe(CustodyKeyLifecycleStatus.Unknown);
        f.Effects.ShouldBe(effects); f.Persisted.Keys.Single().Pins.Count.ShouldBe(vector == "destroy" ? 0 : 1);
    }

    /// <summary>Negative original pin recovery removes only its own provisional reservation, preserving a distinct confirmed hold after serialized restart and blocking destruction.</summary>
    [Fact]
    public async Task NegativePinPreservesSeparateConfirmedOriginalHold()
    {
        var f = new CustodyKeyLifecycleFixture(); await f.Actor.RegisterWrappedAsync(CustodyKeyLifecycleFixture.Registration());
        var original = CustodyKeyLifecycleFixture.Request(CustodyKeyLifecycleAction.Pin, 1, "confirmed-original-pin") with { HoldId = "independent-existing-hold" };
        var originalOutcome = await f.Actor.ApplyAsync(original); originalOutcome.Status.ShouldBe(CustodyKeyLifecycleStatus.Pinned);
        f.NotPerformedPin = true; f.LoseResponse = true;
        var negative = CustodyKeyLifecycleFixture.Request(CustodyKeyLifecycleAction.Pin, 3, "distinct-negative-pin") with { HoldId = "distinct-new-hold" };
        (await f.Actor.ApplyAsync(negative)).Status.ShouldBe(CustodyKeyLifecycleStatus.Unknown); f.Persisted.Keys.Single().Pins.Count.ShouldBe(2);
        (await f.Actor.LookupAsync(negative)).Status.ShouldBe(CustodyKeyLifecycleStatus.NotPerformed); f.Persisted.Keys.Single().Pins.Single().ShouldBe(original);
        var saved = f.Backend.CommittedState.Single(); var restored = new InMemoryStateManager();
        await restored.SetStateAsync(saved.Key, JsonSerializer.Deserialize<CustodyKeyLifecycleLedger>(JsonSerializer.Serialize(saved.Value))!, TestContext.Current.CancellationToken); await restored.SaveStateAsync(TestContext.Current.CancellationToken);
        var restarted = CustodyKeyLifecycleFixture.Create(restored, f.Authority, f);
        (await restarted.LookupAsync(original)).ShouldBe(originalOutcome); (await restarted.LookupAsync(negative)).Status.ShouldBe(CustodyKeyLifecycleStatus.NotPerformed);
        (await restarted.ApplyAsync(CustodyKeyLifecycleFixture.Request(CustodyKeyLifecycleAction.Destroy, 5, "blocked-after-negative-pin"))).Status.ShouldBe(CustodyKeyLifecycleStatus.BlockedByPin);
        restored.CommittedState.Single().Value.ShouldBeOfType<CustodyKeyLifecycleLedger>().Keys.Single().Pins.Single().ShouldBe(original); f.Effects.ShouldBe(2);
    }

    /// <summary>Suspended physical invocation/task releases the modeled non-reentrant tenant turn at the original deadline; late results never persist and exact serialized lookup recovers one effect.</summary>
    [Theory]
    [InlineData(false, false)][InlineData(false, true)][InlineData(true, false)][InlineData(true, true)]
    public async Task SuspendedPhysicalLifecycleReleasesTurnAndRecoversOnlyOriginal(bool lookup, bool invocation)
    {
        var (clock, advance) = PrivateOwnerDeadlineTestClock.Create(); var f = new CustodyKeyLifecycleFixture();
        await f.Actor.RegisterWrappedAsync(CustodyKeyLifecycleFixture.Registration());
        var request = CustodyKeyLifecycleFixture.Request(CustodyKeyLifecycleAction.Pin, 1, "bounded-original-pin");
        if (lookup) { f.LoseResponse = true; (await f.Actor.ApplyAsync(request)).Status.ShouldBe(CustodyKeyLifecycleStatus.Unknown); }
        var provider = Substitute.For<ICustodyKeyLifecycleProvider>();
        using var release = new ManualResetEventSlim(); using var turn = new SemaphoreSlim(1);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var returned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<CustodyKeyLifecycleOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        CustodyKeyLifecycleOutcome? physical = null;
        Task<CustodyKeyLifecycleOutcome> Suspend(CustodyKeyLifecycleOutcome result)
        {
            physical = result; entered.TrySetResult();
            if (!invocation) { return pending.Task; }
            release.Wait(); returned.TrySetResult(); return Task.FromResult(result);
        }
        provider.ExecuteAsync(Arg.Any<CustodyKeyRegistration>(), Arg.Any<CustodyKeyLifecycleRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            f.LoseResponse = false;
            return Suspend(f.ExecuteAsync(call.Arg<CustodyKeyRegistration>(), call.Arg<CustodyKeyLifecycleRequest>(), call.Arg<string>(), call.Arg<CancellationToken>()).GetAwaiter().GetResult());
        });
        provider.LookupAsync(Arg.Any<CustodyKeyRegistration>(), Arg.Any<CustodyKeyLifecycleRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
            Suspend(f.LookupAsync(call.Arg<CustodyKeyRegistration>(), call.Arg<CustodyKeyLifecycleRequest>(), call.Arg<string>(), call.Arg<CancellationToken>()).GetAwaiter().GetResult()));
        var actor = CustodyKeyLifecycleFixture.Create(f.Backend, f.Authority, provider, clock);
        async Task<T> Turn<T>(Func<Task<T>> operation)
        { await turn.WaitAsync(TestContext.Current.CancellationToken); try { return await operation(); } finally { turn.Release(); } }
        var waiting = Task.Run(() => Turn(() => lookup ? actor.LookupAsync(request) : actor.ApplyAsync(request)), TestContext.Current.CancellationToken);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            advance(TimeSpan.FromSeconds(30));
            (await waiting.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyKeyLifecycleStatus.Unknown);
            f.Persisted.Keys.Single().Pins.Single().ShouldBe(request); f.Effects.ShouldBe(1);
            (await Turn(() => actor.ApplyAsync(CustodyKeyLifecycleFixture.Request(CustodyKeyLifecycleAction.Destroy, f.Persisted.Revision, "same-key-blocked")))
                .WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyKeyLifecycleStatus.Unavailable);
            var other = CustodyKeyLifecycleFixture.Registration(CustodyKeyLifecycleFixture.Identity() with { ObjectId = "other-key" })
                with { OperationId = "other-wrap", ExpectedRevision = f.Persisted.Revision };
            (await Turn(() => actor.RegisterWrappedAsync(other)).WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyKeyLifecycleStatus.Wrapped);
            string before = JsonSerializer.Serialize(f.Persisted);
            if (invocation) { release.Set(); await returned.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); }
            else { pending.TrySetResult(physical!); }
            JsonSerializer.Serialize(f.Persisted).ShouldBe(before); f.Persisted.Keys.First().Outcomes.Single().Status.ShouldBe(CustodyKeyLifecycleStatus.Unknown);
            var saved = f.Backend.CommittedState.Single(); var restored = new InMemoryStateManager();
            await restored.SetStateAsync(saved.Key, JsonSerializer.Deserialize<CustodyKeyLifecycleLedger>(before)!, TestContext.Current.CancellationToken);
            await restored.SaveStateAsync(TestContext.Current.CancellationToken);
            var restarted = CustodyKeyLifecycleFixture.Create(restored, f.Authority, f, clock);
            var original = await restarted.LookupAsync(request); original.ShouldBe(physical); original.Status.ShouldBe(CustodyKeyLifecycleStatus.Pinned);
            (await restarted.ApplyAsync(request)).ShouldBe(original); f.Effects.ShouldBe(1);
            var final = restored.CommittedState.Single().Value.ShouldBeOfType<CustodyKeyLifecycleLedger>().Keys.Single(k => k.Registration.Identity == request.Identity);
            final.Requests.Single().ShouldBe(request); final.Pins.Single().ShouldBe(request); final.Outcomes.Single().ShouldBe(original);
        }
        finally { release.Set(); pending.TrySetResult(physical!); }
    }
}
