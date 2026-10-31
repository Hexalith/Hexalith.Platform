using System.Text.Json;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>Real typed source owner/reducer with serialized modeled multi-cell CAS. Tests prove technical state transitions, never actual production backend/issuer/protection qualification.</summary>
public sealed class GovernanceScopeGuardTests
{
    /// <summary>Every concrete writer is fenced at repair, only its exact finite cohort work drains, and successor activation retains prior guard evidence while revoking the old epoch.</summary>
    [Theory]
    [InlineData(DirectoryWriteKind.Create)][InlineData(DirectoryWriteKind.ContentAppend)][InlineData(DirectoryWriteKind.Permit)]
    [InlineData(DirectoryWriteKind.CreationOutbox)][InlineData(DirectoryWriteKind.UserActionIntent)][InlineData(DirectoryWriteKind.LeaseAcquire)]
    [InlineData(DirectoryWriteKind.LeaseCommit)][InlineData(DirectoryWriteKind.RateAuthorization)][InlineData(DirectoryWriteKind.OpenAuthorization)]
    [InlineData(DirectoryWriteKind.BudgetAuthorization)][InlineData(DirectoryWriteKind.CapacityAuthorization)][InlineData(DirectoryWriteKind.OriginalRecoveryResult)]
    public async Task FiniteRepairFencesEveryWriterAndSuccessorPreservesExactHistory(DirectoryWriteKind kind)
    {
        var fixture = new GovernanceGuardFixture(); var owner = new AggregateIdentity("tenant-a", "directory", "directory-a");
        var cohort = new DirectoryRepairCohortItem(owner, "original-work", kind, "conversation-a", "permit-a", "effect-a", 1, "Authorized");
        var install = fixture.Command(GovernanceGuardOperation.InstallRepairFence);
        install = install with { Repair = new("tenant-a", install.OperationId, "epoch-a", install.ExpectedGuardRevision, "post-fence-checkpoint", "repair-revocation", [cohort]) };
        (await fixture.Apply(install)).Status.ShouldBe("Committed");
        var mutation = new GuardedStateMutation("source-a", 0, GuardedTransactionFixture.Hash([]), "sealed-content"u8.ToArray());
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.AppendWrite, write: fixture.Facts(kind)), [mutation])).Status.ShouldBe("Blocked");
        fixture.Backend.Stored.ContainsKey(fixture.Backend.Key("source-a")).ShouldBeFalse();
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.AppendWrite, write: fixture.Facts(kind), reference: "original-work") with { BridgeOriginal = new(owner, cohort.OperationId) }, [mutation])).Status.ShouldBe("Accepted");
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.ActivateSuccessor) with { EpochId = "epoch-b" })).Status.ShouldBe("Blocked");
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.RecordBridgeDrain, reference: "original-work") with { BridgeOriginal = new(owner, cohort.OperationId) })).Status.ShouldBe("Committed");
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.AppendWrite, write: fixture.Facts(kind), reference: "original-work") with { BridgeOriginal = new(owner, cohort.OperationId) }, [mutation with { CellId = "after-drain" }])).Status.ShouldBe("Blocked");
        fixture.Backend.Stored.ContainsKey(fixture.Backend.Key("after-drain")).ShouldBeFalse();
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.ActivateSuccessor) with { EpochId = "epoch-b" })).Status.ShouldBe("Committed");
        fixture.State.Repair.ShouldBeNull(); fixture.State.RepairHistory.Single().Cohort.Single().ShouldBe(cohort);
        var late = fixture.Command(GovernanceGuardOperation.AppendWrite, write: fixture.Facts(kind) with { EpochId = "epoch-a" }) with { EpochId = "epoch-a" };
        (await fixture.Apply(late, [mutation with { CellId = "late-source" }])).Status.ShouldBe("Blocked");
        fixture.Backend.Stored.ContainsKey(fixture.Backend.Key("late-source")).ShouldBeFalse();
    }

    /// <summary>The append guard stamps accepted source writes and later matching admission/content categories receive no source effect.</summary>
    [Theory]
    [InlineData(DirectoryWriteKind.Create)][InlineData(DirectoryWriteKind.ContentAppend)][InlineData(DirectoryWriteKind.Permit)]
    [InlineData(DirectoryWriteKind.CreationOutbox)][InlineData(DirectoryWriteKind.UserActionIntent)][InlineData(DirectoryWriteKind.LeaseAcquire)]
    [InlineData(DirectoryWriteKind.LeaseCommit)][InlineData(DirectoryWriteKind.RateAuthorization)][InlineData(DirectoryWriteKind.OpenAuthorization)]
    [InlineData(DirectoryWriteKind.BudgetAuthorization)][InlineData(DirectoryWriteKind.CapacityAuthorization)]
    public async Task AcceptedAttributionAndInstalledFencesAreJointWithEverySourceWrite(DirectoryWriteKind kind)
    {
        var fixture = new GovernanceGuardFixture(); await fixture.PrepareBoundAsync();
        fixture.OriginalAcceptance!.GuardHighWater.ShouldBe(2); fixture.OriginalAcceptance.AcceptedAtAdmissionFenceOrdinal.ShouldBe(0);
        var command = fixture.Command(GovernanceGuardOperation.AppendWrite, write: fixture.Facts(kind));
        long prior = fixture.State.Revision;
        (await fixture.Apply(command, [new("fenced-source", 0, GuardedTransactionFixture.Hash([]), "sealed-content"u8.ToArray())])).Status.ShouldBe("Blocked");
        fixture.State.Revision.ShouldBe(prior); fixture.Backend.Stored.ContainsKey(fixture.Backend.Key("fenced-source")).ShouldBeFalse();
    }

    /// <summary>Accepted writes never carry more scope attribution entries than a later original violation can validate.</summary>
    [Theory]
    [InlineData(1001, false)][InlineData(900, true)]
    public async Task OversizedAttributionVectorBlocksAppendBeforeSourceEffect(int count, bool longIds)
    {
        var fixture = new GovernanceGuardFixture();
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.AuthorizeAdmissionFence, scope: fixture.Scope, authorization: "seed-fence"))).Status.ShouldBe("Committed");
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.CommitAdmissionFence, scope: fixture.Scope, authorization: "seed-fence"))).Status.ShouldBe("Committed");
        var baseState = fixture.State; var template = baseState.Deletions.Single();
        var deletions = Enumerable.Range(1, count).Select(index => template with
        { RequestId = "deletion-" + index.ToString("D4", System.Globalization.CultureInfo.InvariantCulture) + (longIds ? new string('x', 80) : "") }).ToArray();
        var state = baseState with { Deletions = deletions };
        fixture.Backend.Stored[fixture.Backend.Key(fixture.Backend.Target.GuardCellId)] = JsonSerializer.SerializeToUtf8Bytes(
            new GuardedStateCell("tenant-a", fixture.Backend.Target.InstallationId, fixture.Backend.Target.GuardCellId, state.Revision, JsonSerializer.SerializeToUtf8Bytes(state)));
        var mutation = new GuardedStateMutation("oversized-attribution-source", 0, GuardedTransactionFixture.Hash([]), "sealed-content"u8.ToArray());
        var result = await fixture.Owner.ExecuteAsync(fixture.Command(GovernanceGuardOperation.AppendWrite, write: fixture.Facts()), [mutation], TestContext.Current.CancellationToken);
        result.ShouldNotBeNull(); result.Status.ShouldBe("Blocked");
        fixture.Backend.Stored.ContainsKey(fixture.Backend.Key(mutation.CellId)).ShouldBeFalse();
        fixture.State.Revision.ShouldBe(state.Revision); fixture.State.Deletions.Count.ShouldBe(count);
    }

    /// <summary>A serialized deletion without an installed admission revision is refused at state ingress before lookup or append effects.</summary>
    [Fact]
    public async Task MissingAdmissionFenceRevisionRefusesGuardStateAtIngress()
    {
        var fixture = new GovernanceGuardFixture();
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.AuthorizeAdmissionFence, scope: fixture.Scope, authorization: "seed-fence"))).Status.ShouldBe("Committed");
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.CommitAdmissionFence, scope: fixture.Scope, authorization: "seed-fence"))).Status.ShouldBe("Committed");
        var state = fixture.State with { Deletions = [fixture.State.Deletions.Single() with { AdmissionFenceGuardRevision = 0 }] };
        fixture.Backend.Stored[fixture.Backend.Key(fixture.Backend.Target.GuardCellId)] = JsonSerializer.SerializeToUtf8Bytes(
            new GuardedStateCell("tenant-a", fixture.Backend.Target.InstallationId, fixture.Backend.Target.GuardCellId, state.Revision, JsonSerializer.SerializeToUtf8Bytes(state)));
        (await fixture.Owner.ReadAsync("tenant-a", TestContext.Current.CancellationToken)).ShouldBeNull();
        fixture.Backend.Authority.ClearReceivedCalls();
        var mutation = new GuardedStateMutation("legacy-source", 0, GuardedTransactionFixture.Hash([]), "sealed-content"u8.ToArray());
        (await fixture.Owner.ExecuteAsync(fixture.Command(GovernanceGuardOperation.AppendWrite, write: fixture.Facts()), [mutation], TestContext.Current.CancellationToken)).ShouldBeNull();
        await fixture.Backend.Authority.DidNotReceiveWithAnyArgs().AuthorizeLookupAsync(default!, default!, default!, default);
        fixture.Backend.Stored.ContainsKey(fixture.Backend.Key(mutation.CellId)).ShouldBeFalse();
        GovernanceScopeGuardReducer.Reduce(state, fixture.Command(GovernanceGuardOperation.AppendWrite, write: fixture.Facts()),
            new GovernanceGuardEvidence("tenant-a", "intent", "target", "authority", "receipt", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1), [], [], [], "writer", "revocation", "zero", 1, "Open", "", [], ""), "intent").Receipt.Status.ShouldBe("Unavailable");
    }

    /// <summary>Scope, owner-obligation and candidate cut/token substitutions cannot reuse an authorization at the identical guard revision.</summary>
    [Theory]
    [InlineData("scope")][InlineData("owners")][InlineData("obligations")][InlineData("cut")][InlineData("token")]
    public async Task AuthorizationCannotChangeItsExactEffectBasis(string vector)
    {
        var fixture = new GovernanceGuardFixture(); GovernanceGuardTransition effect;
        if (vector is "cut" or "token")
        {
            await fixture.PrepareBoundAsync(); var deletion = fixture.State.Deletions.Single(); var latest = deletion.ContentBindings.Single();
            (await fixture.Apply(fixture.Command(GovernanceGuardOperation.RecordViolation, ordinal: fixture.Ordinal(violation: "recut", invalidated: [latest.GlobalCutId, latest.TokenId])))).Status.ShouldBe("Committed");
            await fixture.EffectiveAsync(); var ordinal = fixture.Ordinal(2, "cut-b", "token-b");
            (await fixture.Apply(fixture.Command(GovernanceGuardOperation.AuthorizeContentBinding, ordinal: ordinal, authorization: "binding-b"))).Status.ShouldBe("Committed");
            effect = fixture.Command(GovernanceGuardOperation.CommitContentBinding, ordinal: ordinal with { GlobalCutId = vector == "cut" ? "substituted-cut" : ordinal.GlobalCutId,
                TokenId = vector == "token" ? "substituted-token" : ordinal.TokenId }, authorization: "binding-b");
        }
        else
        {
            (await fixture.Apply(fixture.Command(GovernanceGuardOperation.AuthorizeAdmissionFence, scope: fixture.Scope, authorization: "admission-a"))).Status.ShouldBe("Committed");
            effect = fixture.Command(GovernanceGuardOperation.CommitAdmissionFence, scope: vector == "scope" ? fixture.Scope with { SourceConversationId = "other-conversation" } : fixture.Scope, authorization: "admission-a");
            if (vector == "owners") { fixture.ChangeEvidence = (proof, _) => proof with { RequiredOwnerIds = ["substituted-owner"] }; }
            if (vector == "obligations") { fixture.ChangeEvidence = (proof, _) => proof with { ObligationIds = ["substituted-obligation"] }; }
        }
        long prior = fixture.State.Revision;
        (await fixture.Apply(effect)).Status.ShouldBe("Blocked"); fixture.State.Revision.ShouldBe(prior);
    }

    /// <summary>Pre-cut explicit no-cut recut and later complete gap chain preserve prior immutable Effective cycles; overtaken authorization terminalizes without a guard append.</summary>
    [Fact]
    public async Task RepeatedRecutsRetainViolationsAndContinuousBindingWithObsoleteOriginalOutcome()
    {
        var fixture = new GovernanceGuardFixture(); await fixture.PrepareBoundAsync();
        var firstBinding = fixture.State.Deletions.Single().ContentBindings.Single(); var firstCycles = fixture.State.Deletions.Single().OwnerCycles.ToArray();
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.RecordViolation, ordinal: fixture.Ordinal(violation: "violation-a", invalidated: [firstBinding.GlobalCutId, firstBinding.TokenId])))).Status.ShouldBe("Committed");
        await fixture.EffectiveAsync(); var second = fixture.Ordinal(2, "cut-b", "token-b");
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.AuthorizeContentBinding, ordinal: second, authorization: "binding-b"))).Status.ShouldBe("Committed");
        var overtaken = fixture.Command(GovernanceGuardOperation.CommitContentBinding, ordinal: second, authorization: "binding-b");
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.RecordViolation, ordinal: fixture.Ordinal(2, violation: "violation-b", resource: "resource-a", invalidated: [firstBinding.GlobalCutId, firstBinding.TokenId])))).Status.ShouldBe("Committed");
        long beforeObsolete = fixture.State.Revision;
        var original = await fixture.Apply(overtaken); original.Status.ShouldBe("Obsolete"); fixture.State.Revision.ShouldBe(beforeObsolete);
        await fixture.EffectiveAsync(); await fixture.BindAsync("binding-c", "cut-c", "token-c");
        var current = fixture.State.Deletions.Single(); current.Ordinal.ShouldBe(3); current.Violations.Count.ShouldBe(2);
        current.ContentBindings.Count.ShouldBe(2); current.ContentBindings.Last().PredecessorBindingId.ShouldBe(firstBinding.BindingId);
        current.OwnerCycles.Take(2).ShouldBe(firstCycles);
        fixture.DenyEffects = true;
        (await new GovernanceScopeGuardOwner(fixture.Backend.Owner, TimeProvider.System, fixture.Authority).ExecuteAsync(overtaken, [], TestContext.Current.CancellationToken)).ShouldBe(original);
    }

    /// <summary>Hold registration wins after barrier authorization; stale compare is immutable, Open precedence blocks reauthorization, and exact acknowledged release permits a new irreversible seal.</summary>
    [Fact]
    public async Task HoldAndSealSerializeAtConditionalEffectWithExactStaleLookup()
    {
        var fixture = new GovernanceGuardFixture(); await fixture.PrepareBoundAsync(); var batch = fixture.Batch();
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.AuthorizeDestructionStart, batch: batch, authorization: "seal-a"))).Status.ShouldBe("Committed");
        var stale = fixture.Command(GovernanceGuardOperation.CommitDestructionStart, batch: batch, authorization: "seal-a");
        var hold = new GovernanceHoldCommand("hold-a", fixture.Scope, "policy-a", "");
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.RegisterHold, hold: hold))).Status.ShouldBe("Committed");
        long prior = fixture.State.Revision; fixture.Backend.LoseAcknowledgement = true;
        var result = await fixture.Apply(stale); result.Status.ShouldBe("Stale"); fixture.State.Revision.ShouldBe(prior); fixture.State.Deletions.Single().SealId.ShouldBeEmpty();
        (await fixture.Apply(stale)).ShouldBe(result);
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.AuthorizeDestructionStart, batch: batch, authorization: "seal-blocked"))).Status.ShouldBe("Blocked");
        hold = hold with { ReleaseReceiptId = "fully-acknowledged-release" };
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.ReleaseHold, hold: hold))).Status.ShouldBe("Committed");
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.AuthorizeDestructionStart, batch: batch, authorization: "seal-b"))).Status.ShouldBe("Committed");
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.CommitDestructionStart, batch: batch, authorization: "seal-b"))).Status.ShouldBe("Committed");
        fixture.State.Deletions.Single().SealId.ShouldNotBeEmpty();
    }

    /// <summary>A complete post-seal content target gains one singleton containment batch; repeated resources link to coverage, all-target outcomes permit conditional completion.</summary>
    [Fact]
    public async Task PostSealContentContainmentCanCompleteWithoutReopeningAcceptedOwnerCycles()
    {
        var fixture = new GovernanceGuardFixture(); await fixture.PrepareBoundAsync(seal: true);
        var acceptedCycles = fixture.State.Deletions.Single().OwnerCycles.ToArray(); var obligations = fixture.State.Deletions.Single().ObligationIds.ToArray();
        await fixture.IssueConsumeAsync(fixture.Batch(), "accepted");
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.RecordViolation, ordinal: fixture.Ordinal(violation: "content-a", resource: "resource-a")))).Status.ShouldBe("Committed");
        var containment = fixture.Batch("Containment", 1, [fixture.ViolationTarget]);
        var substituted = containment with { Targets = [fixture.ViolationTarget with { TargetProtectionKeyAlias = "foreign-alias" }] };
        substituted = substituted with { ManifestDigest = GovernanceScopeGuardReducer.ManifestDigest(substituted.Targets) };
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.AuthorizeContainment, batch: substituted, authorization: "wrong-target", reference: "resource-a"))).Status.ShouldBe("Blocked");
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.AuthorizeContainment, batch: containment, authorization: "containment-a", reference: "resource-a"))).Status.ShouldBe("Committed");
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.CommitContainment, batch: containment, authorization: "containment-a"))).Status.ShouldBe("Committed");
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.AuthorizeCompletion, authorization: "incomplete"))).Status.ShouldBe("Blocked");
        await fixture.IssueConsumeAsync(containment, "containment");
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.RecordViolation, ordinal: fixture.Ordinal(violation: "content-b", resource: "resource-b")))).Status.ShouldBe("Committed");
        var linked = await fixture.Apply(fixture.Command(GovernanceGuardOperation.AuthorizeContainment, batch: containment, authorization: "duplicate-target", reference: "resource-b"));
        linked.Status.ShouldBe("CoverageLinked"); linked.ReferenceId.ShouldBe(containment.BatchId);
        fixture.State.Deletions.Single().Batches.Count.ShouldBe(2); fixture.State.Deletions.Single().OwnerCycles.ShouldBe(acceptedCycles);
        fixture.State.Deletions.Single().ObligationIds.ShouldBe(obligations);
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.AuthorizeCompletion, authorization: "complete-a"))).Status.ShouldBe("Committed");
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.CommitCompletion, authorization: "complete-a"))).Status.ShouldBe("CompletionSealed");
        fixture.State.Deletions.Single().Completed.ShouldBeTrue();
    }

    /// <summary>Emergency block forbids old-key dispatch; exact blocked same-batch replacement receives successor dispatch and expected-block activation before all-target completion.</summary>
    [Fact]
    public async Task CompromiseAndReplacementRemainExactSameBatchWithoutClearingGlobalBlock()
    {
        var fixture = new GovernanceGuardFixture(); await fixture.PrepareBoundAsync(seal: true); var batch = fixture.Attest(fixture.Batch());
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.RecordBatchIssued, batch: batch))).Status.ShouldBe("Committed");
        var revocation = new DeletionCapabilityRevocationReceipt(new("qualified-issuer", "protection-audience", "tenant-a", "DeletionBatchCapabilitySigningKey", "healthy-key-a", 1, 1, "revocation-a", new string('A', 64)), 1, 1, "exact-protection-block", [batch.BatchId]);
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.RecordKeyCompromise, batch: batch with { BlockSetRevision = 1 }) with { RevocationReceipt = revocation })).Status.ShouldBe("Committed");
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.AuthorizeDispatch, batch: batch, authorization: "old-dispatch"))).Status.ShouldBe("Blocked");
        var replacement = fixture.Attest(batch with { AttestationOrdinal = 2, CapabilityKeyVersion = "healthy-key-b", BlockSetRevision = 1 });
        var replaced = await fixture.Apply(fixture.Command(GovernanceGuardOperation.ReplaceAttestation, batch: replacement));
        replaced.Status.ShouldBe("Committed"); replaced.ReferenceId.ShouldBe(replacement.BatchId);
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.AuthorizeDispatch, batch: replacement, authorization: "new-dispatch"))).Status.ShouldBe("Committed");
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.CommitDispatch, batch: replacement, authorization: "new-dispatch"))).Status.ShouldBe("Committed");
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.RecordProtectionOutcome, batch: replacement with { ProtectionOutcome = "Activated", ProtectionReceiptId = "activation-a" }))).Status.ShouldBe("Committed");
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.RecordProtectionOutcome, batch: replacement with { ProtectionOutcome = "Consumed", ProtectionReceiptId = "destroyed-a", TargetReceiptIds = ["target-a"] }))).Status.ShouldBe("Committed");
        fixture.State.CompromisedKeyVersions.ShouldContain("healthy-key-a"); fixture.State.Deletions.Single().Batches.Single().BatchId.ShouldBe(batch.BatchId);
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.AuthorizeCompletion, authorization: "complete-a"))).Status.ShouldBe("Committed");
        var completion = fixture.Command(GovernanceGuardOperation.CommitCompletion, authorization: "complete-a");
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.RegisterHold, hold: new("later-hold", fixture.Scope, "policy-a", "")))).Status.ShouldBe("Committed");
        fixture.State.Holds.Single().PostStartAttempt.ShouldBeTrue();
        (await fixture.Apply(completion)).Status.ShouldBe("Stale"); fixture.State.Deletions.Single().Completed.ShouldBeFalse();
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.AuthorizeCompletion, authorization: "complete-open"))).Status.ShouldBe("Blocked");
    }

    /// <summary>An attributed accepted admission after sealing requires owner block evidence, records integrity compromise and permanently excludes completion or new containment batches.</summary>
    [Fact]
    public async Task PostSealAdmissionCannotBecomeContentContainmentOrSuccessfulCompletion()
    {
        var fixture = new GovernanceGuardFixture(); await fixture.PrepareBoundAsync(seal: true, kind: DirectoryWriteKind.Permit);
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.RecordViolation, ordinal: fixture.Ordinal(violation: "admission-breach", kind: "Admission")))).Status.ShouldBe("Committed");
        fixture.State.Deletions.Single().IntegrityCompromised.ShouldBeTrue();
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.AuthorizeCompletion, authorization: "complete-a"))).Status.ShouldBe("Blocked");
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.AuthorizeContainment, batch: fixture.Batch("Containment", 1, [fixture.ViolationTarget]), authorization: "containment-a", reference: "resource-b"))).Status.ShouldBe("Blocked");
    }

    /// <summary>A hostile false Count cannot reach authority or transaction; unsupported and cross-tenant source facts also fail before private lookup.</summary>
    [Fact]
    public async Task MalformedScopeAndFalseCountNeverReachPrivateAuthorityOrPersistence()
    {
        var fixture = new GovernanceGuardFixture(); var write = fixture.Command(GovernanceGuardOperation.AppendWrite, write: fixture.Facts());
        (await fixture.Owner.ExecuteAsync(write, new FalseCountGuardMutations(), TestContext.Current.CancellationToken)).ShouldBeNull();
        (await fixture.Owner.ExecuteAsync(write with { Write = fixture.Facts() with { FirstEventSourceConversationId = "foreign-conversation" } }, [], TestContext.Current.CancellationToken)).ShouldBeNull();
        (await fixture.Owner.ExecuteAsync(fixture.Command(GovernanceGuardOperation.AuthorizeAdmissionFence, scope: fixture.Scope with { Kind = "ClassUtcRange" }, authorization: "unsupported"), [], TestContext.Current.CancellationToken)).ShouldBeNull();
        (await fixture.Owner.ExecuteAsync(write with { OperationId = "invalid-\uD800" }, [], TestContext.Current.CancellationToken)).ShouldBeNull();
        (await fixture.Owner.ExecuteAsync(fixture.Command(GovernanceGuardOperation.AuthorizeAdmissionFence, scope: fixture.Scope with { SourceConversationId = "invalid-\uD800" }, authorization: "malformed"), [], TestContext.Current.CancellationToken)).ShouldBeNull();
        Should.Throw<ArgumentException>(() => GovernanceScopeGuardReducer.PredicateDigest(fixture.Scope with { SourceConversationId = "invalid-\uD800" }));
        GovernanceScopeGuardReducer.PredicateDigest(fixture.Scope with { SourceConversationId = "caf\u00e9" }).ShouldNotBe(GovernanceScopeGuardReducer.PredicateDigest(fixture.Scope with { SourceConversationId = "cafe\u0301" }));
        fixture.Authority.ReceivedCalls().ShouldBeEmpty(); fixture.Backend.TransactionCalls.ShouldBe(0);
        (await new GovernanceScopeGuardOwner(fixture.Backend.Owner, TimeProvider.System).ExecuteAsync(write, [], TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    /// <summary>The first tokenless recut requires the exact no-cut receipt, carries prior obligations and certifies its complete gap before the first stable content binding.</summary>
    [Fact]
    public async Task FirstTokenlessViolationRequiresExactNoCutProofAndNewEffectiveCycles()
    {
        var fixture = new GovernanceGuardFixture();
        fixture.OriginalAcceptance = await fixture.Apply(fixture.Command(GovernanceGuardOperation.AppendWrite, write: fixture.Facts()), [new("original-source", 0, GuardedTransactionFixture.Hash([]), "sealed"u8.ToArray())]);
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.AuthorizeAdmissionFence, scope: fixture.Scope, authorization: "admission-a"))).Status.ShouldBe("Committed");
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.CommitAdmissionFence, scope: fixture.Scope, authorization: "admission-a"))).Status.ShouldBe("Committed");
        var tokenless = fixture.Ordinal(violation: "tokenless-a", noCut: "");
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.RecordViolation, ordinal: tokenless))).Status.ShouldBe("Blocked");
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.RecordViolation, ordinal: tokenless with { NoCutReceiptId = "wrong-no-cut" }))).Status.ShouldBe("Blocked");
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.RecordViolation, ordinal: tokenless with { NoCutReceiptId = "authenticated-no-cut" }))).Status.ShouldBe("Committed");
        fixture.State.Deletions.Single().Ordinal.ShouldBe(2); fixture.State.Deletions.Single().ContentBindings.ShouldBeEmpty();
        await fixture.EffectiveAsync(); await fixture.BindAsync("first-binding", "first-cut", "first-token");
        fixture.State.Deletions.Single().ContentBindings.Single().Ordinal.ShouldBe(2);
        fixture.State.Deletions.Single().Violations.Single().NoCutReceiptId.ShouldBe("authenticated-no-cut");
    }

    /// <summary>The actual shared canonical identity binds every issued artifact field and separates intended from successful issue revision.</summary>
    [Theory]
    [InlineData("tenant")][InlineData("seal")][InlineData("manifest")][InlineData("request")][InlineData("attempt")][InlineData("revision")]
    [InlineData("issuer")][InlineData("audience")][InlineData("guard")][InlineData("key")][InlineData("attestation")][InlineData("batch-kind")][InlineData("batch-ordinal")][InlineData("batch-id")]
    public async Task IssuanceRejectsChangedCanonicalCapabilityCorrelation(string vector)
    {
        var fixture = new GovernanceGuardFixture(); await fixture.PrepareBoundAsync(seal: true); var valid = fixture.Attest(fixture.Batch()); var payload = valid.Capability!;
        var changed = vector switch { "tenant" => payload with { TenantId = "tenant-b" }, "seal" => payload with { DestructionSealId = "wrong-seal" },
            "manifest" => payload with { ManifestDigest = GovernanceGuardFixture.Hash("wrong-manifest") }, "request" => payload with { DeletionRequestId = "wrong-request" },
            "attempt" => payload with { SigningAttemptOrdinal = 0 }, "issuer" => payload with { Issuer = "foreign-issuer" }, "audience" => payload with { Audience = "foreign-audience" },
            "guard" => payload with { GuardStreamId = "foreign-guard" }, "key" => payload with { CapabilityKeyVersion = "foreign-key" }, "attestation" => payload with { AttestationOrdinal = 2 },
            "batch-kind" => payload with { BatchKind = "Containment" }, "batch-ordinal" => payload with { BatchOrdinal = 1 }, "batch-id" => payload with { BatchId = "foreign-batch" },
            _ => payload with { IntendedIssuedGuardRevision = payload.IntendedIssuedGuardRevision + 1 } };
        string signingId = vector == "attempt" ? valid.SigningRequestId : DeletionBatchCapabilityIdentity.SigningRequestId(changed);
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.RecordBatchIssued, batch: valid with { Capability = changed, SigningRequestId = signingId }))).Status.ShouldBe("Blocked");
        long intended = valid.Capability!.IntendedIssuedGuardRevision;
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.RecordBatchIssued, batch: valid))).Status.ShouldBe("Committed");
        fixture.State.Deletions.Single().Batches.Single().IssuedGuardRevision.ShouldBe(intended + 1);
    }
    /// <summary>Both caller substitutions are denied against independently authenticated concrete writer categories, without altering sealed owner state.</summary>
    [Theory]
    [InlineData(DirectoryWriteKind.Permit, "Content")][InlineData(DirectoryWriteKind.ContentAppend, "Admission")]
    public async Task ViolationClassificationCannotSubstituteTheIndependentWriteKind(DirectoryWriteKind kind, string substituted)
    {
        var f = new GovernanceGuardFixture(); await f.PrepareBoundAsync(seal: true, kind: kind);
        string before = JsonSerializer.Serialize(f.State);
        var denied = f.Command(GovernanceGuardOperation.RecordViolation, ordinal: f.Ordinal(violation: "substitution", kind: substituted));
        (await f.Apply(denied)).Status.ShouldBe("Blocked"); JsonSerializer.Serialize(f.State).ShouldBe(before);
        string actual = kind == DirectoryWriteKind.Permit ? "Admission" : "Content";
        var original = await f.Apply(f.Command(GovernanceGuardOperation.RecordViolation, ordinal: f.Ordinal(violation: "original", kind: actual)));
        original.Status.ShouldBe("Committed");
        f.State.Deletions.Single().Violations.Single().Kind.ShouldBe(actual);
        f.State.Deletions.Single().IntegrityCompromised.ShouldBe(actual == "Admission");
    }

    /// <summary>A genuine committed acceptance cannot prove another operation/resource/facts/vector, and missing legacy correlation is unavailable; exact original correlation survives restart.</summary>
    [Theory]
    [InlineData("operation")][InlineData("resource")][InlineData("facts")][InlineData("mutation")][InlineData("missing")]
    public async Task ViolationRequiresItsOwnExactCommittedAcceptanceProvenance(string changed)
    {
        var f = new GovernanceGuardFixture(); await f.PrepareBoundAsync(seal: true);
        var accepted = f.OriginalAcceptance!; var foreign = f.State.Receipts.Single(r => r.AcceptedWriteResourceId == "resource-a"); accepted.AcceptedWriteResourceId.ShouldBe("resource-b"); accepted.AcceptedWriteFacts.ShouldBe(f.Facts());
        accepted.AcceptedTargetMutationDigest.Length.ShouldBe(64);
        f.State.Receipts.Single(r => r.OperationId == accepted.OperationId).ShouldBe(accepted);
        string before = JsonSerializer.Serialize(f.State);
        f.ChangeEvidence = (proof, command) => command.Operation != GovernanceGuardOperation.RecordViolation ? proof : changed switch
        {
            "operation" => proof with { ViolationAcceptanceOperationId = f.State.Receipts.Single(r => r.AcceptedWriteResourceId == "resource-a").OperationId },
            "resource" => proof with { OriginalAcceptance = foreign, ViolationAcceptanceOperationId = foreign.OperationId, ViolationWriteFacts = foreign.AcceptedWriteFacts, ViolationTargetMutationDigest = foreign.AcceptedTargetMutationDigest },
            "facts" => proof with { ViolationWriteFacts = proof.ViolationWriteFacts! with { PermitId = "other-authentic-permit" } },
            "mutation" => proof with { ViolationTargetMutationDigest = new string('F', 64) },
            _ => proof with { OriginalAcceptance = accepted with { AcceptedWriteFacts = null, AcceptedWriteResourceId = "", AcceptedTargetMutationDigest = "" } },
        };
        var proposed = f.Ordinal(violation: "foreign-original");
        if (changed == "resource") { proposed = proposed with { AcceptedAtOrdinal = foreign.AcceptedAtAdmissionFenceOrdinal, AcceptedAtGuardHighWater = foreign.GuardHighWater }; }
        (await f.Apply(f.Command(GovernanceGuardOperation.RecordViolation, ordinal: proposed))).Status.ShouldBe("Blocked");
        JsonSerializer.Serialize(f.State).ShouldBe(before);
        f.ChangeEvidence = null;
        var request = f.Command(GovernanceGuardOperation.RecordViolation, ordinal: f.Ordinal(violation: "exact-original"));
        var original = await f.Apply(request); original.Status.ShouldBe("Committed");
        var restarted = new GovernanceScopeGuardOwner(f.Backend.Owner, TimeProvider.System, f.Authority);
        (await restarted.ExecuteAsync(request, [], TestContext.Current.CancellationToken)).ShouldBe(original);
        f.State.Receipts.Single(r => r.OperationId == accepted.OperationId).ShouldBe(accepted);
    }


    /// <summary>Two authoritative owners may share an operation ID; serialized guard state keeps their bridge/drain/activation and original lookup independent.</summary>
    [Fact]
    public async Task SameOperationAcrossOwnersRequiresBothExactOriginalDrains()
    {
        var f = new GovernanceGuardFixture();
        var ownerA = new AggregateIdentity("tenant-a", "directory", "directory-a");
        var ownerB = new AggregateIdentity("tenant-a", "directory", "directory-b");
        var first = new DirectoryRepairCohortItem(ownerA, "same-original", DirectoryWriteKind.Permit, "conversation-a", "permit-a", "effect-a", 1, "Authorized");
        var second = first with { Owner = ownerB };
        var install = f.Command(GovernanceGuardOperation.InstallRepairFence);
        install = install with { Repair = new("tenant-a", install.OperationId, "epoch-a", install.ExpectedGuardRevision, "post-fence-checkpoint", "repair-revocation", [first, second]) };
        (await f.Apply(install)).Status.ShouldBe("Committed");
        f.State.Repair!.Cohort.Count.ShouldBe(2);
        var identityA = new DirectoryRepairOriginalIdentity(ownerA, first.OperationId);
        var identityB = new DirectoryRepairOriginalIdentity(ownerB, second.OperationId);
        var mutationA = new GuardedStateMutation("bridge-a", 0, GuardedTransactionFixture.Hash([]), "sealed-a"u8.ToArray());
        var appendA = f.Command(GovernanceGuardOperation.AppendWrite, write: f.Facts(DirectoryWriteKind.Permit), reference: first.OperationId) with { BridgeOriginal = identityA };
        var acceptedA = await f.Apply(appendA, [mutationA]); acceptedA.Status.ShouldBe("Accepted");
        (await f.Apply(f.Command(GovernanceGuardOperation.RecordBridgeDrain, reference: first.OperationId) with { BridgeOriginal = identityA })).Status.ShouldBe("Committed");
        f.State.Repair!.DrainedOriginals.ShouldBe(new[] { identityA });
        (await f.Apply(f.Command(GovernanceGuardOperation.AppendWrite, write: f.Facts(DirectoryWriteKind.Permit), reference: first.OperationId) with { BridgeOriginal = identityA },
            [mutationA with { CellId = "after-a-drain" }])).Status.ShouldBe("Blocked");
        (await f.Apply(f.Command(GovernanceGuardOperation.ActivateSuccessor) with { EpochId = "epoch-b" })).Status.ShouldBe("Blocked");
        var mutationB = mutationA with { CellId = "bridge-b", NextValue = "sealed-b"u8.ToArray() };
        var factsB = f.Facts(DirectoryWriteKind.Permit) with { PermitOwnerId = ownerB.ToString() };
        (await f.Apply(f.Command(GovernanceGuardOperation.AppendWrite, write: factsB, reference: second.OperationId) with { BridgeOriginal = identityB }, [mutationB])).Status.ShouldBe("Accepted");
        (await f.Apply(f.Command(GovernanceGuardOperation.RecordBridgeDrain, reference: second.OperationId))).Status.ShouldBe("Blocked");
        f.State.Repair!.DrainedOriginals.ShouldBe(new[] { identityA });
        (await f.Apply(f.Command(GovernanceGuardOperation.RecordBridgeDrain, reference: second.OperationId) with { BridgeOriginal = identityB })).Status.ShouldBe("Committed");
        (await f.Apply(f.Command(GovernanceGuardOperation.ActivateSuccessor) with { EpochId = "epoch-b" })).Status.ShouldBe("Committed");
        f.State.Repair.ShouldBeNull(); var history = f.State.RepairHistory.Single();
        history.Cohort.ShouldBe(new[] { first, second }); history.DrainedOriginals.ShouldBe(new[] { identityA, identityB });
        f.State.EpochId.ShouldBe("epoch-b"); f.State.LegacyRevocationReceipt.ShouldBe("legacy-revoked");
        f.Backend.Stored.ContainsKey(f.Backend.Key("after-a-drain")).ShouldBeFalse();
        f.DenyEffects = true; var restarted = new GovernanceScopeGuardOwner(f.Backend.Owner, TimeProvider.System, f.Authority);
        (await restarted.ExecuteAsync(appendA, [mutationA], TestContext.Current.CancellationToken)).ShouldBe(acceptedA);
        f.State.RepairHistory.Single().DrainedOriginals.ShouldBe(new[] { identityA, identityB });
    }
    /// <summary>Two exact finite repairs cannot reactivate a retired epoch after serialized restart; only a fresh successor restores writes.</summary>
    [Fact]
    public async Task RetiredEpochCannotBeReactivatedByASecondRepair()
    {
        var f = new GovernanceGuardFixture();
        var source = new AggregateIdentity("tenant-a", "directory", "directory-a");
        async Task<(GovernanceGuardTransition Original, GovernanceProtocolReceipt Receipt, GuardedStateMutation Mutation)> RepairAsync(string originalId)
        {
            var cohort = new DirectoryRepairCohortItem(source, originalId, DirectoryWriteKind.Permit, "conversation-a", "permit-a", "effect-a", 1, "Authorized");
            var install = f.Command(GovernanceGuardOperation.InstallRepairFence);
            install = install with { Repair = new("tenant-a", install.OperationId, f.State.EpochId, install.ExpectedGuardRevision,
                "checkpoint-" + originalId, "repair-revocation", [cohort]) };
            (await f.Apply(install)).Status.ShouldBe("Committed");
            var original = f.Command(GovernanceGuardOperation.AppendWrite, write: f.Facts(DirectoryWriteKind.Permit), reference: originalId)
                with { BridgeOriginal = new(source, originalId) };
            var mutation = new GuardedStateMutation("source-" + originalId, 0, GuardedTransactionFixture.Hash([]), "sealed-original"u8.ToArray());
            var receipt = await f.Apply(original, [mutation]); receipt.Status.ShouldBe("Accepted");
            (await f.Apply(f.Command(GovernanceGuardOperation.RecordBridgeDrain, reference: originalId) with { BridgeOriginal = new(source, originalId) })).Status.ShouldBe("Committed");
            return (original, receipt, mutation);
        }
        var first = await RepairAsync("original-a");
        (await f.Apply(f.Command(GovernanceGuardOperation.ActivateSuccessor) with { EpochId = "epoch-b" })).Status.ShouldBe("Committed");
        await RepairAsync("original-b");
        // Restore every independently anchored technical cell exactly, including original result and repair cohort.
        foreach (var pair in f.Backend.Stored.ToArray())
        { f.Backend.Stored[pair.Key] = JsonSerializer.SerializeToUtf8Bytes(JsonSerializer.Deserialize<JsonElement>(pair.Value)); }
        var restarted = new GovernanceScopeGuardOwner(f.Backend.Owner, TimeProvider.System, f.Authority);
        string retained = JsonSerializer.Serialize(f.State);
        var reuse = f.Command(GovernanceGuardOperation.ActivateSuccessor) with { EpochId = "epoch-a" };
        (await restarted.ExecuteAsync(reuse, [], TestContext.Current.CancellationToken))!.Status.ShouldBe("Blocked");
        JsonSerializer.Serialize(f.State).ShouldBe(retained);
        var oldWrite = f.Command(GovernanceGuardOperation.AppendWrite, write: f.Facts(DirectoryWriteKind.Permit) with { EpochId = "epoch-a" }) with { EpochId = "epoch-a" };
        (await restarted.ExecuteAsync(oldWrite, [first.Mutation with { CellId = "old-a" }], TestContext.Current.CancellationToken))!.Status.ShouldBe("Blocked");
        f.Backend.Stored.ContainsKey(f.Backend.Key("old-a")).ShouldBeFalse();
        (await restarted.ExecuteAsync(f.Command(GovernanceGuardOperation.ActivateSuccessor) with { EpochId = "epoch-c" }, [], TestContext.Current.CancellationToken))!.Status.ShouldBe("Committed");
        f.State.EpochId.ShouldBe("epoch-c"); f.State.Repair.ShouldBeNull();
        f.State.RepairHistory.Select(value => value.EpochId).ShouldBe(new[] { "epoch-a", "epoch-b" });
        f.State.RepairHistory.ShouldAllBe(value => value.DrainedOriginals.Count == value.Cohort.Count);
        foreach (string retired in new[] { "epoch-a", "epoch-b" })
        {
            var denied = f.Command(GovernanceGuardOperation.AppendWrite, write: f.Facts(DirectoryWriteKind.Permit) with { EpochId = retired }) with { EpochId = retired };
            (await restarted.ExecuteAsync(denied, [first.Mutation with { CellId = "late-" + retired }], TestContext.Current.CancellationToken))!.Status.ShouldBe("Blocked");
            f.Backend.Stored.ContainsKey(f.Backend.Key("late-" + retired)).ShouldBeFalse();
        }
        (await restarted.ExecuteAsync(f.Command(GovernanceGuardOperation.AppendWrite, write: f.Facts(DirectoryWriteKind.Permit)),
            [first.Mutation with { CellId = "fresh-c" }], TestContext.Current.CancellationToken))!.Status.ShouldBe("Accepted");
        f.DenyEffects = true;
        (await restarted.ExecuteAsync(first.Original, [first.Mutation], TestContext.Current.CancellationToken)).ShouldBe(first.Receipt);
    }

    /// <summary>The actual InstallEpoch entry bounds supplied target Count and traversal before any independent guard authority or joint effect.</summary>
    [Theory]
    [InlineData("count", false)][InlineData("count", true)][InlineData("traversal", false)][InlineData("traversal", true)]
    public async Task SuspendedTargetsCannotAuthorizeOrMutateGuard(string access, bool expires)
    {
        var fixture = new GovernanceGuardFixture(); var command = fixture.Command(GovernanceGuardOperation.InstallEpoch);
        var stored = fixture.Backend.Stored.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
        var clock = new RetainedHistoryTimeProvider(fixture.Backend.Now); using var caller = new CancellationTokenSource();
        using var release = new ManualResetEventSlim(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Block() { entered.TrySetResult(); release.Wait(); finished.TrySetResult(); }
        var supplied = Substitute.For<IReadOnlyList<GuardedStateMutation>>();
        supplied.Count.Returns(_ => { if (access == "count") { Block(); } return 0; });
        supplied.GetEnumerator().Returns(_ => { if (access == "traversal") { Block(); } return ((IEnumerable<GuardedStateMutation>)Array.Empty<GuardedStateMutation>()).GetEnumerator(); });
        fixture.Authority.ClearReceivedCalls(); fixture.Backend.Authority.ClearReceivedCalls();
        var owner = new GovernanceScopeGuardOwner(fixture.Backend.Owner, clock, fixture.Authority);
        var pending = Task.Run(() => owner.ExecuteAsync(command, supplied, caller.Token), CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        try
        {
            if (expires) { clock.Advance(TimeSpan.FromSeconds(30)); (await pending.WaitAsync(TimeSpan.FromSeconds(2))).ShouldBeNull(); }
            else { caller.Cancel(); var error = await Should.ThrowAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(2))); error.CancellationToken.ShouldBe(caller.Token); }
        }
        finally { release.Set(); }
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(2));
        fixture.Authority.ReceivedCalls().ShouldBeEmpty(); fixture.Backend.Authority.ReceivedCalls().ShouldBeEmpty(); fixture.Backend.TransactionCalls.ShouldBe(0);
        foreach (var pair in stored) { fixture.Backend.Stored[pair.Key].ShouldBe(pair.Value); }
    }

    /// <summary>Every demonstrated disabled owner entry preserves original cancellation and remains null for an active caller.</summary>
    [Theory]
    [InlineData("read")][InlineData("execute")][InlineData("no-issue")]
    public async Task DisabledOwnerEntriesCheckOriginalCancellation(string entry)
    {
        var fixture = new GovernanceGuardFixture(); var owner = new GovernanceScopeGuardOwner(fixture.Backend.Owner, TimeProvider.System);
        var command = fixture.Command(GovernanceGuardOperation.InstallEpoch);
        var payload = new DeletionBatchCapabilityV1("issuer", "audience", "tenant-a", "deletion-a", new string('A', 64), "AcceptedSet", 0,
            new string('B', 64), new string('C', 64), "guard", 1, 1, 1, "key");
        string requestId = DeletionBatchCapabilityIdentity.SigningRequestId(payload);
        using var caller = new CancellationTokenSource(); caller.Cancel();
        async Task<bool> Run(CancellationToken token) => entry == "read" ? await owner.ReadAsync("tenant-a", token) is null
            : entry == "execute" ? await owner.ExecuteAsync(command, [], token) is null : await owner.ReadNoIssueAsync(payload, requestId, new string('D', 64), token) is null;
        var error = await Should.ThrowAsync<OperationCanceledException>(() => Run(caller.Token)); error.CancellationToken.ShouldBe(caller.Token);
        (await Run(CancellationToken.None)).ShouldBeTrue(); fixture.Backend.TransactionCalls.ShouldBe(0);
    }

    /// <summary>Changing caller violation identity cannot allocate another successor for the same original accepted write.</summary>
    [Fact]
    public async Task ReobservedAcceptedWriteKeepsOneViolationAndOneSuccessor()
    {
        var fixture = new GovernanceGuardFixture(); await fixture.PrepareBoundAsync();
        var binding = fixture.State.Deletions.Single().ContentBindings.Single();
        var first = fixture.Ordinal(violation: "first-observation", invalidated: [binding.GlobalCutId, binding.TokenId]);
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.RecordViolation, ordinal: first))).Status.ShouldBe("Committed");
        string retained = JsonSerializer.Serialize(fixture.State);
        var changedCaller = fixture.Ordinal(2, violation: "changed-observation", invalidated: [binding.GlobalCutId, binding.TokenId]);
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.RecordViolation, ordinal: changedCaller))).Status.ShouldBe("Blocked");
        JsonSerializer.Serialize(fixture.State).ShouldBe(retained);
        fixture.State.Deletions.Single().Violations.Single().AcceptanceReceiptId.ShouldBe(fixture.OriginalAcceptance!.ReceiptId);
    }

    /// <summary>A pre-install accepted original has ordinal zero; a later caller cannot substitute a positive ordinal.</summary>
    [Fact]
    public async Task PreInstallOriginalRejectsInventedAcceptedOrdinal()
    {
        var fixture = new GovernanceGuardFixture(); await fixture.PrepareBoundAsync();
        var binding = fixture.State.Deletions.Single().ContentBindings.Single();
        var wrong = fixture.Ordinal(violation: "invented-ordinal", invalidated: [binding.GlobalCutId, binding.TokenId]) with { AcceptedAtOrdinal = 1 };
        string before = JsonSerializer.Serialize(fixture.State);
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.RecordViolation, ordinal: wrong))).Status.ShouldBe("Blocked");
        JsonSerializer.Serialize(fixture.State).ShouldBe(before);
    }

    /// <summary>Each overlapping request retains its own ordinal at one accepted write's joint linearization.</summary>
    [Fact]
    public async Task OverlappingScopesRetainIndependentOriginalOrdinals()
    {
        var fixture = new GovernanceGuardFixture();
        fixture.OriginalAcceptance = await fixture.Apply(fixture.Command(GovernanceGuardOperation.AppendWrite, write: fixture.Facts()),
            [new("source-initial", 0, GuardedTransactionFixture.Hash([]), "initial"u8.ToArray())]);
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.AuthorizeAdmissionFence, scope: fixture.Scope, authorization: "first-fence"))).Status.ShouldBe("Committed");
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.CommitAdmissionFence, scope: fixture.Scope, authorization: "first-fence"))).Status.ShouldBe("Committed");
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.RecordViolation,
            ordinal: fixture.Ordinal(violation: "first-recut", noCut: "authenticated-no-cut")))).Status.ShouldBe("Committed");
        var secondScope = new GovernanceScopeV1("tenant-a", "ExactInteraction", "interaction-b", "");
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.AuthorizeAdmissionFence, scope: secondScope, authorization: "second-fence")
            with { DeletionRequestId = "deletion-b" })).Status.ShouldBe("Committed");
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.CommitAdmissionFence, scope: secondScope, authorization: "second-fence")
            with { DeletionRequestId = "deletion-b" })).Status.ShouldBe("Committed");
        fixture.AppendResource = "overlap-resource";
        var accepted = await fixture.Apply(fixture.Command(GovernanceGuardOperation.AppendWrite, write: fixture.Facts()),
            [new("source-overlap", 0, GuardedTransactionFixture.Hash([]), "overlap"u8.ToArray())]);
        accepted.Status.ShouldBe("Accepted");
        var attributions = JsonSerializer.Deserialize<GovernanceAdmissionAttribution[]>(accepted.AdmissionAttributionsJson)!;
        attributions.ShouldBe([new("deletion-a", 2), new("deletion-b", 1)]);
        fixture.OriginalAcceptance = accepted;
        var first = new GovernanceOrdinalCommand(2, "", "", "", "", "overlap-first", "Content", 2, accepted.GuardHighWater,
            "overlap-resource", [], "authenticated-no-cut");
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.RecordViolation, ordinal: first))).Status.ShouldBe("Committed");
        var second = first with { Ordinal = 1, AcceptedAtOrdinal = 1, ViolationId = "overlap-second" };
        var wrongSecond = second with { AcceptedAtOrdinal = accepted.AcceptedAtAdmissionFenceOrdinal, ViolationId = "overlap-wrong-maximum" };
        string beforeWrong = JsonSerializer.Serialize(fixture.State);
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.RecordViolation, ordinal: wrongSecond) with { DeletionRequestId = "deletion-b" })).Status.ShouldBe("Blocked");
        JsonSerializer.Serialize(fixture.State).ShouldBe(beforeWrong);
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.RecordViolation, ordinal: second) with { DeletionRequestId = "deletion-b" })).Status.ShouldBe("Committed");
        fixture.State.Deletions.Single(value => value.RequestId == "deletion-a").Violations.Single(value => value.ResourceId == "overlap-resource").AcceptanceReceiptId.ShouldBe(accepted.ReceiptId);
        fixture.State.Deletions.Single(value => value.RequestId == "deletion-b").Violations.Single().AcceptanceReceiptId.ShouldBe(accepted.ReceiptId);
    }

    /// <summary>Original lookup returns only the exact committed receipt under an unchanged current lookup proof; stale, changed, foreign-intent, unperformed or unauthorized lookups return unknown.</summary>
    [Theory]
    [InlineData("valid")][InlineData("stale")][InlineData("changed")][InlineData("intent")][InlineData("uncommitted")][InlineData("no-authority")]
    public async Task LookupOriginalReturnsOnlyExactCommittedReceiptUnderUnchangedCurrentProof(string vector)
    {
        var fixture = new GovernanceGuardFixture();
        var command = fixture.Command(GovernanceGuardOperation.AuthorizeAdmissionFence, scope: fixture.Scope, authorization: "lookup-a");
        var committed = await fixture.Apply(command); committed.Status.ShouldBe("Committed");
        int reads = 0;
        var authority = Substitute.For<IGovernanceGuardAuthority>();
        authority.ReadLookupAsync(Arg.Any<GovernanceGuardTransition>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            var proof = await fixture.Authority.ReadLookupAsync(call.Arg<GovernanceGuardTransition>(), call.ArgAt<string>(1), call.ArgAt<string>(2), call.Arg<CancellationToken>());
            return vector switch
            {
                "stale" => proof! with { ValidUntil = proof.ObservedAt },
                "changed" when Interlocked.Increment(ref reads) > 1 => proof! with { AuthorityReceiptId = "changed-lookup-receipt" },
                _ => proof,
            };
        });
        var owner = vector == "no-authority" ? new GovernanceScopeGuardOwner(fixture.Backend.Owner, TimeProvider.System) : new GovernanceScopeGuardOwner(fixture.Backend.Owner, TimeProvider.System, authority);
        var lookup = vector == "uncommitted" ? fixture.Command(GovernanceGuardOperation.AuthorizeAdmissionFence, scope: fixture.Scope, authorization: "never-applied") : command;
        IReadOnlyList<GuardedStateMutation> targets = vector == "intent" ? [new("foreign-target", 0, GuardedTransactionFixture.Hash([]), "foreign"u8.ToArray())] : [];
        long prior = fixture.State.Revision;
        var original = await owner.LookupOriginalAsync(lookup, targets, TestContext.Current.CancellationToken);
        if (vector == "valid") { JsonSerializer.Serialize(original.ShouldNotBeNull()).ShouldBe(JsonSerializer.Serialize(committed)); }
        else { original.ShouldBeNull(); }
        fixture.State.Revision.ShouldBe(prior);
    }

}
