using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.EventStore.Server.Tests.Security;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Actual production guard owner/transaction and ERC-backed consumption actor. External eligibility/signing inputs are explicit component fixtures, not installed qualification evidence.</summary>
public sealed class ErasureRootUnissuedReplacementTests
{
    [Fact]
    public async Task RejectedUnissuedCandidateRetainsIssuedOrdinalExactRestoreLookupAndLaterHealthyProgress()
    {
        var token = TestContext.Current.CancellationToken;
        var guard = new GovernanceGuardFixture(); await guard.PrepareBoundAsync(seal: true);
        var owner = new ErasureRootConsumptionFixture(); _ = await owner.SeedAsync();
        owner.Authority.VerifyBlockedReplacementAsync(Arg.Any<DeletionBlockedReplacementReconciliation>(), Arg.Any<CancellationToken>()).Returns(true);
        var issued = guard.Attest(guard.Batch());
        var issue = await guard.Apply(guard.Command(GovernanceGuardOperation.RecordBatchIssued, batch: issued)); issue.Status.ShouldBe("Committed");
        (await guard.Apply(guard.Command(GovernanceGuardOperation.AuthorizeDispatch, batch: issued, authorization: "old-dispatch"))).Status.ShouldBe("Committed");
        var dispatch = await guard.Apply(guard.Command(GovernanceGuardOperation.CommitDispatch, batch: issued, authorization: "old-dispatch")); dispatch.Status.ShouldBe("Committed");
        var targets = issued.Targets.Select(target => new ProtectionTarget(target.TenantId, target.AgentInteractionId, target.TargetProtectionKeyAlias)).ToArray();
        var request = new DeletionBatchConsumptionRequest(issued.Capability!, issued.DetachedJws, issue.GuardHighWater, dispatch.ReceiptId, dispatch.GuardHighWater, targets);
        (await owner.Invoke(() => owner.Actor.RegisterAsync(request))).Status.ShouldBe(DeletionConsumptionStatus.Unconsumed);
        async Task<DeletionCapabilityRevocationReceipt> Compromise(string key)
        {
            var revocation = (await owner.Invoke(() => owner.Actor.RegisterRevocationAsync(ErasureRootConsumptionFixture.Revocation(key))))!;
            var batch = new GovernanceBatchCommand("", "", 0, [], "", 0, key, "", "", "", [], revocation.KeyBlockSetRevision);
            var command = guard.Command(GovernanceGuardOperation.RecordKeyCompromise, batch: batch) with { RevocationReceipt = revocation };
            (await guard.Apply(command)).Status.ShouldBe("Committed"); return revocation;
        }
        var predecessorBlock = await Compromise(issued.CapabilityKeyVersion);
        var predecessorOutcome = await owner.Invoke(() => owner.Actor.LookupAsync("tenant-a", request.Capability.BatchId));
        var rejected = guard.Attest(issued with { AttestationOrdinal = 2, CapabilityKeyVersion = "rejected-key-v2", BlockSetRevision = predecessorBlock.KeyBlockSetRevision });
        var replacementBlock = await Compromise(rejected.CapabilityKeyVersion);
        object staleOwner = owner.CaptureOwner();
        var attempt = guard.Command(GovernanceGuardOperation.ReplaceAttestation, batch: rejected) with { ExpectedGuardRevision = rejected.Capability!.IntendedIssuedGuardRevision };
        var refusal = await guard.Apply(attempt); refusal.Status.ShouldBe("Stale");
        var proof = new GovernanceRejectedIssueOriginal(attempt, ErasureRootCustody.Hash(Array.Empty<GuardedStateMutation>()), refusal, guard.State.Revision, ErasureRootCustody.Hash(guard.State))
            { CurrentGuardStateBytes = JsonSerializer.SerializeToUtf8Bytes(guard.State) };
        var original = new DeletionBlockedReplacementReconciliation("rejected-unissued-owner-operation", predecessorOutcome.ReceiptId!, replacementBlock.KeyBlockSetRevision,
            refusal.ReceiptId, rejected.Capability!, rejected.DetachedJws, rejected.SigningRequestId, 0, targets, replacementBlock) { UnissuedProof = proof };
        var blocked = await owner.Invoke(() => owner.Actor.ReconcileBlockedReplacementAsync(original));
        blocked.Status.ShouldBe(DeletionConsumptionStatus.ActivationBlockedByReplacementKeyCompromise);
        var retained = (await owner.Invoke(() => owner.Actor.ReadBlockedReplacementAsync(rejected.Capability!)))!;
        JsonSerializer.Serialize(retained.Original).ShouldBe(JsonSerializer.Serialize(original));
        var mirror = issued with { ProtectionOutcome = "UnissuedReplacementBlocked", ProtectionReceiptId = blocked.ReceiptId!, BlockSetRevision = blocked.KeyBlockSetRevision };
        guard.ChangeEvidence = (evidence, _) => evidence with { ProtectionBlockedReplacement = retained };
        (await guard.Apply(guard.Command(GovernanceGuardOperation.RecordProtectionOutcome, batch: mirror))).Status.ShouldBe("Committed");
        var unchanged = guard.State.Deletions.Single().Batches.Single(); unchanged.Capability.ShouldBe(issued.Capability); unchanged.AttestationOrdinal.ShouldBe(1);
        unchanged.RejectedUnissuedReplacement.ShouldNotBeNull();
        await owner.RestoreOwnerAsync(staleOwner);
        var restored = (await owner.Invoke(() => owner.Actor.ReadBlockedReplacementAsync(rejected.Capability!)))!;
        JsonSerializer.Serialize(restored).ShouldBe(JsonSerializer.Serialize(retained));
        var healthy = guard.Attest(issued with { AttestationOrdinal = 2, CapabilityKeyVersion = "healthy-key-v3", BlockSetRevision = replacementBlock.KeyBlockSetRevision });
        guard.ChangeEvidence = null;
        var healthyIssue = await guard.Apply(guard.Command(GovernanceGuardOperation.ReplaceAttestation, batch: healthy)); healthyIssue.Status.ShouldBe("Committed");
        (await guard.Apply(guard.Command(GovernanceGuardOperation.AuthorizeDispatch, batch: healthy, authorization: "healthy-dispatch"))).Status.ShouldBe("Committed");
        var healthyDispatch = await guard.Apply(guard.Command(GovernanceGuardOperation.CommitDispatch, batch: healthy, authorization: "healthy-dispatch")); healthyDispatch.Status.ShouldBe("Committed");
        var replacement = new DeletionBatchConsumptionRequest(healthy.Capability!, healthy.DetachedJws, healthyIssue.GuardHighWater, healthyDispatch.ReceiptId, healthyDispatch.GuardHighWater, targets);
        var activation = new DeletionReattestationActivation("healthy-activation", blocked.ReceiptId!, replacementBlock.KeyBlockSetRevision, healthyIssue.ReceiptId, replacement);
        var activated = await owner.Invoke(() => owner.Actor.ActivateAsync(activation)); activated.Status.ShouldBe(DeletionConsumptionStatus.Unconsumed);
        guard.ChangeEvidence = (evidence, _) => evidence with { ProtectionActivationRequest = activation, ProtectionActivationOutcome = activated };
        (await guard.Apply(guard.Command(GovernanceGuardOperation.RecordProtectionOutcome, batch: healthy with { ProtectionOutcome = "Activated", ProtectionReceiptId = activated.ReceiptId!, BlockSetRevision = activated.KeyBlockSetRevision }))).Status.ShouldBe("Committed");
        guard.State.Deletions.Single().Batches.Single().AttestationOrdinal.ShouldBe(2);
        guard.State.CompromisedKeyVersions.ShouldContain(issued.CapabilityKeyVersion); guard.State.CompromisedKeyVersions.ShouldContain(rejected.CapabilityKeyVersion);
        var historical = (await owner.Invoke(() => owner.Actor.ReadBlockedReplacementAsync(rejected.Capability!)))!;
        JsonSerializer.Serialize(historical).ShouldBe(JsonSerializer.Serialize(retained));
        (await owner.Store.HeadAsync("tenant-a")).Epoch.ShouldBe(1); (await owner.Store.HeadAsync("tenant-a")).Erased.ShouldBeEmpty();
    }
}
