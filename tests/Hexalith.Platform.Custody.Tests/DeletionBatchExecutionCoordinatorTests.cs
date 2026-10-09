using Hexalith.EventStore.Contracts.Security;
using System.Text.Json;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Custody.Tests;

/// <summary>Exact canonical signature through actual guard adapter/reducer and private execution coordinator; synthetic owner authority/consumption never qualifies a production installation.</summary>
public sealed class DeletionBatchExecutionCoordinatorTests
{
    /// <summary>Canonical signed artifact, actual issue/dispatch revisions and complete ordered owner vector correlate through conditional completion; restart reuses all original outcomes.</summary>
    [Fact]
    public async Task ActualCanonicalArtifactFlowsThroughIssueDispatchConsumptionAndCompletion()
    {
        using var fixture = new DeletionBatchExecutionFixture();
        var result = await fixture.Coordinator.ExecuteAsync(fixture.Payload, cancellationToken: TestContext.Current.CancellationToken);
        result.Status.ShouldBe("CompletionSealed"); result.Signing!.SigningRequestId.ShouldBe(DeletionBatchCapabilityIdentity.SigningRequestId(fixture.Payload));
        var batch = fixture.State.Deletions.Single().Batches.Single(); batch.Capability.ShouldBe(fixture.Payload); batch.IssuedGuardRevision.ShouldBe(11); batch.DispatchGuardRevision.ShouldBe(13);
        result.Protection!.TargetReceipts.Count.ShouldBe(2); fixture.Reservations.ShouldBe(1); fixture.Signatures.ShouldBe(1); fixture.State.Deletions.Single().Completed.ShouldBeTrue();
        int mutations = fixture.SourceMutations;
        (await fixture.Coordinator.ExecuteAsync(fixture.Payload, cancellationToken: TestContext.Current.CancellationToken)).Status.ShouldBe("CompletionSealed");
        fixture.Signatures.ShouldBe(1); fixture.Reservations.ShouldBe(1); fixture.SourceMutations.ShouldBe(mutations);
    }

    /// <summary>Irreversible response loss uses only exact original protection lookup and retains the original vector without a second reservation.</summary>
    [Fact]
    public async Task LostConsumptionAcknowledgementUsesExactOriginalLookup()
    {
        using var fixture = new DeletionBatchExecutionFixture { LoseConsumptionAcknowledgement = true };
        var result = await fixture.Coordinator.ExecuteAsync(fixture.Payload, cancellationToken: TestContext.Current.CancellationToken);
        result.Status.ShouldBe("CompletionSealed"); fixture.Reservations.ShouldBe(1);
        await fixture.Protection.Received(1).LookupAsync("tenant-a", "batch-a", Arg.Any<CancellationToken>());
    }

    /// <summary>Unknown or partial owner outcomes cannot mirror consumption or complete, despite a valid signature and dispatch.</summary>
    [Theory]
    [InlineData(true)][InlineData(false)]
    public async Task UnknownOrPartialConsumptionNeverBecomesCompletion(bool unknown)
    {
        using var fixture = new DeletionBatchExecutionFixture { LoseConsumptionAcknowledgement = unknown, UnknownProtectionLookup = unknown, PartialProtectionResult = !unknown };
        var result = await fixture.Coordinator.ExecuteAsync(fixture.Payload, cancellationToken: TestContext.Current.CancellationToken);
        result.Status.ShouldBe(unknown ? "ConsumptionUnknown" : "ConsumptionUnverified"); fixture.State.Deletions.Single().Completed.ShouldBeFalse();
        fixture.State.Deletions.Single().Batches.Single().ProtectionOutcome.ShouldBe("Unconsumed"); fixture.Reservations.ShouldBe(1);
    }

    /// <summary>Only durable exact no-issue terminalizes the stale signed attempt; its successor preserves stable identities and increments exactly the attempt/current compare/key.</summary>
    [Theory]
    [InlineData(true)][InlineData(false)]
    public async Task StaleIssueNeedsExactNoIssueBeforeStableSuccessor(bool missingProof)
    {
        using var fixture = new DeletionBatchExecutionFixture { StaleIssue = true, NoIssueProofUnavailable = missingProof };
        var result = await fixture.Coordinator.ExecuteAsync(fixture.Payload, cancellationToken: TestContext.Current.CancellationToken);
        result.Status.ShouldBe(missingProof ? "NoIssueProofUnavailable" : "ObsoleteUnissued"); fixture.Reservations.ShouldBe(0); fixture.Signatures.ShouldBe(1);
        if (missingProof) { result.NextAttempt.ShouldBeNull(); }
        else
        {
            var next = result.NextAttempt!; next.SigningAttemptOrdinal.ShouldBe(2); next.AttestationOrdinal.ShouldBe(fixture.Payload.AttestationOrdinal);
            next.ShouldBe(fixture.Payload with { SigningAttemptOrdinal = 2, IntendedIssuedGuardRevision = 11, CapabilityKeyVersion = "key-b" });
            (await fixture.Coordinator.ExecuteAsync(next, cancellationToken: TestContext.Current.CancellationToken)).Status.ShouldBe("CompletionSealed");
            fixture.Signatures.ShouldBe(2); fixture.Reservations.ShouldBe(1);
        }
    }

    /// <summary>A post-start Open hold prevents dispatch and every protection call; signature/issue alone never authorizes consumption.</summary>
    [Fact]
    public async Task PostStartHoldBlocksBeforeProtectionOwner()
    {
        using var fixture = new DeletionBatchExecutionFixture { BlockDispatchWithHold = true };
        (await fixture.Coordinator.ExecuteAsync(fixture.Payload, cancellationToken: TestContext.Current.CancellationToken)).Status.ShouldBe("Blocked");
        fixture.Protection.ReceivedCalls().ShouldBeEmpty(); fixture.State.Deletions.Single().Completed.ShouldBeFalse();
    }

