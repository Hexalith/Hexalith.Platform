using Hexalith.EventStore.Contracts.Security;
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
        original.Protection!.TargetReceipts.Count.ShouldBe(2); fixture.ProtectionState.CommittedState.ShouldNotBeEmpty();
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
}
