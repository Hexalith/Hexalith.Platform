using System.Text.Json;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.EventStore.Testing.Fakes;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>Actual durable candidate uniqueness/retry/completion/restore source tests; ciphertext/writer/independent anchor ports are synthetic.</summary>
public sealed class InteractionOccurrenceRegistryActorTests
{
    /// <summary>A different authorized mutation resolves the independently admitted staged original after restart, while read-only lookup cannot advance it or repeat a physical effect.</summary>
    [Fact]
    public async Task LaterMutationRecoversPreJournalOriginalWithoutItsCaller()
    {
        var f = new InteractionOccurrenceFixture { JournalAvailable = false }; var original = InteractionOccurrenceFixture.Request();
        await Should.ThrowAsync<InvalidOperationException>(() => f.Actor.ReserveAsync(original));
        var retained = System.Text.Json.JsonSerializer.Serialize(f.Backend.CommittedState.Single().Value.ShouldBeOfType<AnchoredStateTransition>());
        f.Authority.ClearReceivedCalls(); var restarted = InteractionOccurrenceFixture.Create(f.Backend, f.Authority);
        (await restarted.LookupAsync(original.Identity)).Status.ShouldBe(InteractionOccurrenceReservationStatus.Unavailable);
        await f.Authority.DidNotReceive().RecordTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>());
        f.Head.ShouldBe(0); System.Text.Json.JsonSerializer.Serialize(f.Backend.CommittedState.Single().Value).ShouldBe(retained);
        f.JournalAvailable = true;
        (await restarted.ReserveAsync(InteractionOccurrenceFixture.Request(2, "01ARZ3NDEKTSV4RRFFQ69G5FAW"))).Status.ShouldBe(InteractionOccurrenceReservationStatus.Reserved);
        var saved = f.Backend.CommittedState.Single().Value.ShouldBeOfType<InteractionOccurrenceRegistrySnapshot>();
        saved.Revision.ShouldBe(2); saved.Records.Count.ShouldBe(2); saved.Records[0].Request.ShouldBe(original);
        (await restarted.LookupAsync(original.Identity)).Record!.RegistryRevision.ShouldBe(1);
    }

    /// <summary>Reservation commits before any sealed result; same exact retry keeps the original reference and sealed bytes across restart.</summary>
    [Fact]
    public async Task DurableUniqueReservationAndOriginalSealedRetrySurviveRestart()
    {
        var f = new InteractionOccurrenceFixture(); var request = InteractionOccurrenceFixture.Request(); var reserved = await f.Actor.ReserveAsync(request);
        reserved.Status.ShouldBe(InteractionOccurrenceReservationStatus.Reserved); f.Backend.CommittedState.Single().Value.ShouldBeOfType<InteractionOccurrenceRegistrySnapshot>().Records.Single().Sealed.ShouldBeNull();
        var sealedResult = InteractionOccurrenceFixture.Sealed(); var retained = await f.Actor.RetainSealedAsync(request.Identity, sealedResult);
        sealedResult.PayloadBytes[0] = 0; retained.Record!.Sealed!.PayloadBytes[0].ShouldBe((byte)'{');
        var retry = await f.Actor.ReserveAsync(request with { ProposedKeyReference = "01ARZ3NDEKTSV4RRFFQ69G5FAW" }); retry.Record!.KeyReference.ShouldBe(request.ProposedKeyReference); retry.Record.Sealed!.PayloadBytes.ShouldBe(retained.Record.Sealed!.PayloadBytes);
        var saved = f.Backend.CommittedState.Single(); var restored = new InMemoryStateManager(); await restored.SetStateAsync(saved.Key,
            JsonSerializer.Deserialize<InteractionOccurrenceRegistrySnapshot>(JsonSerializer.Serialize(saved.Value))!, TestContext.Current.CancellationToken); await restored.SaveStateAsync(TestContext.Current.CancellationToken);
        var lookup = await InteractionOccurrenceFixture.Create(restored, f.Authority).LookupAsync(request.Identity); lookup.Record!.KeyReference.ShouldBe(request.ProposedKeyReference); lookup.Record.Sealed!.PayloadBytes.ShouldBe(retained.Record.Sealed.PayloadBytes);
        JsonSerializer.Serialize(saved.Value).ShouldNotContain("synthetic-intent"); JsonSerializer.Serialize(saved.Value).ShouldNotContain("DataEncryptionKey");
    }
    /// <summary>Unknown completion remains unreadable; only exact writer proof activates, and changed evidence/key reference cannot reclassify it.</summary>
    [Fact]
    public async Task WriterProofIsRequiredAndBoundToOriginalReference()
    {
        var f = new InteractionOccurrenceFixture(); var request = InteractionOccurrenceFixture.Request(); await f.Actor.ReserveAsync(request); await f.Actor.RetainSealedAsync(request.Identity, InteractionOccurrenceFixture.Sealed());
        f.Authority.VerifyWriterAsync(Arg.Any<InteractionOccurrenceRecord>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(false);
        (await f.Actor.CompleteWriterAsync(request.Identity, request.ProposedKeyReference, "unknown", true)).Status.ShouldBe(InteractionOccurrenceReservationStatus.Unavailable);
        (await f.Actor.LookupAsync(request.Identity)).Record!.WriterState.ShouldBe(InteractionOccurrenceWriterState.SealedPending);
        f.Authority.VerifyWriterAsync(Arg.Any<InteractionOccurrenceRecord>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(true);
        (await f.Actor.CompleteWriterAsync(request.Identity, "01ARZ3NDEKTSV4RRFFQ69G5FAW", "source-proof", true)).Status.ShouldBe(InteractionOccurrenceReservationStatus.Unavailable);
        var active = await f.Actor.CompleteWriterAsync(request.Identity, request.ProposedKeyReference, "source-proof", true); active.Record!.WriterState.ShouldBe(InteractionOccurrenceWriterState.Active);
        (await f.Actor.CompleteWriterAsync(request.Identity, request.ProposedKeyReference, "changed", false)).Status.ShouldBe(InteractionOccurrenceReservationStatus.Conflict);
    }
    /// <summary>Complete reference namespace rejects collision even across aliases; changed content/DigestKey/root version cannot reuse a source occurrence.</summary>
    [Fact]
    public async Task CollisionAndChangedIntentCannotAuthorizeEncryption()
    {
        var f = new InteractionOccurrenceFixture(); var request = InteractionOccurrenceFixture.Request(); await f.Actor.ReserveAsync(request);
        (await f.Actor.ReserveAsync(InteractionOccurrenceFixture.Request(2))).Status.ShouldBe(InteractionOccurrenceReservationStatus.ReferenceCollision);
        (await f.Actor.ReserveAsync(request with { ContentIntentHmac = new string('B', 64) })).Status.ShouldBe(InteractionOccurrenceReservationStatus.Conflict);
        (await f.Actor.ReserveAsync(request with { DigestKeyVersion = "new-digest-v2" })).Status.ShouldBe(InteractionOccurrenceReservationStatus.Conflict);
        (await f.Actor.ReserveAsync(request with { Identity = request.Identity with { RootKeyVersion = "new-root-v2" } })).Status.ShouldBe(InteractionOccurrenceReservationStatus.Conflict);
        f.Backend.CommittedState.Single().Value.ShouldBeOfType<InteractionOccurrenceRegistrySnapshot>().Records.Count.ShouldBe(1);
    }
    /// <summary>Authoritative never-written proof permits only the next attempt with another globally unique reference; abandoned references remain used.</summary>
    [Fact]
    public async Task AbortedAttemptNeverReusesReferenceAndRequiresGapFreeSuccessor()
    {
        var f = new InteractionOccurrenceFixture(); var request = InteractionOccurrenceFixture.Request(); await f.Actor.ReserveAsync(request);
        await f.Actor.CompleteWriterAsync(request.Identity, request.ProposedKeyReference, "authoritative-never-written", false);
        (await f.Actor.ReserveAsync(request)).Status.ShouldBe(InteractionOccurrenceReservationStatus.Unavailable);
        (await f.Actor.ReserveAsync(request with { ReservationAttemptOrdinal = 3, ProposedKeyReference = "01ARZ3NDEKTSV4RRFFQ69G5FAW" })).Status.ShouldBe(InteractionOccurrenceReservationStatus.Conflict);
        (await f.Actor.ReserveAsync(request with { ReservationAttemptOrdinal = 2 })).Status.ShouldBe(InteractionOccurrenceReservationStatus.ReferenceCollision);
        var next = request with { ReservationAttemptOrdinal = 2, ProposedKeyReference = "01ARZ3NDEKTSV4RRFFQ69G5FAW" };
        (await f.Actor.ReserveAsync(next)).Record!.KeyReference.ShouldBe(next.ProposedKeyReference);
        f.Backend.CommittedState.Single().Value.ShouldBeOfType<InteractionOccurrenceRegistrySnapshot>().Records.Count.ShouldBe(2);
    }
    /// <summary>Older durable rollback and changed installed epoch deny original lookup/new reservations; no reference/key can be revived.</summary>
    [Fact]
    public async Task IndependentMonotonicAnchorDeniesRollbackAndRestoredEpoch()
    {
        var f = new InteractionOccurrenceFixture(); var request = InteractionOccurrenceFixture.Request(); await f.Actor.ReserveAsync(request); var old = f.Backend.CommittedState.Single();
        byte[] oldBytes = JsonSerializer.SerializeToUtf8Bytes(old.Value); await f.Actor.RetainSealedAsync(request.Identity, InteractionOccurrenceFixture.Sealed());
        var restored = new InMemoryStateManager(); await restored.SetStateAsync(old.Key, JsonSerializer.Deserialize<InteractionOccurrenceRegistrySnapshot>(oldBytes)!, TestContext.Current.CancellationToken); await restored.SaveStateAsync(TestContext.Current.CancellationToken);
        var actor = InteractionOccurrenceFixture.Create(restored, f.Authority); (await actor.LookupAsync(request.Identity)).Status.ShouldBe(InteractionOccurrenceReservationStatus.Unavailable);
        (await actor.ReserveAsync(InteractionOccurrenceFixture.Request(2, "01ARZ3NDEKTSV4RRFFQ69G5FAW"))).Status.ShouldBe(InteractionOccurrenceReservationStatus.Unavailable);
        f.Epoch = "epoch-2"; (await f.Actor.LookupAsync(request.Identity)).Status.ShouldBe(InteractionOccurrenceReservationStatus.Unavailable);
        f.Backend.CommittedState.Single().Value.ShouldBeOfType<InteractionOccurrenceRegistrySnapshot>().EpochId.ShouldBe("epoch-1");
    }
    /// <summary>Missing/withdrawn exact current caller/root/writer authority never exposes metadata or authorizes a reserved reference.</summary>
    [Fact]
    public async Task MissingOrWithdrawnAuthorityFailsClosed()
    {
        var f = new InteractionOccurrenceFixture(); var request = InteractionOccurrenceFixture.Request();
        (await InteractionOccurrenceFixture.Create(f.Backend).ReserveAsync(request)).Status.ShouldBe(InteractionOccurrenceReservationStatus.Unavailable); f.Backend.CommittedState.ShouldBeEmpty();
        await f.Actor.ReserveAsync(request); f.Authority.AuthorizeOperationAsync(Arg.Any<InteractionOccurrenceIdentity>(), "Lookup", Arg.Any<CancellationToken>()).Returns(false);
        (await f.Actor.LookupAsync(request.Identity)).Record.ShouldBeNull();
        f.Authority.AuthorizeReservationAsync(Arg.Any<InteractionOccurrenceRequest>(), Arg.Any<CancellationToken>()).Returns(false);
        (await f.Actor.ReserveAsync(InteractionOccurrenceFixture.Request(2, "01ARZ3NDEKTSV4RRFFQ69G5FAW"))).Status.ShouldBe(InteractionOccurrenceReservationStatus.Unavailable);
    }
    /// <summary>Precommit save failure advances the independent anchor conservatively and closes availability; committed lost ack reuses the original reference only.</summary>
    [Theory]
    [InlineData(1, false)][InlineData(1, true)][InlineData(2, false)][InlineData(2, true)]
    public async Task FailedOrLostAckReservationNeverUsesStagedCache(int failSave, bool committed)
    {
        var f = new InteractionOccurrenceFixture(); var request = InteractionOccurrenceFixture.Request();
        var manager = InteractionOccurrenceFixture.Faulting<InteractionOccurrenceRegistrySnapshot>(f.Backend, failSave, committed);
        await Should.ThrowAsync<HttpRequestException>(() => InteractionOccurrenceFixture.Create(manager, f.Authority).ReserveAsync(request));
        f.Head.ShouldBe(failSave == 1 ? 0 : 1);
        // Round-trip both pending and main objects before a fresh actor performs reconciliation.
        foreach (var item in f.Backend.CommittedState.ToArray())
        {
            if (item.Value is AnchoredStateTransition pending) { await f.Backend.SetStateAsync(item.Key, JsonSerializer.Deserialize<AnchoredStateTransition>(JsonSerializer.Serialize(pending))!); }
            else { await f.Backend.SetStateAsync(item.Key, JsonSerializer.Deserialize<InteractionOccurrenceRegistrySnapshot>(JsonSerializer.Serialize(item.Value))!); }
        }
        await f.Backend.SaveStateAsync();
        var retry = await f.Actor.ReserveAsync(request); retry.Status.ShouldBe(InteractionOccurrenceReservationStatus.Reserved);
        retry.Record!.KeyReference.ShouldBe(request.ProposedKeyReference); f.Head.ShouldBe(1);
        f.Backend.CommittedState.Single().Value.ShouldBeOfType<InteractionOccurrenceRegistrySnapshot>().Records.Single().KeyReference.ShouldBe(request.ProposedKeyReference);
    }

    /// <summary>Existing reference bound denies a new occurrence before anchor/save while retaining exact original committed ciphertext lookup.</summary>
    [Fact]
    public async Task AtReferenceBoundRetainsOriginalOutcomeAndIndependentAnchor()
    {
        var f = new InteractionOccurrenceFixture(); var request = InteractionOccurrenceFixture.Request();
        await f.Actor.ReserveAsync(request); await f.Actor.RetainSealedAsync(request.Identity, InteractionOccurrenceFixture.Sealed());
        var original = (await f.Actor.CompleteWriterAsync(request.Identity, request.ProposedKeyReference, "commit-proof", true)).Record!;
        var saved = f.Backend.CommittedState.Single(); var state = (InteractionOccurrenceRegistrySnapshot)saved.Value;
        var records = new List<InteractionOccurrenceRecord> { original };
        for (int n = 1; n < 10000; n++)
        {
            string reference = "01ARZ3NDEKTSV4RRF" + n.ToString("D9", System.Globalization.CultureInfo.InvariantCulture);
            var extra = InteractionOccurrenceFixture.Request(checked((ulong)n + 1), reference);
            records.Add(new(extra, reference, n + 3, InteractionOccurrenceWriterState.Reserved, null, null));
        }
        state = state with { Revision = 10002, Records = records.ToArray() }; f.Head = state.Revision; f.AnchorDigest = InteractionOccurrenceFixture.Digest(state);
        await f.Backend.SetStateAsync(saved.Key, state, TestContext.Current.CancellationToken); await f.Backend.SaveStateAsync(TestContext.Current.CancellationToken);
        (await f.Actor.ReserveAsync(InteractionOccurrenceFixture.Request(10001, "01ARZ3NDEKTSV4RRFFQ69G5FAW"))).Status.ShouldBe(InteractionOccurrenceReservationStatus.Unavailable);
        f.Head.ShouldBe(10002); ((InteractionOccurrenceRegistrySnapshot)f.Backend.CommittedState.Single().Value).Records.Count.ShouldBe(10000);
        var read = (await f.Actor.LookupAsync(request.Identity)).Record!; read.KeyReference.ShouldBe(original.KeyReference); read.Sealed!.PayloadBytes.ShouldBe(original.Sealed!.PayloadBytes);
    }
    /// <summary>Equal epoch/revision with divergent committed source identity is denied by the independent exact-state anchor.</summary>
    [Fact]
    public async Task SameRevisionDivergentRestoreCannotReleaseOriginalReference()
    {
        var f = new InteractionOccurrenceFixture(); var request = InteractionOccurrenceFixture.Request(); await f.Actor.ReserveAsync(request);
        var saved = f.Backend.CommittedState.Single(); var state = (InteractionOccurrenceRegistrySnapshot)saved.Value;
        var record = state.Records.Single(); var changed = record with { Request = record.Request with { ContentIntentHmac = new string('B', 64) } };
        var restored = new InMemoryStateManager();
        await restored.SetStateAsync(saved.Key, state with { Records = [changed] }, TestContext.Current.CancellationToken);
        await restored.SaveStateAsync(TestContext.Current.CancellationToken);
        (await InteractionOccurrenceFixture.Create(restored, f.Authority).LookupAsync(request.Identity)).Status.ShouldBe(InteractionOccurrenceReservationStatus.Unavailable);
        ((InteractionOccurrenceRegistrySnapshot)restored.CommittedState.Single().Value).Revision.ShouldBe(f.Head);
    }

    /// <summary>An actor-ID-sensitive private transport executes persisted original reserve/seal/lookup through the canonical owner address.</summary>
    [Fact]
    public async Task PrivateRegistryTransportUsesCanonicalHostedActorAndRetainsOriginal()
    {
        var f = new InteractionOccurrenceFixture(); var proxies = Substitute.For<Dapr.Actors.Client.IActorProxyFactory>();
        proxies.CreateActorProxy<IInteractionOccurrenceRegistryActor>(Arg.Any<Dapr.Actors.ActorId>(), Arg.Any<string>()).Returns(call =>
        {
            call.Arg<Dapr.Actors.ActorId>().GetId().ShouldBe(InteractionOccurrenceRegistryActor.GetActorId("tenant-a"));
            call.Arg<string>().ShouldBe(InteractionOccurrenceRegistryActor.ActorTypeName);
            return InteractionOccurrenceFixture.Create(f.Backend, f.Authority);
        });
        var adapter = new DaprInteractionOccurrenceRegistry(proxies, TimeProvider.System); var request = InteractionOccurrenceFixture.Request();
        (await adapter.ReserveAsync(request, TestContext.Current.CancellationToken)).Status.ShouldBe(InteractionOccurrenceReservationStatus.Reserved);
        var sealedResult = InteractionOccurrenceFixture.Sealed();
        var sealedOriginal = await adapter.RetainSealedAsync(request.Identity, sealedResult, TestContext.Current.CancellationToken);
        sealedOriginal.Record!.Sealed!.PayloadBytes.ShouldBe(sealedResult.PayloadBytes);
        await f.Backend.ClearCacheAsync(TestContext.Current.CancellationToken);
        var original = await new DaprInteractionOccurrenceRegistry(proxies, TimeProvider.System).LookupAsync(request.Identity, TestContext.Current.CancellationToken);
        JsonSerializer.Serialize(original).ShouldBe(JsonSerializer.Serialize(sealedOriginal));
        f.Backend.CommittedState.Single().Value.ShouldBeOfType<InteractionOccurrenceRegistrySnapshot>().Records.Single().Request.ShouldBe(request);
    }
    /// <summary>Aggregate restored carriers must be refused before independent state validation or mutation, without changing original arrays.</summary>
    [Fact]
    public async Task RestoredCumulativeCiphertextBoundPrecedesIndependentStateCalls()
    {
        var f = new InteractionOccurrenceFixture();
        var records = Enumerable.Range(1, 3).Select(n =>
        {
            string reference = "01ARZ3NDEKTSV4RRF" + n.ToString("D9", System.Globalization.CultureInfo.InvariantCulture);
            var request = InteractionOccurrenceFixture.Request((ulong)n, reference);
            return new InteractionOccurrenceRecord(request, reference, n, InteractionOccurrenceWriterState.SealedPending,
                new(reference, Enumerable.Repeat((byte)n, 12 * 1024 * 1024).ToArray(), "json+pdenc-v2", 1), null);
        }).ToArray();
        var state = new InteractionOccurrenceRegistrySnapshot("tenant-a", "epoch-1", 3, records);
        await f.Backend.SetStateAsync("candidate-interaction-occurrences-v1", state, TestContext.Current.CancellationToken);
        await f.Backend.SaveStateAsync(TestContext.Current.CancellationToken);
        byte[][] hashes = records.Select(r => System.Security.Cryptography.SHA256.HashData(r.Sealed!.PayloadBytes)).ToArray();
        f.Authority.ClearReceivedCalls();

        await Should.ThrowAsync<InvalidOperationException>(() => f.Actor.LookupAsync(records[0].Request.Identity));

        await f.Authority.DidNotReceiveWithAnyArgs().ValidateStateAsync(default!, default!, default, default!, default);
        await f.Authority.DidNotReceiveWithAnyArgs().AdmitTransitionAsync(default!, default);
        await f.Authority.DidNotReceiveWithAnyArgs().RecordTransitionAsync(default!, default);
        await f.Authority.DidNotReceiveWithAnyArgs().AuthorizeReservationAsync(default!, default);
        f.Head.ShouldBe(0); f.Backend.CommittedState.Single().Value.ShouldBeSameAs(state);
        for (int n = 0; n < records.Length; n++) { System.Security.Cryptography.SHA256.HashData(records[n].Sealed!.PayloadBytes).ShouldBe(hashes[n]); }
    }

    /// <summary>Several exact carriers within the existing aggregate budget retain detached ciphertext after serialized restart.</summary>
    [Fact]
    public async Task WithinBoundMultiCarrierRestartKeepsExactOriginals()
    {
        var f = new InteractionOccurrenceFixture();
        for (int n = 1; n <= 3; n++)
        {
            string reference = "01ARZ3NDEKTSV4RRF" + n.ToString("D9", System.Globalization.CultureInfo.InvariantCulture);
            var request = InteractionOccurrenceFixture.Request((ulong)n, reference);
            await f.Actor.ReserveAsync(request);
            await f.Actor.RetainSealedAsync(request.Identity, InteractionOccurrenceFixture.Sealed(reference));
        }
        var saved = f.Backend.CommittedState.Single(); byte[] exact = JsonSerializer.SerializeToUtf8Bytes(saved.Value);
        var restored = new InMemoryStateManager();
        await restored.SetStateAsync(saved.Key, JsonSerializer.Deserialize<InteractionOccurrenceRegistrySnapshot>(exact)!, TestContext.Current.CancellationToken);
        await restored.SaveStateAsync(TestContext.Current.CancellationToken);
        var restarted = InteractionOccurrenceFixture.Create(restored, f.Authority);
        var original = JsonSerializer.Deserialize<InteractionOccurrenceRegistrySnapshot>(exact)!;
        foreach (var record in original.Records)
        {
            var result = await restarted.LookupAsync(record.Request.Identity);
            result.Status.ShouldBe(InteractionOccurrenceReservationStatus.Sealed);
            result.Record!.Sealed!.PayloadBytes.ShouldBe(record.Sealed!.PayloadBytes);
            result.Record.Sealed.PayloadBytes.ShouldNotBeSameAs(record.Sealed.PayloadBytes);
        }
        JsonSerializer.SerializeToUtf8Bytes(restored.CommittedState.Single().Value).ShouldBe(exact);
    }

    [Fact]
    public async Task InvalidRecoveredSnapshotJsonReturnsUnavailable()
    {
        var f = new InteractionOccurrenceFixture();
        byte[] bytes = "{not-json"u8.ToArray();
        string digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
        string scope = InteractionOccurrenceRegistryActor.GetActorId("tenant-a") + "|candidate-interaction-occurrences-v1";
        f.Head = 9;
        f.Authority.ReadCurrentOriginalAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new AnchoredStateTransition(scope, 0, 1, f.AnchorDigest, digest, bytes));
        (await f.Actor.LookupAsync(InteractionOccurrenceFixture.Request().Identity)).Status.ShouldBe(InteractionOccurrenceReservationStatus.Unavailable);
    }

    /// <summary>A newer committed original replaces a missing primary registry and keeps the exact sealed bytes.</summary>
    [Fact]
    public async Task NewerCurrentOriginalRestoresExactRegistryState()
    {
        var f = new InteractionOccurrenceFixture();
        var request = InteractionOccurrenceFixture.Request();
        await f.Actor.ReserveAsync(request);
        byte[] skeleton = [9, 8, 7];
        var sealedResult = InteractionOccurrenceFixture.Sealed() with { SnapshotReplaySkeleton = skeleton };
        await f.Actor.RetainSealedAsync(request.Identity, sealedResult);
        var saved = f.Backend.CommittedState.Single();
        var snapshot = (InteractionOccurrenceRegistrySnapshot)saved.Value;
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(snapshot);
        string digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
        f.Head = snapshot.Revision;
        f.AnchorDigest = digest;
        (await f.Backend.TryRemoveStateAsync(saved.Key, TestContext.Current.CancellationToken)).ShouldBeTrue();
        await f.Backend.SaveStateAsync(TestContext.Current.CancellationToken);
        f.Backend.CommittedState.ShouldBeEmpty();
        string scope = InteractionOccurrenceRegistryActor.GetActorId("tenant-a") + "|candidate-interaction-occurrences-v1";
        f.Authority.ReadCurrentOriginalAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new AnchoredStateTransition(scope, 0, snapshot.Revision, digest, digest, bytes));
        var lookup = await f.Actor.LookupAsync(request.Identity);
        lookup.Status.ShouldBe(InteractionOccurrenceReservationStatus.Sealed);
        lookup.Record!.Sealed!.PayloadBytes.ShouldBe(sealedResult.PayloadBytes);
        lookup.Record.Sealed.SnapshotReplaySkeleton.ShouldBe(skeleton);
        ((InteractionOccurrenceRegistrySnapshot)f.Backend.CommittedState.Single().Value).Revision.ShouldBe(snapshot.Revision);
    }

    /// <summary>A newer matching original replaces an older stored snapshot that no longer validates.</summary>
    [Fact]
    public async Task NewerCurrentOriginalReplacesOlderStoredRegistry()
    {
        var f = new InteractionOccurrenceFixture();
        var request = InteractionOccurrenceFixture.Request();
        await f.Actor.ReserveAsync(request);
        await f.Actor.RetainSealedAsync(request.Identity, InteractionOccurrenceFixture.Sealed());
        var stored = (InteractionOccurrenceRegistrySnapshot)f.Backend.CommittedState.Single().Value;
        long olderRevision = stored.Revision;
        var newerRecord = stored.Records[0] with { RegistryRevision = olderRevision + 1 };
        var newer = stored with { Revision = olderRevision + 1, Records = [newerRecord] };
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(newer);
        string digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
        f.Head = newer.Revision;
        f.AnchorDigest = digest;
        ((InteractionOccurrenceRegistrySnapshot)f.Backend.CommittedState.Single().Value).Revision.ShouldBe(olderRevision);
        string scope = InteractionOccurrenceRegistryActor.GetActorId("tenant-a") + "|candidate-interaction-occurrences-v1";
        string olderDigest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(stored)));
        f.Authority.ReadCurrentOriginalAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new AnchoredStateTransition(scope, olderRevision, newer.Revision, olderDigest, digest, bytes));
        var lookup = await f.Actor.LookupAsync(request.Identity);
        lookup.Status.ShouldBe(InteractionOccurrenceReservationStatus.Sealed);
        lookup.Record!.RegistryRevision.ShouldBe(newer.Revision);
        lookup.Record.Sealed!.PayloadBytes.ShouldBe(newerRecord.Sealed!.PayloadBytes);
        ((InteractionOccurrenceRegistrySnapshot)f.Backend.CommittedState.Single().Value).Revision.ShouldBe(newer.Revision);
    }

    /// <summary>A recovered snapshot that deserializes to null is unavailable, including the argument-null refusal.</summary>
    [Fact]
    public async Task NullRecoveredSnapshotReturnsUnavailable()
    {
        var f = new InteractionOccurrenceFixture();
        byte[] bytes = "null"u8.ToArray();
        string digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
        string scope = InteractionOccurrenceRegistryActor.GetActorId("tenant-a") + "|candidate-interaction-occurrences-v1";
        f.Head = 9;
        f.Authority.ReadCurrentOriginalAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new AnchoredStateTransition(scope, 0, 1, f.AnchorDigest, digest, bytes));
        (await f.Actor.LookupAsync(InteractionOccurrenceFixture.Request().Identity)).Status.ShouldBe(InteractionOccurrenceReservationStatus.Unavailable);
    }

    /// <summary>Retained skeleton bytes are detached from the caller, and an oversized skeleton is refused before another write.</summary>
    [Fact]
    public async Task SealedSkeletonIsDetachedAndOversizeSkeletonIsRejected()
    {
        var f = new InteractionOccurrenceFixture();
        var request = InteractionOccurrenceFixture.Request();
        await f.Actor.ReserveAsync(request);
        byte[] skeleton = [1, 2, 3];
        var sealedResult = InteractionOccurrenceFixture.Sealed() with { SnapshotReplaySkeleton = skeleton };
        var retained = await f.Actor.RetainSealedAsync(request.Identity, sealedResult);
        skeleton[0] = 9;
        retained.Record!.Sealed!.SnapshotReplaySkeleton.ShouldNotBeNull();
        retained.Record.Sealed.SnapshotReplaySkeleton[0].ShouldBe((byte)1);
        retained.Record.Sealed.SnapshotReplaySkeleton.ShouldNotBeSameAs(skeleton);
        string before = JsonSerializer.Serialize(f.Backend.CommittedState.Single().Value);
        await Should.ThrowAsync<ArgumentException>(() => f.Actor.RetainSealedAsync(request.Identity, InteractionOccurrenceFixture.Sealed() with { SnapshotReplaySkeleton = new byte[16777217] }));
        JsonSerializer.Serialize(f.Backend.CommittedState.Single().Value).ShouldBe(before);
    }

}