    /// <summary>A persisted guard issue with lost response is recovered by original exact source result on restart; no second source issue or signature is created.</summary>
    [Fact]
    public async Task LostIssueResponseRetainsOriginalArtifactAndRecoversWithoutNewIssue()
    {
        using var fixture = new DeletionBatchExecutionFixture { LoseIssueAcknowledgement = true };
        (await fixture.Coordinator.ExecuteAsync(fixture.Payload, cancellationToken: TestContext.Current.CancellationToken)).Status.ShouldBe("Unavailable");
        fixture.State.Deletions.Single().Batches.Single().Capability.ShouldBe(fixture.Payload);
        (await fixture.Coordinator.ExecuteAsync(fixture.Payload, cancellationToken: TestContext.Current.CancellationToken)).Status.ShouldBe("CompletionSealed");
        fixture.Signatures.ShouldBe(1); fixture.Reservations.ShouldBe(1);
    }

    /// <summary>Missing qualified private bindings leave the ordinary coordinator unavailable without signer, guard or consuming-owner invocation.</summary>
    [Fact]
    public async Task DefaultUnavailableCompositionMakesNoEffects()
    {
        using var fixture = new DeletionBatchExecutionFixture();
        (await new DeletionBatchExecutionCoordinator(TimeProvider.System).ExecuteAsync(fixture.Payload, cancellationToken: TestContext.Current.CancellationToken)).Status.ShouldBe("Unavailable");
        fixture.Signatures.ShouldBe(0); fixture.Reservations.ShouldBe(0); fixture.Source.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Actual serialized Dapr guard transaction, retained signer and protection actors preserve one signature/reservation/vector and their original receipts across coordinator restart.</summary>
    [Fact]
    public async Task ActualOwnersAndSharedTransactionPreserveOriginalResultsThroughRestart()
    {
        using var fixture = new DeletionBatchActualOwnerFixture(); fixture.Backend.LoseAcknowledgement = true;
        var original = await fixture.Coordinator.ExecuteAsync(fixture.Payload, cancellationToken: TestContext.Current.CancellationToken);
        original.Status.ShouldBe("CompletionSealed"); fixture.Signatures.ShouldBe(1); fixture.Reservations.ShouldBe(1);
        int commits = fixture.Backend.CommittedTransactions;
        (await fixture.Coordinator.ExecuteAsync(fixture.Payload, cancellationToken: TestContext.Current.CancellationToken)).Status.ShouldBe("CompletionSealed");
        fixture.Backend.CommittedTransactions.ShouldBe(commits); fixture.Signatures.ShouldBe(1); fixture.Reservations.ShouldBe(1);
        original.Protection!.TargetReceipts.Count.ShouldBe(2);
        var ledger = fixture.ProtectionState.CommittedState.Single().Value; ledger.GetType().Name.ShouldBe("DeletionConsumptionLedger");
        string persistedLedger = JsonSerializer.Serialize(ledger); using var document = JsonDocument.Parse(persistedLedger);
        var retained = document.RootElement.GetProperty("Batches").EnumerateArray().Single();
        var retainedRequest = retained.GetProperty("Original").Deserialize<DeletionBatchConsumptionRequest>()!;
        var retainedOutcome = retained.GetProperty("Outcome").Deserialize<DeletionConsumptionOutcome>()!;
        retainedRequest.Capability.ShouldBe(fixture.Payload); retainedRequest.DispatchReceiptId.ShouldBe(fixture.State.Deletions.Single().Batches.Single().DispatchReceiptId);
        retainedOutcome.Status.ShouldBe(DeletionConsumptionStatus.Consumed); retainedOutcome.ReceiptId.ShouldBe(original.Protection.ReceiptId);
        retainedOutcome.TargetReceipts.ShouldBe(original.Protection.TargetReceipts); retainedOutcome.TargetReceipts.Select(receipt => receipt.Target).ShouldBe(retainedRequest.Targets);
        var lookedUp = await fixture.ProtectionActor.LookupAsync("tenant-a", fixture.Payload.BatchId);
        JsonSerializer.Serialize(lookedUp).ShouldBe(JsonSerializer.Serialize(retainedOutcome));
        JsonSerializer.Serialize(fixture.ProtectionState.CommittedState.Single().Value).ShouldBe(persistedLedger); fixture.Reservations.ShouldBe(1);
    }

    /// <summary>Actual signer Unknown and protection ConsumptionReserved recover only the original retained physical results through their real actor lookup paths.</summary>
    [Fact]
    public async Task ActualSignerAndProtectionTypedUnknownRecoverWithoutNewPhysicalWork()
    {
        using var fixture = new DeletionBatchActualOwnerFixture { LoseSignatureResponse = true, LoseConsumptionResponse = true };
        (await fixture.Coordinator.ExecuteAsync(fixture.Payload, cancellationToken: TestContext.Current.CancellationToken)).Status.ShouldBe("Unknown");
        fixture.Signatures.ShouldBe(1); fixture.Reservations.ShouldBe(0);
        (await fixture.Coordinator.ExecuteAsync(fixture.Payload, cancellationToken: TestContext.Current.CancellationToken)).Status.ShouldBe("CompletionSealed");
        fixture.Signatures.ShouldBe(1); fixture.Reservations.ShouldBe(1);
    }

    /// <summary>Actual terminal CAS receipt flows through the no-issue owner/adapter and retained signer actor before a stable canonical successor issues and consumes.</summary>
    [Fact]
    public async Task ActualNoIssueOwnerAdapterAndSignerProveStableSuccessor()
    {
        using var fixture = new DeletionBatchActualOwnerFixture { MakeFirstIssueStale = true };
        var result = await fixture.Coordinator.ExecuteAsync(fixture.Payload, cancellationToken: TestContext.Current.CancellationToken);
        result.Status.ShouldBe("ObsoleteUnissued"); result.Signing!.NoIssueProof.ShouldNotBeNull(); result.NextAttempt.ShouldNotBeNull();
        fixture.State.Deletions.Single().Batches.Single().Capability.ShouldBeNull(); fixture.Signatures.ShouldBe(1); fixture.Reservations.ShouldBe(0);
        var next = result.NextAttempt!; next.ShouldBe(fixture.Payload with { SigningAttemptOrdinal = 2, IntendedIssuedGuardRevision = 11, CapabilityKeyVersion = "key-b" });
        (await fixture.Coordinator.ExecuteAsync(next, cancellationToken: TestContext.Current.CancellationToken)).Status.ShouldBe("CompletionSealed");
        fixture.Signatures.ShouldBe(2); fixture.Reservations.ShouldBe(1);
    }

    /// <summary>Actual same-owner revocation and exact EventStore mirror preserve per-batch block receipts through replacement activation and terminal all-target completion.</summary>
    [Fact]
    public async Task ActualRevocationMirrorAndReplacementActivationCompleteExactOriginalBatch()
    {
        using var fixture = new DeletionBatchActualOwnerFixture(); var port = new EventStoreDeletionBatchGuardPort(fixture.Guard);
        var signed = await fixture.Signer(fixture.Payload).SignAsync(fixture.Payload);
        (await port.IssueAsync(signed, false, TestContext.Current.CancellationToken))!.Status.ShouldBe("Committed");
        (await port.DispatchAsync(signed, TestContext.Current.CancellationToken))!.Status.ShouldBe("Committed");
        var batch = fixture.State.Deletions.Single().Batches.Single();
        var request = new DeletionBatchConsumptionRequest(fixture.Payload, signed.DetachedJws!, batch.IssuedGuardRevision, batch.DispatchReceiptId, batch.DispatchGuardRevision,
            batch.Targets.Select(value => new ProtectionTarget(value.TenantId, value.AgentInteractionId, value.TargetProtectionKeyAlias)).ToArray());
        (await fixture.ProtectionActor.RegisterAsync(request)).Status.ShouldBe(DeletionConsumptionStatus.Unconsumed);
        var envelope = new DeletionCapabilityRevocationEnvelope("issuer-a", "protection-a", "tenant-a", "DeletionBatchCapabilitySigningKey", "key-a", 1, 1, "revocation-a", new string('A', 64));
        var block = (await fixture.ProtectionActor.RegisterRevocationAsync(envelope))!;
        var mirror = new EventStoreDeletionCapabilityGuardRevocationMirror(fixture.Guard);
        (await mirror.RecordAsync(block, TestContext.Current.CancellationToken)).ShouldBeTrue();
        var mirrored = (await mirror.LookupAsync(envelope, TestContext.Current.CancellationToken))!;
        mirrored.ReceiptId.ShouldBe(block.ReceiptId); mirrored.AffectedBatchIds.ShouldBe(block.AffectedBatchIds);
        var protectedBlock = await fixture.ProtectionActor.LookupAsync("tenant-a", fixture.Payload.BatchId);
        fixture.State.Deletions.Single().Batches.Single().ProtectionReceiptId.ShouldBe(protectedBlock.ReceiptId);
        var replacement = fixture.Payload with { AttestationOrdinal = 2, CapabilityKeyVersion = "key-b", IntendedIssuedGuardRevision = fixture.State.Revision };
        var completed = await fixture.Coordinator.ExecuteAsync(replacement, true, TestContext.Current.CancellationToken);
        completed.Status.ShouldBe("CompletionSealed"); fixture.Reservations.ShouldBe(1); fixture.Signatures.ShouldBe(2);
        fixture.State.CompromisedKeyVersions.ShouldContain("key-a"); fixture.State.Deletions.Single().Batches.Single().BatchId.ShouldBe(fixture.Payload.BatchId);
        int commits = fixture.Backend.CommittedTransactions;
        (await mirror.RecordAsync(block, TestContext.Current.CancellationToken)).ShouldBeTrue(); fixture.Backend.CommittedTransactions.ShouldBe(commits);
    }

    /// <summary>A port's blocked cancellation callback never retains the coordinator continuation or starts later dispatch/protection work.</summary>
    [Fact]
    public async Task BlockingProviderCancellationDoesNotRetainCallerOrResumeSourceWork()
    {
        using var fixture = new DeletionBatchExecutionFixture(); using var caller = new CancellationTokenSource(); using var releaseCallback = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var callback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var late = new TaskCompletionSource<GovernanceProtocolReceipt?>(TaskCreationOptions.RunContinuationsAsynchronously); var port = Substitute.For<IDeletionBatchGuardPort>();
        port.IssueAsync(Arg.Any<DeletionCapabilitySigningOutcome>(), false, Arg.Any<CancellationToken>()).Returns(call =>
        {
            call.Arg<CancellationToken>().Register(() => { callback.SetResult(); releaseCallback.Wait(); }); entered.SetResult(); return late.Task;
        });
        var coordinator = new DeletionBatchExecutionCoordinator(TimeProvider.System, port, _ => fixture.Signer, _ => fixture.Protection);
        var pending = coordinator.ExecuteAsync(fixture.Payload, cancellationToken: caller.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); caller.Cancel();
        try
        {
            await Should.ThrowAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            await callback.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await port.DidNotReceive().DispatchAsync(Arg.Any<DeletionCapabilitySigningOutcome>(), Arg.Any<CancellationToken>()); fixture.Protection.ReceivedCalls().ShouldBeEmpty();
        }
        finally { releaseCallback.Set(); late.SetResult(new("late", "late", "Committed", 11, 1, "", "late")); }
    }

    /// <summary>Actual subscriber retains separately authenticated historical source events in either mirror order and retries; higher same-key events preserve the first immutable per-batch block receipt.</summary>
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task ActualSubscriberSupportsHigherSameKeyAndUnorderedDifferentKeySourceProofs(bool differentKeys)
    {
        using var f = new DeletionBatchActualOwnerFixture(); var port = new EventStoreDeletionBatchGuardPort(f.Guard);
        var signed = await f.Signer(f.Payload).SignAsync(f.Payload); await port.IssueAsync(signed, false, TestContext.Current.CancellationToken); await port.DispatchAsync(signed, TestContext.Current.CancellationToken);
        var batch = f.State.Deletions.Single().Batches.Single();
        var request = new DeletionBatchConsumptionRequest(f.Payload, signed.DetachedJws!, batch.IssuedGuardRevision, batch.DispatchReceiptId, batch.DispatchGuardRevision,
            batch.Targets.Select(t => new ProtectionTarget(t.TenantId, t.AgentInteractionId, t.TargetProtectionKeyAlias)).ToArray());
        await f.ProtectionActor.RegisterAsync(request);
        var first = new DeletionCapabilityRevocationEnvelope("issuer-a", "protection-a", "tenant-a", "DeletionBatchCapabilitySigningKey", "key-a", 1, 1, "original-source-event", new string('A', 64));
        var second = first with { KeyVersion = differentKeys ? "unrelated-key" : "key-a", RevocationRevision = 2, EventIdentity = "higher-source-event", SignatureDigest = new string('B', 64) };
        var firstProof = (await f.ProtectionActor.RegisterRevocationAsync(first))!; var originalBlock = await f.ProtectionActor.LookupAsync("tenant-a", f.Payload.BatchId);
        var secondProof = (await f.ProtectionActor.RegisterRevocationAsync(second))!; secondProof.AffectedBatchIds.ShouldBeEmpty();
        var auth = Substitute.For<IDeletionCapabilityRevocationAuthenticator>(); DateTimeOffset sourceProofTime = DateTimeOffset.UtcNow;
        auth.VerifyAsync(Arg.Any<DeletionCapabilityRevocationEnvelope>(), "signed-source-proof", Arg.Any<CancellationToken>()).Returns(call => new DeletionCapabilityRevocationAuthorization(call.Arg<DeletionCapabilityRevocationEnvelope>(), "independent-original-source-proof", sourceProofTime.AddSeconds(-1), sourceProofTime.AddMinutes(1)));
        var subscriber = new DeletionCapabilityRevocationSubscriber(new(first.Issuer, first.Audience, first.TenantId), auth, f.Registrar, new EventStoreDeletionCapabilityGuardRevocationMirror(f.Guard), TimeProvider.System);
        if (differentKeys) { (await subscriber.ReceiveAsync(second, "signed-source-proof", TestContext.Current.CancellationToken)).ShouldBeTrue(); }
        (await subscriber.ReceiveAsync(first, "signed-source-proof", TestContext.Current.CancellationToken)).ShouldBeTrue();
        if (!differentKeys) { (await subscriber.ReceiveAsync(second, "signed-source-proof", TestContext.Current.CancellationToken)).ShouldBeTrue(); }
        var mirrored = f.State.Deletions.Single().Batches.Single(); mirrored.ProtectionReceiptId.ShouldBe(originalBlock.ReceiptId); mirrored.BlockSetRevision.ShouldBe(1);
        f.State.Revocations.Count.ShouldBe(2); f.State.CompromisedKeyVersions.Count.ShouldBe(differentKeys ? 2 : 1);
        f.State.Revocations.Single(r => r.Envelope == first).ReceiptId.ShouldBe(firstProof.ReceiptId); f.State.Revocations.Single(r => r.Envelope == second).ReceiptId.ShouldBe(secondProof.ReceiptId);
        int commits = f.Backend.CommittedTransactions;
        (await subscriber.ReceiveAsync(first, "signed-source-proof", TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await subscriber.ReceiveAsync(second, "signed-source-proof", TestContext.Current.CancellationToken)).ShouldBeTrue(); f.Backend.CommittedTransactions.ShouldBe(commits);
        // The global compare is now 2 while the immutable original batch block remains 1.
        var replacement = f.Payload with { AttestationOrdinal = 2, CapabilityKeyVersion = "key-b", IntendedIssuedGuardRevision = f.State.Revision };
        var completed = await f.Coordinator.ExecuteAsync(replacement, true, TestContext.Current.CancellationToken);
        completed.Status.ShouldBe("CompletionSealed"); f.LatestActivation!.ExpectedKeyBlockSetRevision.ShouldBe(2); f.LatestActivation.CompromiseBlockReceiptId.ShouldBe(originalBlock.ReceiptId);
        f.LatestActivation.OperationId.ShouldEndWith("-compare-2"); f.Reservations.ShouldBe(1);
    }
    /// <summary>A reservation winner recovers its independently proved original irreversible vector after revocation and guard mirroring; a revocation winner cannot reserve.</summary>
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task ActualCoordinatorOrdersOriginalReservationAgainstMirroredCompromise(bool reservationFirst)
    {
        using var f = new DeletionBatchActualOwnerFixture { LoseConsumptionResponse = reservationFirst, UnknownConsumptionLookup = reservationFirst };
        var port = new EventStoreDeletionBatchGuardPort(f.Guard);
        if (reservationFirst) { (await f.Coordinator.ExecuteAsync(f.Payload, cancellationToken: TestContext.Current.CancellationToken)).Status.ShouldBe("ConsumptionUnknown"); }
        else
        {
            var signed = await f.Signer(f.Payload).SignAsync(f.Payload); await port.IssueAsync(signed, false, TestContext.Current.CancellationToken); await port.DispatchAsync(signed, TestContext.Current.CancellationToken);
            var batch = f.State.Deletions.Single().Batches.Single(); await f.ProtectionActor.RegisterAsync(new(f.Payload, signed.DetachedJws!, batch.IssuedGuardRevision, batch.DispatchReceiptId,
                batch.DispatchGuardRevision, batch.Targets.Select(t => new ProtectionTarget(t.TenantId, t.AgentInteractionId, t.TargetProtectionKeyAlias)).ToArray()));
        }
        var envelope = new DeletionCapabilityRevocationEnvelope("issuer-a", "protection-a", "tenant-a", "DeletionBatchCapabilitySigningKey", "key-a", 1, 1, "race-revocation", new string('A', 64));
        var block = (await f.ProtectionActor.RegisterRevocationAsync(envelope))!; (await new EventStoreDeletionCapabilityGuardRevocationMirror(f.Guard).RecordAsync(block, TestContext.Current.CancellationToken)).ShouldBeTrue();
        f.State.Deletions.Single().Batches.Single().DispatchReceiptId.ShouldNotBeNullOrWhiteSpace(); f.UnknownConsumptionLookup = false;
        var recovered = await f.Coordinator.ExecuteAsync(f.Payload, cancellationToken: TestContext.Current.CancellationToken);
        recovered.Status.ShouldBe(reservationFirst ? "CompletionSealed" : "ConsumptionBlocked"); f.Reservations.ShouldBe(reservationFirst ? 1 : 0);
        f.State.Deletions.Single().Completed.ShouldBe(reservationFirst); f.State.CompromisedKeyVersions.ShouldContain("key-a");
        if (reservationFirst) { recovered.Protection!.TargetReceipts.Count.ShouldBe(2); (await f.Coordinator.ExecuteAsync(f.Payload, cancellationToken: TestContext.Current.CancellationToken)).Status.ShouldBe("CompletionSealed"); f.Reservations.ShouldBe(1); }
    }
    /// <summary>Actual covered-target owner outcome carries the requesting batch's immutable aggregate receipt through guard mirroring, completion, and coordinator retry without a second physical reservation.</summary>
    [Fact]
    public async Task ActualCoordinatorCompletesCoveredTargetsWithOriginalTargetReceipts()
    {
        using var f = new DeletionBatchActualOwnerFixture();
        f.ProtectionAuthority.VerifyDispatchAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<CancellationToken>()).Returns(true);
        var originalPayload = f.Payload with { BatchId = "previous-destroying-batch" }; var signed = await f.Signer(originalPayload).SignAsync(originalPayload);
        var targets = f.State.Deletions.Single().Batches.Single().Targets.Select(t => new ProtectionTarget(t.TenantId, t.AgentInteractionId, t.TargetProtectionKeyAlias)).ToArray();
        var original = new DeletionBatchConsumptionRequest(originalPayload, signed.DetachedJws!, 11, "independent-original-dispatch", 13, targets);
        await f.ProtectionActor.RegisterAsync(original); var destroyed = await f.ProtectionActor.ReserveAndConsumeAsync(original); f.Reservations.ShouldBe(1);
        var result = await f.Coordinator.ExecuteAsync(f.Payload, cancellationToken: TestContext.Current.CancellationToken);
        result.Status.ShouldBe("CompletionSealed"); result.Protection!.Status.ShouldBe(DeletionConsumptionStatus.AlreadyDestroyedByBatch); result.Protection.BatchId.ShouldBe(f.Payload.BatchId);
        result.Protection.ReceiptId.ShouldNotBeNullOrWhiteSpace(); result.Protection.TargetReceipts.ShouldBe(destroyed.TargetReceipts); result.Protection.TargetReceipts.All(t => t.OriginalBatchId == originalPayload.BatchId).ShouldBeTrue();
        var retried = await f.Coordinator.ExecuteAsync(f.Payload, cancellationToken: TestContext.Current.CancellationToken); retried.Status.ShouldBe("CompletionSealed");
        JsonSerializer.Serialize(retried.Protection).ShouldBe(JsonSerializer.Serialize(result.Protection)); f.Reservations.ShouldBe(1);
    }

    /// <summary>Actual post-seal content invalidates completion; exact containment coverage and conditional recompletion retain both historical seals, and retry selects the current latest receipt.</summary>
    [Fact]
    public async Task LatestCompletionReceiptSurvivesPostSealContentCoverageAndRecompletion()
    {
        using var f = new DeletionBatchActualOwnerFixture(); var target = f.State.Deletions.Single().Batches.Single().Targets[0];
        f.ViolationResource = "late-content-resource"; f.ViolationTarget = target;
        f.ViolationFacts = new("tenant-a", target.AgentInteractionId, "conversation-a", "conversation-a", "original-permit-owner", "original-permit", "original-effect-capability", 1,
            DirectoryWriteKind.ContentAppend, f.State.EpochId, "original-source-acceptance");
        // Commit the original source append through the actual guard before the independently installed deletion fence/seal.
        var installed = f.State; f.SetState(installed with { Deletions = [] });
        var append = new GovernanceGuardTransition("tenant-a", "original-content-append", GovernanceGuardOperation.AppendWrite, f.State.Revision, f.State.EpochId,
            "deletion-a", null, f.ViolationFacts, null, null, null, null, "", f.ViolationResource);
        f.OriginalAcceptance = (await f.Guard.ExecuteAsync(append, [new("original-content-cell", 0, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData([])), "sealed-original-content"u8.ToArray())], TestContext.Current.CancellationToken))!;
        f.OriginalAcceptance.Status.ShouldBe("Accepted");
        f.SetState(f.State with { Deletions = installed.Deletions });
        var payload = f.Payload with { IntendedIssuedGuardRevision = f.State.Revision };
        var first = await f.Coordinator.ExecuteAsync(payload, cancellationToken: TestContext.Current.CancellationToken); first.Status.ShouldBe("CompletionSealed"); var originalCompletion = first.Guard!;
        var ordinal = new GovernanceOrdinalCommand(1, "", "", "", "", "post-seal-content", "Content", f.OriginalAcceptance.AcceptedAtAdmissionFenceOrdinal, f.OriginalAcceptance.GuardHighWater, f.ViolationResource, [], "");
        var violation = new GovernanceGuardTransition("tenant-a", "record-post-seal-content", GovernanceGuardOperation.RecordViolation, f.State.Revision, f.State.EpochId,
            "deletion-a", null, null, null, ordinal, null, null, "", f.ViolationResource);
        (await f.Guard.ExecuteAsync(violation, [], TestContext.Current.CancellationToken))!.Status.ShouldBe("Committed"); f.State.Deletions.Single().Completed.ShouldBeFalse();
        var port = new EventStoreDeletionBatchGuardPort(f.Guard); (await port.CompleteAsync(payload, TestContext.Current.CancellationToken))!.Status.ShouldBe("Blocked");
        var batch = new GovernanceBatchCommand("coverage-link", "Containment", 1, [target], DeletionBatchCapabilityIdentity.TargetManifestDigest([new(target.TenantId, target.AgentInteractionId, target.TargetProtectionKeyAlias)]), 0, "", "", "", "", [], 0);
        var coverage = new GovernanceGuardTransition("tenant-a", "authorize-content-coverage", GovernanceGuardOperation.AuthorizeContainment, f.State.Revision, f.State.EpochId,
            "deletion-a", null, null, null, null, null, batch, "coverage-authorization", f.ViolationResource);
        (await f.Guard.ExecuteAsync(coverage, [], TestContext.Current.CancellationToken))!.Status.ShouldBe("CoverageLinked");
        var recompleted = (await port.CompleteAsync(payload, TestContext.Current.CancellationToken))!; recompleted.Status.ShouldBe("CompletionSealed"); recompleted.GuardHighWater.ShouldBeGreaterThan(originalCompletion.GuardHighWater);
        var latest = await port.CompleteAsync(payload, TestContext.Current.CancellationToken); latest.ShouldBe(recompleted);
        (await f.Coordinator.ExecuteAsync(payload, cancellationToken: TestContext.Current.CancellationToken)).Guard.ShouldBe(recompleted);
        f.State.Receipts.Count(r => r.Status == "CompletionSealed").ShouldBe(2); f.Reservations.ShouldBe(1);
    }

