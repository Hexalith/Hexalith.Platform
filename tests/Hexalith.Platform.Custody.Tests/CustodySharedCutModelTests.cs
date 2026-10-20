using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;
using Shouldly;

namespace Hexalith.Platform.Custody.Tests;

/// <summary>Value-free offline F0 exploration. Symbolic outputs are never real custody receipts.</summary>
public sealed class CustodySharedCutModelTests(ITestOutputHelper output)
{
    private static readonly SharedCutAction[] SuccessfulSchedule =
    [
        SharedCutAction.CommitNativeSend, SharedCutAction.DispatchGuard, SharedCutAction.ReserveProtection,
        SharedCutAction.ReserveCustody, SharedCutAction.PrepareSurvivors, SharedCutAction.CompleteNativeResult,
        SharedCutAction.CloseNativeIntent, SharedCutAction.CloseHolders, SharedCutAction.RetireAlternate,
        SharedCutAction.RetireTainted, SharedCutAction.ConfirmPreparation, SharedCutAction.Arm, SharedCutAction.Cut,
        SharedCutAction.VerifyRestoreBarrier, SharedCutAction.Publish,
    ];

    [Theory]
    [InlineData(1, 2)]
    [InlineData(1000, 0)]
    public void ExactOneAndThousandTargetVectorsCutTogetherAndPreserveOriginalBytes(int targets, int survivors)
    {
        var state = Initial(targets, survivors);
        foreach (var action in SuccessfulSchedule)
        {
            var prior = state;
            var step = CustodySharedCutModel.Apply(state, action, state.Head);
            if (!step.Accepted && action == SharedCutAction.PrepareSurvivors && survivors == 0) { continue; }
            step.Accepted.ShouldBeTrue(action + ":" + step.Reason); state = step.State;
            CustodySharedCutModel.Violations(state).ShouldBeEmpty();
            if (action == SharedCutAction.Cut)
            {
                prior.Paths.Where(p => p.Selected).All(p => p.Epochs == SharedCutEpoch.Source).ShouldBeTrue();
                state.Paths.Where(p => p.Selected).All(p => p.Epochs == SharedCutEpoch.None).ShouldBeTrue();
            }
            AssertObservation(state);
        }
        state.Paths.Count(p => p.Selected).ShouldBe(targets);
        state.Paths.Count(p => !p.Selected && p.Epochs == SharedCutEpoch.Successor).ShouldBe(survivors);
        state.CutCount.ShouldBe(1);
        AssertObservation(Accept(state, SharedCutAction.RestoreApplication));
        CustodySharedCutModel.Apply(state, SharedCutAction.Cut, state.Head).Accepted.ShouldBeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GuardDispatchOrdersSelectedPinsBeforeB0AndSurvivorPinsRemainIndependent(bool effective)
    {
        var pin = effective ? SharedCutAction.EffectivePin : SharedCutAction.UnknownPin;
        var state = Initial(); var selected = state.Original.Targets[0]; var survivor = state.Survivors[0];
        state = Accept(state, pin, selected, "selected-hold");
        CustodySharedCutModel.Apply(state, SharedCutAction.DispatchGuard, state.Head).Reason.ShouldBe("effective-or-unknown-selected-hold");
        state = Accept(state, pin, survivor, "survivor-hold"); int oldHead = state.Head;
        CustodySharedCutModel.Apply(state, SharedCutAction.ReleasePin, state.Head, identity: survivor, holdId: "selected-hold").Accepted.ShouldBeFalse();
        state = Accept(state, SharedCutAction.ReleasePin, selected, "selected-hold");
        state.Holds.Single(h => h.HoldId == "survivor-hold").Kind.ShouldNotBe(SharedCutHoldKind.Released);
        CustodySharedCutModel.Apply(state, SharedCutAction.DispatchGuard, oldHead).Reason.ShouldBe("stale-current-revision");
        state = Accept(state, SharedCutAction.DispatchGuard);
        state.Boundary.ShouldBe(SharedCutBoundary.Dispatched);
        CustodySharedCutModel.Apply(state, pin, state.Head, identity: selected, holdId: "late-selected").Reason.ShouldBe("selected-pin-after-guard-dispatch");
        state = Finish(state); AssertObservation(state);
        state.Paths.Single(p => p.Identity == survivor).Epochs.ShouldBe(SharedCutEpoch.Successor);
        state.Holds.Single(h => h.Identity == survivor).Kind.ShouldNotBe(SharedCutHoldKind.Released);
    }

    [Fact]
    public void RestoredCallersNeedExplicitCurrentRevisionAndLookupCannotReconcileOrMutate()
    {
        var state = RunTo(SharedCutBoundary.B3Armed); int head = state.Head;
        state = Accept(state, SharedCutAction.RestoreApplication);
        state.ApplicationHead.ShouldBe(0); state.Head.ShouldBe(head);
        CustodySharedCutModel.Apply(state, SharedCutAction.Cut).Reason.ShouldBe("expected-current-revision-required");
        CustodySharedCutModel.Apply(state, SharedCutAction.Cut, state.ApplicationHead).Reason.ShouldBe("stale-current-revision");
        Accept(state, SharedCutAction.Lookup).ShouldBe(state); AssertObservation(state);
        state = Accept(state, SharedCutAction.RestartApplication); state.ApplicationHead.ShouldBe(head);
        state = Accept(state, SharedCutAction.Cut); state.Head.ShouldBe(head + 1);
        state = Finish(state); AssertObservation(state);
    }

    [Fact]
    public void UnavailableRestartCannotRefreshRestoredRevisionUntilSameAuthorityReconciliation()
    {
        var state = RunTo(SharedCutBoundary.B3Armed); int independentHead = state.Head;
        state = Accept(state, SharedCutAction.RestoreApplication);
        state = Accept(state, SharedCutAction.LoseAuthority);
        state = Accept(state, SharedCutAction.CrashApplication);
        var restart = CustodySharedCutModel.Apply(state, SharedCutAction.RestartApplication);
        restart.Accepted.ShouldBeTrue(); restart.Reason.ShouldBe("process-restarted-current-authority-still-unavailable");
        state = restart.State; state.ApplicationRunning.ShouldBeTrue(); state.ApplicationHead.ShouldBe(0);
        state.Head.ShouldBe(independentHead); AssertObservation(state);
        CustodySharedCutModel.Apply(state, SharedCutAction.Cut, state.ApplicationHead).Reason.ShouldBe("current-authority-unavailable");
        state = Accept(state, SharedCutAction.RecoverSameAuthority); state.ApplicationHead.ShouldBe(0);
        CustodySharedCutModel.Apply(state, SharedCutAction.Cut, state.ApplicationHead).Reason.ShouldBe("stale-current-revision");
        Accept(state, SharedCutAction.Lookup).ShouldBe(state);
        state = Accept(state, SharedCutAction.RestartApplication); state.ApplicationHead.ShouldBe(independentHead);
        state = Accept(state, SharedCutAction.Cut); state.Head.ShouldBe(independentHead + 1);
        AssertObservation(Finish(state));
    }

    [Fact]
    public void AvailableAuthorityReplacementIsExplicitlyUnsupportedBeforeAndAfterRestore()
    {
        foreach (var boundary in new[] { SharedCutBoundary.B2Prepared, SharedCutBoundary.B5Published })
        {
            var original = RunTo(boundary);
            var restored = Accept(original, SharedCutAction.RestoreApplication);
            foreach (var state in new[] { original, restored, Accept(restored, SharedCutAction.RestartApplication) })
            {
                state.ApplicationRunning.ShouldBeTrue(); state.AuthorityAvailable.ShouldBeTrue();
                var result = CustodySharedCutModel.Apply(state, SharedCutAction.ReplaceAuthority, state.Head, state.Original);
                result.Accepted.ShouldBeFalse(); result.Reason.ShouldBe("authority-replacement-unsupported");
                result.State.ShouldBe(state); result.State.Head.ShouldBe(original.Head); result.State.Original.ShouldBe(original.Original);
                AssertObservation(result.State);
            }
        }
    }

    [Fact]
    public void SameFreshAllocationUsesSourceBeforeFreezeIsBlockedDuringFreezeAndUsesSuccessorAfterArm()
    {
        var identity = Identity(999);
        foreach (var boundary in Enum.GetValues<SharedCutBoundary>())
        {
            var state = RunTo(boundary);
            var result = CustodySharedCutModel.Apply(state, SharedCutAction.AllocateRoot, state.Head, identity: identity);
            if (state.Frozen)
            {
                result.Accepted.ShouldBeFalse(); result.Reason.ShouldBe("tenant-freeze-or-registration-bound"); result.State.ShouldBe(state);
            }
            else
            {
                result.Accepted.ShouldBeTrue(); state = result.State;
                state.Paths.Last().Identity.ShouldBe(identity);
                state.Paths.Last().Epochs.ShouldBe(boundary < SharedCutBoundary.B1CustodyReserved ? SharedCutEpoch.Source : SharedCutEpoch.Successor);
                state.AllocationExclusions.Count.ShouldBe(4);
                state = Finish(state); state.Paths.Last().Epochs.ShouldBe(SharedCutEpoch.Successor);
                CustodySharedCutModel.Violations(state).ShouldBeEmpty(); AssertObservation(state);
            }
        }
    }

    [Fact]
    public void HoldAndControlMutationsInvalidateRevisionAndWholeTenantFreezeBlocksAdmission()
    {
        var state = RunTo(SharedCutBoundary.B2Prepared); int head = state.Head;
        state = Accept(state, SharedCutAction.EffectivePin, state.Survivors[0], "held-survivor");
        CustodySharedCutModel.Apply(state, SharedCutAction.Arm, head).Reason.ShouldBe("stale-current-revision");
        head = state.Head; state = Accept(state, SharedCutAction.ReleasePin, state.Survivors[0], "held-survivor");
        CustodySharedCutModel.Apply(state, SharedCutAction.Arm, head).Reason.ShouldBe("stale-current-revision");
        CustodySharedCutModel.Apply(state, SharedCutAction.AllocateRoot, state.Head, identity: Identity(999)).Accepted.ShouldBeFalse();
        CustodySharedCutModel.Apply(state, SharedCutAction.Rotate, state.Head).Accepted.ShouldBeFalse();
        state = Accept(state, SharedCutAction.Arm); head = state.Head;
        state = Accept(state, SharedCutAction.Rotate);
        CustodySharedCutModel.Apply(state, SharedCutAction.Cut, head).Reason.ShouldBe("stale-current-revision");
        state = Accept(state, SharedCutAction.AllocateRoot, Identity(999));
        state = Finish(state); state.Survivors.ShouldContain(Identity(999));
        state.Paths.Last().Epochs.ShouldBe(SharedCutEpoch.Successor); AssertObservation(state);
    }

    [Theory]
    [InlineData("alias")][InlineData("version")][InlineData("lifecycle")][InlineData("store")][InlineData("contract")]
    public void OrdinaryAllocationRejectsPreviouslyAllocatedInteractionAcrossIdentityChangesAndRestore(string change)
    {
        var state = Finish(Initial());
        foreach (var retained in new[] { state.Original.Targets[0], state.Survivors[0] })
        {
            var candidate = ChangedIdentity(retained, change);
            foreach (var current in new[] { state, Accept(state, SharedCutAction.RestoreApplication), Accept(state, SharedCutAction.RestartApplication) })
            { CustodySharedCutModel.Apply(current, SharedCutAction.AllocateRoot, current.Head, identity: candidate).Reason.ShouldBe("permanent-interaction-allocation-exclusion"); }
        }
        var fresh = Identity(999) with { ObjectId = new string('x', 2048) };
        CustodyKeyLifecycleActor.ValidateIdentity(fresh);
        state = Accept(state, SharedCutAction.AllocateRoot, fresh);
        state.Survivors.Last().ShouldBe(fresh); state.Paths.Last().Identity.ObjectId.Length.ShouldBe(2048);
        CustodySharedCutModel.Violations(state).ShouldBeEmpty();
    }

    [Fact]
    public void RegistrationBoundCountsSelectedAndSurvivorsWithoutChangingIdentityLimits()
    {
        var full = Initial(1, 999);
        Should.Throw<ArgumentException>(() => Initial(1, 1000));
        full = Finish(full);
        CustodySharedCutModel.Apply(full, SharedCutAction.AllocateRoot, full.Head, identity: Identity(1001)).Accepted.ShouldBeFalse();
        full.AllocationExclusions.Count.ShouldBe(1000);
        Should.Throw<ArgumentException>(() => CustodyKeyLifecycleActor.ValidateIdentity(Identity(1002) with { ObjectId = new string('x', 2049) }));
    }

    [Fact]
    public void DelayedFirstOriginalDeliveryAfterFreezeAndSidecarLossTaintsUntilActualOriginalClosureAndRetirement()
    {
        var state = RunTo(SharedCutBoundary.B1CustodyReserved); var native = state.Native;
        native.Phase.ShouldBe(SharedCutNativePhase.SendCommitted);
        state = Accept(state, SharedCutAction.LoseSidecar);
        state = Accept(state, SharedCutAction.RestartApplication);
        state = Accept(state, SharedCutAction.FirstStagingDelivery);
        state.Paths[0].Epochs.HasFlag(SharedCutEpoch.Tainted).ShouldBeTrue();
        state.Native.IntentId.ShouldBe(native.IntentId); state.Native.ProducerIncarnation.ShouldBe(native.ProducerIncarnation);
        state.Native.DeliveryId.ShouldBe(native.DeliveryId);
        Accept(state, SharedCutAction.DelayedDuplicateExecute).ShouldBe(state);
        foreach (var action in new[] { SharedCutAction.CommitNativeSend, SharedCutAction.FirstStagingDelivery,
            SharedCutAction.DelayedDuplicateKeyRelease, SharedCutAction.CloseHolders, SharedCutAction.RetireTainted, SharedCutAction.Arm })
        { CustodySharedCutModel.Apply(state, action, state.Head).Accepted.ShouldBeFalse(); }
        state = Accept(state, SharedCutAction.CompleteNativeResult); state.Native.Phase.ShouldBe(SharedCutNativePhase.ResultDurable);
        CustodySharedCutModel.Apply(state, SharedCutAction.FirstStagingDelivery, state.Head).Accepted.ShouldBeFalse();
        state = Accept(state, SharedCutAction.CloseNativeIntent); state = Accept(state, SharedCutAction.CloseHolders);
        state = Accept(state, SharedCutAction.PrepareSurvivors); state = Accept(state, SharedCutAction.RetireAlternate);
        CustodySharedCutModel.Apply(state, SharedCutAction.ConfirmPreparation, state.Head).Accepted.ShouldBeFalse();
        state = Accept(state, SharedCutAction.RetireTainted); state = Finish(state);
        state.LostSidecarIncarnation.ShouldBe("original-sidecar"); state.CutCount.ShouldBe(1); AssertObservation(state);
    }

    [Fact]
    public void InFlightFirstDeliveryCanArriveDuringCrashAndAuthorityOutageWithoutNewAdmission()
    {
        var state = RunTo(SharedCutBoundary.B1CustodyReserved); int oldHead = state.Head;
        state = Accept(state, SharedCutAction.CrashApplication);
        state = Accept(state, SharedCutAction.LoseSidecar);
        state = Accept(state, SharedCutAction.LoseAuthority);
        var delivered = CustodySharedCutModel.Apply(state, SharedCutAction.FirstStagingDelivery);
        delivered.Accepted.ShouldBeTrue(); state = delivered.State;
        state.Head.ShouldBe(oldHead + 1); state.ApplicationHead.ShouldBe(oldHead);
        state.Paths[0].Epochs.HasFlag(SharedCutEpoch.Tainted).ShouldBeTrue(); AssertObservation(state);
        CustodySharedCutModel.Apply(state, SharedCutAction.CommitNativeSend, state.Head).Accepted.ShouldBeFalse();
        state = Accept(state, SharedCutAction.RecoverSameAuthority);
        state = Accept(state, SharedCutAction.RestartApplication);
        CustodySharedCutModel.Apply(state, SharedCutAction.CompleteNativeResult, oldHead).Reason.ShouldBe("stale-current-revision");
        state = Finish(state); AssertObservation(state); state.Native.FirstDelivered.ShouldBeTrue();
    }

    [Theory]
    [InlineData("native")][InlineData("holder")][InlineData("alternate")][InlineData("tainted")]
    [InlineData("survivor")][InlineData("target-successor")][InlineData("missing-survivor")]
    public void EachUnresolvedPreparationBlocksArmCutPublication(string missing)
    {
        var state = RunTo(SharedCutBoundary.B2Prepared);
        state = missing switch
        {
            "native" => state with { Native = state.Native with { Phase = SharedCutNativePhase.SendCommitted } },
            "holder" => state with { HoldersClosed = false },
            "alternate" => state with { AlternateRetired = false },
            "tainted" => state with { TaintedRetired = false },
            "target-successor" => state with { Paths = state.Paths.SetItem(0, state.Paths[0] with { Epochs = SharedCutEpoch.Source | SharedCutEpoch.Successor }) },
            "missing-survivor" => state with { Paths = state.Paths.RemoveAt(2) },
            _ => state with { Paths = state.Paths.SetItem(2, state.Paths[2] with { Epochs = SharedCutEpoch.Source }) },
        };
        CustodySharedCutModel.Apply(state, SharedCutAction.Arm, state.Head).Accepted.ShouldBeFalse();
        CustodySharedCutModel.Apply(state with { Boundary = SharedCutBoundary.B3Armed }, SharedCutAction.Cut, state.Head).Accepted.ShouldBeFalse();
        CustodySharedCutModel.Apply(state with { Boundary = SharedCutBoundary.B4Cut, RestoreBarrierVerified = true }, SharedCutAction.Publish, state.Head).Accepted.ShouldBeFalse();
    }

    [Theory]
    [InlineData("operation")][InlineData("batch")][InlineData("reservation")][InlineData("request")]
    [InlineData("tenant")][InlineData("object")][InlineData("alias")][InlineData("version")]
    [InlineData("lifecycle")][InlineData("store")][InlineData("contract")][InlineData("bytes")]
    public void ChangedOriginalIsRejectedByLookupDuplicateAndEffect(string change)
    {
        var state = RunTo(SharedCutBoundary.B1CustodyReserved);
        var identities = state.Original.Targets.ToArray();
        identities[0] = ChangedIdentity(identities[0], change);
        if (change == "tenant") { identities = identities.Select(i => i with { TenantId = "other-tenant" }).ToArray(); }
        var changed = Original(identities, change);
        foreach (var action in new[] { SharedCutAction.Lookup, SharedCutAction.DelayedDuplicateExecute, SharedCutAction.Arm })
        { CustodySharedCutModel.Apply(state, action, state.Head, changed).Reason.ShouldBe("original-conflict"); }
    }

    [Fact]
    public void ExactManifestOrderDigestPurposeAndOwnedBytesAreValidated()
    {
        var original = Original([Identity(0), Identity(1)]);
        Should.Throw<ArgumentException>(() => Original(original.Targets.Reverse().ToArray()));
        Should.Throw<ArgumentException>(() => Original([Identity(0), Identity(0)]));
        Should.Throw<ArgumentException>(() => Original([Identity(0) with { Purpose = PlatformKeyPurpose.ExportEnvelopeKey }]));
        Should.Throw<ArgumentException>(() => new SharedCutOriginal("op", "batch", "reserve", "request", "wrong", original.Targets, [1]));
        byte[] bytes = [1, 2, 3]; var copy = new SharedCutOriginal("op", "batch", "reserve", "request", original.ManifestDigest, original.Targets, bytes);
        bytes[0] = 9; copy.SymbolicOriginal[0].ShouldBe((byte)1);
    }

    // The Local audit requires THIS exact passing method and a report freshly created by its execution.
    [Fact]
    public void BoundedExplorationAndUnsafeCounterexamplesProduceValueFreeEvidence()
    {
        const int maximumDepth = 20, maximumStates = 100000;
        // Environment/fault interleavings are exercised separately at every boundary, avoiding an
        // exponential cross-product of independent fault markers in the bounded protocol exploration.
        var actions = SuccessfulSchedule.Concat(new[] { SharedCutAction.FirstStagingDelivery,
            SharedCutAction.DelayedDuplicateExecute, SharedCutAction.DelayedDuplicateKeyRelease,
            SharedCutAction.AllocateRoot, SharedCutAction.Rotate, SharedCutAction.Lookup }).Distinct().ToArray();
        var commands = actions.Select(a => (Action: a, Identity: a == SharedCutAction.AllocateRoot ? Identity(999) : null, HoldId: (string?)null))
            .Concat(new[]
            {
                (SharedCutAction.EffectivePin, (CustodyKeyObjectIdentity?)Identity(0), (string?)"selected-effective"),
                (SharedCutAction.UnknownPin, (CustodyKeyObjectIdentity?)Identity(0), (string?)"selected-unknown"),
                (SharedCutAction.ReleasePin, (CustodyKeyObjectIdentity?)Identity(0), (string?)"selected-effective"),
                (SharedCutAction.ReleasePin, (CustodyKeyObjectIdentity?)Identity(0), (string?)"selected-unknown"),
                (SharedCutAction.EffectivePin, (CustodyKeyObjectIdentity?)Identity(2), (string?)"held-survivor"),
                (SharedCutAction.ReleasePin, (CustodyKeyObjectIdentity?)Identity(2), (string?)"held-survivor"),
            }).ToArray();
        var queue = new Queue<(SharedCutState State, ImmutableArray<string> Trace)>();
        var seen = new HashSet<string>(StringComparer.Ordinal); var initial = Initial();
        queue.Enqueue((initial, [])); seen.Add(CustodySharedCutModel.Fingerprint(initial));
        long transitions = 0, refused = 0; int depthReached = 0, publications = 0, cuts = 0;
        while (queue.TryDequeue(out var item))
        {
            var (state, trace) = item; depthReached = Math.Max(depthReached, trace.Length);
            if (state.Boundary == SharedCutBoundary.B5Published) { publications++; }
            if (state.Boundary == SharedCutBoundary.B4Cut) { cuts++; }
            if (trace.Length == maximumDepth) { continue; }
            foreach (var (action, identity, holdId) in commands)
            {
                transitions++;
                var result = CustodySharedCutModel.Apply(state, action, state.Head, identity: identity, holdId: holdId);
                var nextTrace = trace.Add(action + (holdId is null ? "" : ":" + holdId) + ":" + result.Reason);
                CustodySharedCutModel.Violations(result.State).ShouldBeEmpty(string.Join(" -> ", nextTrace));
                result.State.Head.ShouldBeGreaterThanOrEqualTo(state.Head); result.State.Original.ShouldBe(state.Original);
                result.State.Native.IntentId.ShouldBe(state.Native.IntentId); result.State.Native.DeliveryId.ShouldBe(state.Native.DeliveryId);
                if (!state.PublishedOriginal.IsEmpty) { result.State.PublishedOriginal.ToArray().ShouldBe(state.PublishedOriginal.ToArray()); }
                if (!result.Accepted) { refused++; result.State.ShouldBe(state); }
                if (action is SharedCutAction.Lookup or SharedCutAction.DelayedDuplicateExecute) { result.State.ShouldBe(state); }
                AssertObservation(result.State);
                if (seen.Add(CustodySharedCutModel.Fingerprint(result.State)))
                {
                    seen.Count.ShouldBeLessThan(maximumStates, "state bound must never silently truncate exploration");
                    queue.Enqueue((result.State, nextTrace));
                }
            }
        }
        publications.ShouldBeGreaterThan(0); cuts.ShouldBeGreaterThan(0);
        var boundaryEvidence = BoundaryFaultSchedules(); var counterexamples = UnsafeCounterexamples();
        var report = new
        {
            Kind = "OfflineModelOnly", LiveReady = false, DependencyAvailable = false,
            EvidenceProducer = nameof(BoundedExplorationAndUnsafeCounterexamplesProduceValueFreeEvidence),
            Bounds = new { MaximumDepth = maximumDepth, MaximumStates = maximumStates, Targets = 2, Survivors = 1,
                Epochs = 4, OriginalNativeIntents = 1, NativeDeliveries = 1, KekSigningControlRotations = 1, NewSurvivorAllocations = 1,
                SelectedHoldIds = 2, SurvivorHoldIds = 1,
                RevisionQuotient = "current-or-stale; explicit numeric stale/omitted comparisons tested separately",
                FaultCoverage = "four distinct fault schedules at every boundary plus delayed first delivery after freeze/sidecar loss" },
            States = seen.Count, DepthReached = depthReached, Transitions = transitions, RefusedTransitions = refused,
            ReachableCutStates = cuts, ReachablePublishedStates = publications,
            ExplicitVectors = new[] { new { Targets = 1, Survivors = 2 }, new { Targets = 1000, Survivors = 0 } },
            BoundaryFaultSchedules = boundaryEvidence, UnsafeCounterexamples = counterexamples,
            Assumptions = new[]
            {
                "Independent linearizable forward authority retains original bytes, survivor membership, interaction exclusions and native identity across old application restore.",
                "Shared source-epoch destruction is one irreversible physical cut, never a target loop. Permanent alternate/tainted retirement is an actual physical premise.",
                "Original ResultDurable/Closed require actual producer I/O completion and no pending delivery; holder closure covers original SDK/sidecar/UDS/gRPC/TLS/component buffers or accepted termination.",
                "Complete graph and exact hold/release authorization and owner guard ordering are correct independent premises; survivor/restore barrier proof is physically correct.",
                "Fault actions model environment events, not unauthenticated application authority mutations. Result bytes are symbolic value-free test data.",
            },
            Unproved = new[] { "implemented qualifying Dapr component/provider", "physical destruction/all-copy inventory", "real delayed delivery and buffer closure",
                "Dapr limits/SDK interoperability", "independent runtime authorities and qualification roles", "real current-owner signatures and transport authorization", "operational capacity/retention", "owner Live qualification" },
        };
        string json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }); output.WriteLine(json);
        string? path = Environment.GetEnvironmentVariable("HEXALITH_CUSTODY_MODEL_REPORT");
        if (!string.IsNullOrWhiteSpace(path)) { File.WriteAllText(path, json + Environment.NewLine); }
    }

    private static List<object> BoundaryFaultSchedules()
    {
        var evidence = new List<object>();
        foreach (var boundary in Enum.GetValues<SharedCutBoundary>())
        foreach (var fault in new[] { SharedCutAction.CrashApplication, SharedCutAction.LoseAcknowledgment, SharedCutAction.RestoreApplication, SharedCutAction.LoseSidecar })
        {
            var trace = new List<object>(); var baseline = RunTo(boundary, trace); var state = baseline;
            var observations = new List<object>();
            void Record(string point)
            {
                var actual = AssertObservation(state); observations.Add(new { Point = point,
                    state.Original.OperationId, state.Original.BatchId, state.Original.ReservationId,
                    Boundary = state.Boundary.ToString(), state.Head, state.ApplicationHead,
                    actual.Status, OriginalHex = Convert.ToHexString(actual.SymbolicOriginal.ToArray()) });
                Accept(state, SharedCutAction.Lookup).ShouldBe(state);
            }
            void Step(SharedCutAction action)
            {
                var result = CustodySharedCutModel.Apply(state, action, state.Head); result.Accepted.ShouldBeTrue();
                trace.Add(TraceStep(state, action, state.Head, result)); state = result.State;
            }
            Record("before-fault"); Step(fault); Record("after-fault");
            // Retry the exact admitted original immediately, before authority/app reconciliation.
            var duplicate = CustodySharedCutModel.Apply(state, SharedCutAction.DelayedDuplicateExecute, original: baseline.Original);
            duplicate.Accepted.ShouldBeTrue(); duplicate.State.ShouldBe(state);
            duplicate.Reason.ShouldStartWith(CustodySharedCutModel.Observe(state).Status + ":");
            trace.Add(TraceStep(state, SharedCutAction.DelayedDuplicateExecute, null, duplicate));
            Record("immediate-exact-original-duplicate-before-reconciliation");
            switch (fault)
            {
                case SharedCutAction.CrashApplication: state.ApplicationRunning.ShouldBeFalse(); state.CrashInjected.ShouldBeTrue(); break;
                case SharedCutAction.LoseAcknowledgment: state.AcknowledgmentLost.ShouldBeTrue(); break;
                case SharedCutAction.RestoreApplication: state.ApplicationHead.ShouldBe(0); state.OldSnapshotRestored.ShouldBeTrue(); break;
                case SharedCutAction.LoseSidecar: state.LostSidecarIncarnation.ShouldBe("original-sidecar"); break;
            }
            Step(SharedCutAction.LoseAuthority); Record("authority-lost");
            foreach (var action in new[] { SharedCutAction.Arm, SharedCutAction.Cut, SharedCutAction.Publish, SharedCutAction.ReplaceAuthority })
            { CustodySharedCutModel.Apply(state, action, state.Head).Accepted.ShouldBeFalse(); }
            Step(SharedCutAction.RecoverSameAuthority); Record("same-authority-recovered"); state.Head.ShouldBe(baseline.Head);
            Step(SharedCutAction.RestartApplication); Record("restarted");
            Step(SharedCutAction.RestoreApplication); Record("old-snapshot"); state.Head.ShouldBe(baseline.Head);
            CustodySharedCutModel.Apply(state, SharedCutAction.Cut).Reason.ShouldBe("expected-current-revision-required");
            Step(SharedCutAction.RestartApplication); Record("current-head-reconciled");
            Accept(state, SharedCutAction.DelayedDuplicateExecute).ShouldBe(state);
            state = Finish(state, trace); Record("original-published"); state.CutCount.ShouldBe(1);
            CustodySharedCutModel.Violations(state).ShouldBeEmpty();
            evidence.Add(new { Boundary = boundary.ToString(), Fault = fault.ToString(), Trace = trace, Observations = observations,
                LostSidecarIncarnation = state.LostSidecarIncarnation, FinalHead = state.Head, state.CutCount });
        }
        return evidence;
    }

    private static List<object> UnsafeCounterexamples()
    {
        var armed = RunTo(SharedCutBoundary.B3Armed); var cut = Accept(armed, SharedCutAction.Cut); var published = Finish(cut);
        var cases = new (string Expected, SharedCutState State, string Mutation)[]
        {
            ("partial-target-cut", armed with { Paths = armed.Paths.SetItem(0, armed.Paths[0] with { Epochs = SharedCutEpoch.None }) }, "delete only selected path 0 before shared cut; selected path 1 remains"),
            ("whole-selected-set-lost-before-B4", armed with { Paths = armed.Paths.Select(p => p.Selected ? p with { Epochs = SharedCutEpoch.None } : p).ToImmutableArray() }, "delete every selected path while boundary remains B3 and cut count zero"),
            ("armed-selected-path-not-sole-source", armed with { Paths = armed.Paths.Select(p => p.Selected ? p with { Epochs = SharedCutEpoch.Successor } : p).ToImmutableArray() }, "replace all selected sole-Source paths with surviving Successor paths at B3"),
            ("post-cut-survivor-not-successor-only", cut with { Paths = cut.Paths.SetItem(2, cut.Paths[2] with { Epochs = SharedCutEpoch.Source }) }, "replace prepared survivor Successor with retired Source-only path after B4"),
            ("survivor-path-lost", cut with { Paths = cut.Paths.SetItem(2, cut.Paths[2] with { Epochs = SharedCutEpoch.None }) }, "after successful survivor preparation and shared cut, delete already-prepared survivor successor path"),
            ("survivor-membership-changed", cut with { Paths = cut.Paths.RemoveAt(2) }, "remove survivor from mutable graph while independent survivor inventory retains it"),
            ("survivor-membership-changed", cut with { Paths = cut.Paths.SetItem(2, cut.Paths[2] with { Identity = Identity(999) }) }, "substitute different complete survivor identity in mutable graph"),
            ("cut-count-boundary-mismatch", armed with { CutCount = 1 }, "record one cut while boundary remains B3 and selected paths remain"),
            ("cut-count-boundary-mismatch", cut with { CutCount = 0 }, "erase committed cut count at B4"),
            ("false-completion", armed with { PublishedOriginal = armed.Original.SymbolicOriginal }, "publish bytes at B3 before shared cut and restore barrier"),
            ("false-completion", published with { RestoreBarrierVerified = false }, "publish terminal original without independent restore barrier"),
            ("terminal-original-bytes-changed", published with { PublishedOriginal = [0] }, "substitute terminal original bytes after B5"),
            ("armed-with-unresolved-copy-or-holder", armed with { HoldersClosed = false }, "reopen unresolved old buffers at B3 despite claimed arm"),
            ("cut-left-selected-path", published with { Paths = armed.Paths }, "restore old epoch paths from application backup after B5"),
            ("permanent-allocation-inventory-changed", cut with { AllocationExclusions = cut.AllocationExclusions.Clear() }, "erase retained allocation exclusions independently of graph"),
        };
        return cases.Select(c =>
        {
            var violations = CustodySharedCutModel.Violations(c.State); violations.ShouldContain(c.Expected);
            var observed = CustodySharedCutModel.Observe(c.State);
            observed.Status.ShouldBe("Unknown"); observed.SymbolicOriginal.ShouldBeEmpty();
            var lookup = CustodySharedCutModel.Apply(c.State, SharedCutAction.Lookup, original: c.State.Original);
            lookup.State.ShouldBe(c.State); lookup.Reason.ShouldBe("Unknown:original-read-only-no-new-attempt");
            var trace = new List<object>(); var baseline = RunTo(c.State.Boundary, trace);
            baseline.Head.ShouldBe(c.State.Head);
            return (object)new { c.State.Original.OperationId, c.State.Original.BatchId, c.State.Original.ReservationId,
                ExpectedViolation = c.Expected, Baseline = trace, Mutation = new { Action = c.Mutation,
                    Boundary = c.State.Boundary.ToString(), c.State.Head }, Violations = violations,
                Observed = new { observed.Status, OriginalHex = Convert.ToHexString(observed.SymbolicOriginal.ToArray()), LookupReason = lookup.Reason } };
        }).ToList();
    }

    private static SharedCutObservation AssertObservation(SharedCutState state)
    {
        var observed = CustodySharedCutModel.Observe(state);
        observed.Status.ShouldBe(!state.AuthorityAvailable ? "Unavailable" : state.Boundary == SharedCutBoundary.B5Published ? "ModelPublished" : "Unknown");
        observed.SymbolicOriginal.ToArray().ShouldBe(state.AuthorityAvailable && state.Boundary == SharedCutBoundary.B5Published ? state.Original.SymbolicOriginal.ToArray() : []);
        return observed;
    }
    private static SharedCutState Initial(int targets = 2, int survivors = 1) => CustodySharedCutModel.Create(
        Original(Enumerable.Range(0, targets).Select(Identity).ToArray()), Enumerable.Range(targets, survivors).Select(Identity));
    private static CustodyKeyObjectIdentity Identity(int i) => new("model-tenant", $"interaction-{i:D4}", PlatformKeyPurpose.InteractionRootDek,
        "model-alias", "original-root-v1", "model-lifecycle", "opaque-model-store", "model-contract");
    private static CustodyKeyObjectIdentity ChangedIdentity(CustodyKeyObjectIdentity i, string change) => change switch
    {
        "object" => i with { ObjectId = "interaction-0000-changed" }, "alias" => i with { KeyAlias = "changed" },
        "version" => i with { KeyVersion = "changed" }, "lifecycle" => i with { LifecycleVersion = "changed" },
        "store" => i with { StoreTarget = "changed" }, "contract" => i with { ContractVersion = "changed" }, _ => i,
    };
    private static SharedCutOriginal Original(IReadOnlyList<CustodyKeyObjectIdentity> identities, string change = "")
    {
        string digest = DeletionBatchCapabilityIdentity.TargetManifestDigest(identities.Select(i => new ProtectionTarget(i.TenantId, i.ObjectId, i.KeyAlias)).ToArray());
        return new(change == "operation" ? "changed-operation" : "original-operation", change == "batch" ? "changed-batch" : "original-batch",
            change == "reservation" ? "changed-reservation" : "original-reservation", change == "request" ? "changed-request" : "original-request",
            digest, identities, Encoding.UTF8.GetBytes("OFFLINE MODEL ONLY: symbolic ordered original; " + digest + (change == "bytes" ? "changed" : "")));
    }
    private static SharedCutState Accept(SharedCutState state, SharedCutAction action, CustodyKeyObjectIdentity? identity = null, string? holdId = null)
    {
        var step = CustodySharedCutModel.Apply(state, action, state.Head, identity: identity, holdId: holdId);
        step.Accepted.ShouldBeTrue(action + ":" + step.Reason); return step.State;
    }
    private static object TraceStep(SharedCutState before, SharedCutAction action, int? expectedHead, SharedCutStep result)
    {
        var observed = CustodySharedCutModel.Observe(result.State);
        return new { before.Original.OperationId, before.Original.BatchId, before.Original.ReservationId,
            Action = action.ToString(), ExpectedRevision = expectedHead, BeforeBoundary = before.Boundary.ToString(), BeforeHead = before.Head,
            Boundary = result.State.Boundary.ToString(), result.State.Head, result.State.ApplicationHead, result.Accepted, result.Reason,
            observed.Status, OriginalHex = Convert.ToHexString(observed.SymbolicOriginal.ToArray()) };
    }
    private static SharedCutState RunTo(SharedCutBoundary boundary, List<object>? trace = null)
    {
        var state = Initial();
        foreach (var action in SuccessfulSchedule)
        {
            if (state.Boundary == boundary) { return state; }
            var result = CustodySharedCutModel.Apply(state, action, state.Head);
            result.Accepted.ShouldBeTrue(action + ":" + result.Reason);
            trace?.Add(TraceStep(state, action, state.Head, result)); state = result.State;
        }
        state.Boundary.ShouldBe(boundary); return state;
    }
    private static SharedCutState Finish(SharedCutState state, List<object>? trace = null)
    {
        // Reconcile effects with the same authority. Lookup itself does not mutate or start this worker schedule.
        foreach (var action in SuccessfulSchedule)
        {
            var step = CustodySharedCutModel.Apply(state, action, state.Head);
            if (step.Accepted) { trace?.Add(TraceStep(state, action, state.Head, step)); state = step.State; }
        }
        state.Boundary.ShouldBe(SharedCutBoundary.B5Published); return state;
    }
}