    /// <summary>Actual protection/coordinator refuses duplicate physical receipt identities and recovers the independently valid original vector without another destruction.</summary>
    [Fact]
    public async Task ActualDuplicatePhysicalReceiptIdsCannotCertifyCompletion()
    {
        using var fixture = new DeletionBatchActualOwnerFixture { DuplicatePhysicalReceiptIds = true };
        var denied = await fixture.Coordinator.ExecuteAsync(fixture.Payload, cancellationToken: TestContext.Current.CancellationToken);
        denied.Status.ShouldBe("ConsumptionReserved"); fixture.Reservations.ShouldBe(1); fixture.State.Deletions.Single().Completed.ShouldBeFalse();
        using var reservedDocument = JsonDocument.Parse(JsonSerializer.Serialize(fixture.ProtectionState.CommittedState.Single().Value));
        var reserved = reservedDocument.RootElement.GetProperty("Batches").EnumerateArray().Single().GetProperty("Outcome").Deserialize<DeletionConsumptionOutcome>()!;
        reserved.Status.ShouldBe(DeletionConsumptionStatus.ConsumptionReserved); reserved.TargetReceipts.ShouldBeEmpty();
        (await fixture.Coordinator.ExecuteAsync(fixture.Payload, cancellationToken: TestContext.Current.CancellationToken)).Status.ShouldBe("ConsumptionReserved");
        fixture.Reservations.ShouldBe(1); fixture.DuplicatePhysicalReceiptIds = false;
        var completed = await fixture.Coordinator.ExecuteAsync(fixture.Payload, cancellationToken: TestContext.Current.CancellationToken);
        completed.Status.ShouldBe("CompletionSealed"); fixture.Reservations.ShouldBe(1); completed.Protection!.ReceiptId.ShouldBe(reserved.ReceiptId);
        completed.Protection.TargetReceipts.Select(receipt => receipt.ReceiptId).Distinct(StringComparer.Ordinal).Count().ShouldBe(2);
        fixture.State.Deletions.Single().Completed.ShouldBeTrue();
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(fixture.ProtectionState.CommittedState.Single().Value));
        var retained = document.RootElement.GetProperty("Batches").EnumerateArray().Single();
        retained.GetProperty("Original").Deserialize<DeletionBatchConsumptionRequest>()!.Capability.ShouldBe(fixture.Payload);
        var outcome = retained.GetProperty("Outcome").Deserialize<DeletionConsumptionOutcome>()!;
        outcome.Status.ShouldBe(DeletionConsumptionStatus.Consumed); outcome.ReceiptId.ShouldBe(reserved.ReceiptId); outcome.TargetReceipts.ShouldBe(completed.Protection.TargetReceipts);
    }
    /// <summary>Only bounded pure factory acquisition may precede owner calls; late factory completion resumes no protocol phase.</summary>
    [Theory]
    [InlineData(false, false)][InlineData(false, true)][InlineData(true, false)][InlineData(true, true)]
    public async Task SuspendedOwnerFactoryCannotRetainOrResumeCoordinator(bool protectionFactory, bool cancellation)
    {
        using var f = new DeletionBatchExecutionFixture(); var (clock, advance) = PrivateOwnerDeadlineTestClock.Create(); using var caller = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        void Suspend() { entered.TrySetResult(); release.Wait(); finished.TrySetResult(); }
        var coordinator = new DeletionBatchExecutionCoordinator(clock, new EventStoreDeletionBatchGuardPort(f.Source),
            payload => { payload.ShouldBe(f.Payload); if (!protectionFactory) { Suspend(); } return f.Signer; },
            tenant => { tenant.ShouldBe(f.Payload.TenantId); if (protectionFactory) { Suspend(); } return f.Protection; });
        var pending = Task.Run(() => coordinator.ExecuteAsync(f.Payload, cancellationToken: caller.Token), CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        try
        {
            if (cancellation) { caller.Cancel(); var error = await Should.ThrowAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken)); error.CancellationToken.ShouldBe(caller.Token); }
            else { advance(TimeSpan.FromSeconds(30)); (await pending.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken)).Status.ShouldBe("Unavailable"); }
            int signatures = f.Signatures; int mutations = f.SourceMutations; int calls = f.Protection.ReceivedCalls().Count();
            release.Set(); await finished.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            f.Signatures.ShouldBe(signatures); f.SourceMutations.ShouldBe(mutations); f.Protection.ReceivedCalls().Count().ShouldBe(calls); f.Reservations.ShouldBe(0);
        }
        finally { release.Set(); }
    }

    /// <summary>An issued replacement revoked before or after dispatch retains the original block and reconciles its ordinal without consuming.</summary>
    [Theory]
    [InlineData(false, false)][InlineData(false, true)][InlineData(true, false)][InlineData(true, true)]
    public async Task ActualIssuedReplacementRevocationCanReconcileAndAdvanceHealthyNext(bool dispatched, bool loseAcknowledgement)
    {
        using var f = new DeletionBatchActualOwnerFixture(); var port = new EventStoreDeletionBatchGuardPort(f.Guard);
        var first = await f.Signer(f.Payload).SignAsync(f.Payload); await port.IssueAsync(first, false, TestContext.Current.CancellationToken); await port.DispatchAsync(first, TestContext.Current.CancellationToken);
        var batch = f.State.Deletions.Single().Batches.Single();
        await f.ProtectionActor.RegisterAsync(new(f.Payload, first.DetachedJws!, batch.IssuedGuardRevision, batch.DispatchReceiptId, batch.DispatchGuardRevision,
            batch.Targets.Select(t => new ProtectionTarget(t.TenantId, t.AgentInteractionId, t.TargetProtectionKeyAlias)).ToArray()));
        var auth = Substitute.For<IDeletionCapabilityRevocationAuthenticator>(); var now = DateTimeOffset.UtcNow;
        auth.VerifyAsync(Arg.Any<DeletionCapabilityRevocationEnvelope>(), "source-proof", Arg.Any<CancellationToken>()).Returns(call => new DeletionCapabilityRevocationAuthorization(call.Arg<DeletionCapabilityRevocationEnvelope>(), "independent-source", now.AddSeconds(-1), now.AddMinutes(1)));
        var subscriber = new DeletionCapabilityRevocationSubscriber(new("issuer-a", "protection-a", "tenant-a"), auth, f.Registrar, new EventStoreDeletionCapabilityGuardRevocationMirror(f.Guard), TimeProvider.System);
        var revocation = new DeletionCapabilityRevocationEnvelope("issuer-a", "protection-a", "tenant-a", "DeletionBatchCapabilitySigningKey", "key-a", 1, 1, "key-a-event", new string('A', 64));
        (await subscriber.ReceiveAsync(revocation, "source-proof", TestContext.Current.CancellationToken)).ShouldBeTrue();
        string block = f.State.Deletions.Single().Batches.Single().ProtectionReceiptId;
        var replacement = f.Payload with { AttestationOrdinal = 2, CapabilityKeyVersion = "key-b", IntendedIssuedGuardRevision = f.State.Revision };
        var second = await f.Signer(replacement).SignAsync(replacement); (await port.IssueAsync(second, true, TestContext.Current.CancellationToken))!.Status.ShouldBe("Committed");
        if (dispatched) { (await port.DispatchAsync(second, TestContext.Current.CancellationToken))!.Status.ShouldBe("Committed"); }
        (await subscriber.ReceiveAsync(revocation with { KeyVersion = "key-b", EventIdentity = "key-b-event", SignatureDigest = new string('B', 64) }, "source-proof", TestContext.Current.CancellationToken)).ShouldBeTrue();
        var pending = f.State.Deletions.Single().Batches.Single(); pending.ProtectionReceiptId.ShouldBe(block); pending.ProtectionOutcome.ShouldBe("ReplacementAwaitingActivation");
        await f.RestartSerializedOwnersAsync(); f.LoseBlockedReconciliationAcknowledgement = loseAcknowledgement;
        var result = await f.Coordinator.ExecuteAsync(replacement, true, TestContext.Current.CancellationToken);
        result.Status.ShouldBe("ActivationBlockedByReplacementKeyCompromise"); f.Reservations.ShouldBe(0);
        var retained = f.ReadProtectionBlockedReplacement(replacement.BatchId)!;
        retained.Original.Capability.ShouldBe(replacement); retained.Original.CompromiseBlockReceiptId.ShouldBe(block);
        retained.Original.CommittedIssuedGuardRevision.ShouldBe(pending.IssuedGuardRevision); retained.Outcome.ReceiptId.ShouldBe(result.Protection!.ReceiptId);
        await f.RestartSerializedOwnersAsync();
        var next = replacement with { AttestationOrdinal = 3, CapabilityKeyVersion = "key-c", IntendedIssuedGuardRevision = f.State.Revision };
        (await f.Coordinator.ExecuteAsync(next, true, TestContext.Current.CancellationToken)).Status.ShouldBe("CompletionSealed"); f.Reservations.ShouldBe(1); f.Signatures.ShouldBe(3);
        (await f.Coordinator.ExecuteAsync(next, true, TestContext.Current.CancellationToken)).Status.ShouldBe("CompletionSealed"); f.Reservations.ShouldBe(1); f.Signatures.ShouldBe(3);
    }

    /// <summary>Omitted/foreign/stale/forged independently supplied reconciliation proof cannot change either actual owner's original ledger.</summary>
    [Theory]
    [InlineData("omitted")][InlineData("foreign")][InlineData("stale")][InlineData("forged")][InlineData("foreign-block")][InlineData("foreign-issue")]
    public async Task InvalidBlockedReplacementProofPreservesOriginalLedgers(string vector)
    {
        using var f = new DeletionBatchActualOwnerFixture(); var port = new EventStoreDeletionBatchGuardPort(f.Guard);
        var first = await f.Signer(f.Payload).SignAsync(f.Payload); await port.IssueAsync(first, false, TestContext.Current.CancellationToken); await port.DispatchAsync(first, TestContext.Current.CancellationToken);
        var batch = f.State.Deletions.Single().Batches.Single();
        await f.ProtectionActor.RegisterAsync(new(f.Payload, first.DetachedJws!, batch.IssuedGuardRevision, batch.DispatchReceiptId, batch.DispatchGuardRevision,
            batch.Targets.Select(t => new ProtectionTarget(t.TenantId, t.AgentInteractionId, t.TargetProtectionKeyAlias)).ToArray()));
        var auth = Substitute.For<IDeletionCapabilityRevocationAuthenticator>(); var now = DateTimeOffset.UtcNow;
        auth.VerifyAsync(Arg.Any<DeletionCapabilityRevocationEnvelope>(), "source-proof", Arg.Any<CancellationToken>()).Returns(call => new DeletionCapabilityRevocationAuthorization(call.Arg<DeletionCapabilityRevocationEnvelope>(), "independent-source", now.AddSeconds(-1), now.AddMinutes(1)));
        var subscriber = new DeletionCapabilityRevocationSubscriber(new("issuer-a", "protection-a", "tenant-a"), auth, f.Registrar, new EventStoreDeletionCapabilityGuardRevocationMirror(f.Guard), TimeProvider.System);
        var revocation = new DeletionCapabilityRevocationEnvelope("issuer-a", "protection-a", "tenant-a", "DeletionBatchCapabilitySigningKey", "key-a", 1, 1, "key-a-event", new string('A', 64));
        (await subscriber.ReceiveAsync(revocation, "source-proof", TestContext.Current.CancellationToken)).ShouldBeTrue();
        string block = f.State.Deletions.Single().Batches.Single().ProtectionReceiptId;
        var replacement = f.Payload with { AttestationOrdinal = 2, CapabilityKeyVersion = "key-b", IntendedIssuedGuardRevision = f.State.Revision };
        var second = await f.Signer(replacement).SignAsync(replacement); (await port.IssueAsync(second, true, TestContext.Current.CancellationToken))!.Status.ShouldBe("Committed");
        (await subscriber.ReceiveAsync(revocation with { KeyVersion = "key-b", EventIdentity = "key-b-event", SignatureDigest = new string('B', 64) }, "source-proof", TestContext.Current.CancellationToken)).ShouldBeTrue();
        var pending = f.State.Deletions.Single().Batches.Single(); pending.ProtectionReceiptId.ShouldBe(block); pending.ProtectionOutcome.ShouldBe("ReplacementAwaitingActivation");

        var comparison = (await f.ProtectionActor.ReadActivationComparisonAsync("tenant-a", replacement.BatchId, replacement.CapabilityKeyVersion))!;
        var phase = new DeletionBlockedReplacementReconciliation("blocked-original", block, comparison.KeyBlockSetRevision, pending.IssueReceiptId,
            replacement, second.DetachedJws!, second.SigningRequestId, pending.IssuedGuardRevision,
            pending.Targets.Select(t => new ProtectionTarget(t.TenantId, t.AgentInteractionId, t.TargetProtectionKeyAlias)).ToArray(), comparison.ReplacementKeyRevocation!);
        phase = vector switch { "foreign" => phase with { Capability = phase.Capability with { DestructionSealId = "foreign-seal" }, SigningRequestId = DeletionBatchCapabilityIdentity.SigningRequestId(phase.Capability with { DestructionSealId = "foreign-seal" }) },
            "stale" => phase with { ExpectedKeyBlockSetRevision = phase.ExpectedKeyBlockSetRevision + 1 },
            "forged" => phase with { RevocationReceipt = phase.RevocationReceipt with { ReceiptId = "forged-proof" } },
            "foreign-block" => phase with { CompromiseBlockReceiptId = "foreign-block" }, "foreign-issue" => phase with { GuardReplacementReceiptId = "foreign-issue" }, _ => phase };
        if (vector == "omitted") { f.ProtectionAuthority.VerifyBlockedReplacementAsync(Arg.Any<DeletionBlockedReplacementReconciliation>(), Arg.Any<CancellationToken>()).Returns(false); }
        byte[] guardBefore = JsonSerializer.SerializeToUtf8Bytes(f.State); byte[] ownerBefore = JsonSerializer.SerializeToUtf8Bytes(f.ProtectionState.CommittedState.Single().Value);
        if (vector == "forged") { await Should.ThrowAsync<ArgumentException>(() => f.ProtectionActor.ReconcileBlockedReplacementAsync(phase)); }
        else { (await f.ProtectionActor.ReconcileBlockedReplacementAsync(phase)).Status.ShouldNotBe(DeletionConsumptionStatus.ActivationBlockedByReplacementKeyCompromise); }
        JsonSerializer.SerializeToUtf8Bytes(f.State).ShouldBe(guardBefore); JsonSerializer.SerializeToUtf8Bytes(f.ProtectionState.CommittedState.Single().Value).ShouldBe(ownerBefore);
        f.Reservations.ShouldBe(0); f.State.Deletions.Single().Batches.Single().ProtectionReceiptId.ShouldBe(block);
    }

}
