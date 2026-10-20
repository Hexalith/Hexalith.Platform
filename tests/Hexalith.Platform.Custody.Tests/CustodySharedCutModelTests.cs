using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;
using Shouldly;

namespace Hexalith.Platform.Custody.Tests;

/// <summary>Offline F0 graph exploration only; no real keys, transport, provider or irreversible receipts.</summary>
public sealed class CustodySharedCutModelTests(ITestOutputHelper output)
{
    private static readonly SharedCutAction[] SuccessfulSchedule =
    [
        SharedCutAction.ReserveProtection, SharedCutAction.ReserveCustody, SharedCutAction.PrepareSurvivors,
        SharedCutAction.CloseNativeSend, SharedCutAction.CloseHolders, SharedCutAction.RetireAlternate,
        SharedCutAction.RetireTainted, SharedCutAction.ConfirmPreparation, SharedCutAction.Arm,
        SharedCutAction.Cut, SharedCutAction.VerifyRestoreBarrier, SharedCutAction.Publish,
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
            state = Accept(state, action);
            CustodySharedCutModel.Violations(state).ShouldBeEmpty();
            if (action != SharedCutAction.Cut)
            { state.Paths.Where(p => p.Selected).Select(p => p.Epochs != SharedCutEpoch.None).Distinct().Count().ShouldBe(1); }
            else
            {
                prior.Paths.Where(p => p.Selected).All(p => p.Epochs == SharedCutEpoch.Source).ShouldBeTrue();
                state.Paths.Where(p => p.Selected).All(p => p.Epochs == SharedCutEpoch.None).ShouldBeTrue();
            }
        }
        state.Paths.Count(p => p.Selected).ShouldBe(targets);
        state.Paths.Count(p => !p.Selected && p.Epochs == SharedCutEpoch.Successor).ShouldBe(survivors);
        state.CutCount.ShouldBe(1);
        CustodySharedCutModel.Observe(state).Status.ShouldBe("ModelPublished");
        state.PublishedOriginal.ToArray().ShouldBe(state.Original.SymbolicOriginal.ToArray());
        var restored = Accept(state, SharedCutAction.RestoreApplication);
        restored.ApplicationHead.ShouldBe(0);
        restored.Head.ShouldBe(state.Head);
        restored = Accept(restored, SharedCutAction.RestartApplication);
        restored.PublishedOriginal.ToArray().ShouldBe(state.PublishedOriginal.ToArray());
        restored.Paths.ShouldBe(state.Paths);
        Accept(restored, SharedCutAction.DelayedDuplicateExecute).ShouldBe(restored);
        CustodySharedCutModel.Apply(restored, SharedCutAction.Cut).Accepted.ShouldBeFalse();
        CustodySharedCutModel.Apply(restored, SharedCutAction.ReissueRoot).Accepted.ShouldBeFalse();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void EarlierPinsBlockAndExactReleaseInvalidatesStaleHead(int holdValue)
    {
        var hold = (SharedCutHold)holdValue;
        var state = Accept(Initial(), hold == SharedCutHold.Effective ? SharedCutAction.EffectivePin : SharedCutAction.UnknownPin);
        CustodySharedCutModel.Apply(state, SharedCutAction.ReserveProtection).Reason.ShouldBe("effective-or-unknown-pin");
        int priorHead = state.Head;
        state = Accept(state, SharedCutAction.ReleasePin);
        CustodySharedCutModel.Apply(state, SharedCutAction.ReserveProtection, priorHead).Reason.ShouldBe("stale-current-head");
        state = Accept(state, SharedCutAction.ReserveProtection);
        CustodySharedCutModel.Apply(state, SharedCutAction.EffectivePin).Reason.ShouldBe("post-dispatch-pin-cannot-conflict");
        foreach (var action in SuccessfulSchedule.Skip(1)) { state = Accept(state, action); }
        state.Boundary.ShouldBe(SharedCutBoundary.B5Published);
    }

    [Fact]
    public void WholeTenantFreezeBlocksNewRootsRotationAndDelayedKeyDelivery()
    {
        var state = RunTo(SharedCutBoundary.B1CustodyReserved);
        foreach (var action in new[] { SharedCutAction.AllocateSurvivorRoot, SharedCutAction.Rotate, SharedCutAction.ReissueRoot,
            SharedCutAction.DelayedDuplicateKeyRelease, SharedCutAction.EffectivePin, SharedCutAction.UnknownPin })
        {
            var blocked = CustodySharedCutModel.Apply(state, action);
            blocked.Accepted.ShouldBeFalse(); blocked.State.ShouldBe(state);
        }
        state = Finish(state);
        state = Accept(state, SharedCutAction.AllocateSurvivorRoot);
        var before = state.Paths;
        state = Accept(state, SharedCutAction.Rotate);
        state.Paths.ShouldBe(before);
        state.Paths.Last().Epochs.ShouldBe(SharedCutEpoch.Successor);
        state.PublishedOriginal.ToArray().ShouldBe(state.Original.SymbolicOriginal.ToArray());
    }

    [Theory]
    [InlineData("native")]
    [InlineData("holder")]
    [InlineData("alternate")]
    [InlineData("tainted")]
    [InlineData("survivor")]
    [InlineData("target-successor")]
    public void EachIncompletePreparationBlocksArmCutAndPublication(string missing)
    {
        var prepared = RunTo(SharedCutBoundary.B2Prepared);
        var state = missing switch
        {
            "native" => prepared with { NativeSendUncertain = true },
            "holder" => prepared with { HolderUncertain = true },
            "alternate" => prepared with { Paths = prepared.Paths.SetItem(0, prepared.Paths[0] with { Epochs = SharedCutEpoch.Source | SharedCutEpoch.Alternate }) },
            "tainted" => prepared with { Paths = prepared.Paths.SetItem(0, prepared.Paths[0] with { Epochs = SharedCutEpoch.Source | SharedCutEpoch.Tainted }) },
            "target-successor" => prepared with { Paths = prepared.Paths.SetItem(0, prepared.Paths[0] with { Epochs = SharedCutEpoch.Source | SharedCutEpoch.Successor }) },
            _ => prepared with { Paths = prepared.Paths.SetItem(2, prepared.Paths[2] with { Epochs = SharedCutEpoch.Source }) },
        };
        CustodySharedCutModel.Apply(state, SharedCutAction.Arm).Accepted.ShouldBeFalse();
        CustodySharedCutModel.Apply(state with { Boundary = SharedCutBoundary.B3Armed }, SharedCutAction.Cut).Accepted.ShouldBeFalse();
        CustodySharedCutModel.Apply(state with { Boundary = SharedCutBoundary.B4Cut, RestoreBarrierVerified = true }, SharedCutAction.Publish).Accepted.ShouldBeFalse();
    }

    [Fact]
    public void SidecarLossLeavesOriginalHolderAndNativeSendUncertainUntilActualClosurePremises()
    {
        var state = Accept(RunTo(SharedCutBoundary.B1CustodyReserved), SharedCutAction.LoseSidecar);
        state = Accept(state, SharedCutAction.RestartApplication);
        state.HolderUncertain.ShouldBeTrue(); state.NativeSendUncertain.ShouldBeTrue();
        CustodySharedCutModel.Apply(state, SharedCutAction.CloseHolders).Accepted.ShouldBeFalse();
        CustodySharedCutModel.Apply(state, SharedCutAction.Arm).Accepted.ShouldBeFalse();
        Accept(state, SharedCutAction.DelayedDuplicateExecute).ShouldBe(state);
        CustodySharedCutModel.Apply(state, SharedCutAction.DelayedDuplicateKeyRelease).Accepted.ShouldBeFalse();
        state = Finish(state);
        state.CutCount.ShouldBe(1);
        state.SidecarLost.ShouldBeTrue();
    }

    [Theory]
    [InlineData("operation")][InlineData("batch")][InlineData("reservation")][InlineData("request")]
    [InlineData("tenant")][InlineData("object")][InlineData("purpose")][InlineData("alias")]
    [InlineData("version")][InlineData("lifecycle")][InlineData("store")][InlineData("contract")][InlineData("bytes")]
    public void CompleteOriginalSubstitutionOrRootReissueNeverAllocatesReplacement(string change)
    {
        var state = RunTo(SharedCutBoundary.B1CustodyReserved);
        if (change == "purpose")
        {
            Should.Throw<ArgumentException>(() => Original([Identity(0) with { Purpose = PlatformKeyPurpose.ExportEnvelopeKey }]));
            return;
        }
        var identities = state.Original.Targets.ToArray();
        identities[0] = change switch
        {
            "object" => identities[0] with { ObjectId = "interaction-0000-changed" },
            "alias" => identities[0] with { KeyAlias = "alias-changed" },
            "version" => identities[0] with { KeyVersion = "root-v2" },
            "lifecycle" => identities[0] with { LifecycleVersion = "changed" },
            "store" => identities[0] with { StoreTarget = "changed" },
            "contract" => identities[0] with { ContractVersion = "changed" },
            _ => identities[0],
        };
        if (change == "tenant") { identities = identities.Select(i => i with { TenantId = "other-tenant" }).ToArray(); }
        var changed = Original(identities, change);
        CustodySharedCutModel.Apply(state, SharedCutAction.DelayedDuplicateExecute, original: changed).Reason.ShouldBe("original-conflict");
        CustodySharedCutModel.Apply(state, SharedCutAction.Lookup, original: changed).Reason.ShouldBe("original-conflict");
        var final = Finish(state);
        CustodySharedCutModel.Apply(final, SharedCutAction.ReissueRoot, original: changed).Accepted.ShouldBeFalse();
        final.Original.ShouldBe(state.Original);
    }

    [Fact]
    public void ManifestMustHaveExactDigestSortedMembershipAndOwnedImmutableBytes()
    {
        var original = Original([Identity(0), Identity(1)]);
        Should.Throw<ArgumentException>(() => Original(original.Targets.Reverse().ToArray()));
        Should.Throw<ArgumentException>(() => Original([Identity(0), Identity(0)]));
        Should.Throw<ArgumentException>(() => new SharedCutOriginal("op", "batch", "reserve", "request", "wrong", original.Targets, [1]));
        byte[] bytes = [1, 2, 3]; var copy = new SharedCutOriginal("op", "batch", "reserve", "request", original.ManifestDigest, original.Targets, bytes);
        bytes[0] = 9; copy.SymbolicOriginal[0].ShouldBe((byte)1);
    }

    [Fact]
    public void PublicationMergesWithCurrentSurvivorAdmissionAndDoesNotRewindIt()
    {
        var state = RunTo(SharedCutBoundary.B3Armed);
        state = Accept(state, SharedCutAction.AllocateSurvivorRoot);
        state = Accept(state, SharedCutAction.Rotate);
        int head = state.Head;
        state = Finish(state);
        state.Head.ShouldBeGreaterThan(head); state.NewSurvivorAdmitted.ShouldBeTrue();
        state.Paths.Last().Selected.ShouldBeFalse(); state.Paths.Last().Epochs.ShouldBe(SharedCutEpoch.Successor);
        var oldApp = Accept(state, SharedCutAction.RestoreApplication);
        oldApp.Paths.ShouldBe(state.Paths); oldApp.Head.ShouldBe(state.Head);
        CustodySharedCutModel.Violations(oldApp).ShouldBeEmpty();
    }

    [Fact]
    public void BoundedExplorationAndUnsafeCounterexamplesProduceValueFreeEvidence()
    {
        const int maximumDepth = 16, maximumStates = 150000;
        var queue = new Queue<(SharedCutState State, ImmutableArray<string> Trace)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var initial = Initial(); queue.Enqueue((initial, [])); seen.Add(CustodySharedCutModel.Fingerprint(initial));
        long transitions = 0, refused = 0; int depthReached = 0, publications = 0, cuts = 0;
        while (queue.TryDequeue(out var item))
        {
            var (state, trace) = item; depthReached = Math.Max(depthReached, trace.Length);
            if (state.Boundary == SharedCutBoundary.B5Published) { publications++; }
            if (state.Boundary == SharedCutBoundary.B4Cut) { cuts++; }
            if (trace.Length == maximumDepth) { continue; }
            foreach (var action in Enum.GetValues<SharedCutAction>())
            {
                transitions++;
                var result = CustodySharedCutModel.Apply(state, action, state.Head);
                var nextTrace = trace.Add(action + ":" + result.Reason);
                CustodySharedCutModel.Violations(result.State).ShouldBeEmpty(string.Join(" -> ", nextTrace));
                result.State.Head.ShouldBeGreaterThanOrEqualTo(state.Head);
                result.State.Original.ShouldBe(state.Original);
                if (!state.PublishedOriginal.IsEmpty) { result.State.PublishedOriginal.ToArray().ShouldBe(state.PublishedOriginal.ToArray()); }
                if (!result.Accepted) { refused++; result.State.ShouldBe(state); }
                if (action == SharedCutAction.Lookup) { result.State.ShouldBe(state); }
                string fingerprint = CustodySharedCutModel.Fingerprint(result.State);
                if (seen.Add(fingerprint))
                {
                    seen.Count.ShouldBeLessThan(maximumStates, "the explicit state cap must not truncate exploration");
                    queue.Enqueue((result.State, nextTrace));
                }
            }
        }
        publications.ShouldBeGreaterThan(0); cuts.ShouldBeGreaterThan(0); depthReached.ShouldBe(maximumDepth);
        var boundaryEvidence = BoundaryLossSchedules();
        var counterexamples = UnsafeCounterexamples();
        var report = new
        {
            Kind = "OfflineModelOnly", LiveReady = false, DependencyAvailable = false,
            Bounds = new { MaximumDepth = maximumDepth, MaximumStates = maximumStates, Targets = 2, Survivors = 1,
                Epochs = 4, OriginalOperations = 1, Rotations = 1, NewSurvivorAllocations = 1 },
            States = seen.Count, DepthReached = depthReached, Transitions = transitions, RefusedTransitions = refused,
            ReachableCutStates = cuts, ReachablePublishedStates = publications,
            ExplicitVectors = new[] { new { Targets = 1, Survivors = 2 }, new { Targets = 1000, Survivors = 0 } },
            BoundaryLossSchedules = boundaryEvidence, UnsafeCounterexamples = counterexamples,
            Assumptions = new[]
            {
                "Linearizable independent forward authority and retained exact original survive application restore; no authority replacement.",
                "Cut removes one epoch for the whole manifest atomically and irreversibly; this is a physical premise, not implemented evidence.",
                "Complete graph contains every copy; retiring an epoch means permanent physical retirement, not disablement or missing-key denial.",
                "CloseNativeSend/CloseHolders mean real original I/O completion and all SDK/sidecar/component/UDS/gRPC/TLS buffer closure or accepted termination proof.",
                "Successful pins/releases and original reservation have independently authenticated current owner ordering; signature verification is outside this model.",
                "Restore-barrier verification and survivor recovery are independently correct abstract premises; result bytes are symbolic value-free test data.",
            },
            Unproved = new[] { "physical provider/component feasibility", "real all-copy inventory", "transport retry/buffer closure",
                "Dapr request/frame limits and SDK interoperability", "independent runtime enrollment/current authority", "operational retention/capacity", "owner Live qualification" },
        };
        string json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        output.WriteLine(json);
        string? path = Environment.GetEnvironmentVariable("HEXALITH_CUSTODY_MODEL_REPORT");
        if (!string.IsNullOrWhiteSpace(path)) { File.WriteAllText(path, json + Environment.NewLine); }
    }

    private static List<object> BoundaryLossSchedules()
    {
        var evidence = new List<object>();
        foreach (var boundary in Enum.GetValues<SharedCutBoundary>())
        {
            var original = RunTo(boundary);
            foreach (var fault in new[] { "crash", "lost-response", "old-snapshot", "sidecar-loss" })
            {
                var state = original; var trace = new List<string> { boundary.ToString(), fault };
                if (fault == "old-snapshot") { state = Accept(state, SharedCutAction.RestoreApplication); }
                else if (fault == "sidecar-loss")
                {
                    var loss = CustodySharedCutModel.Apply(state, SharedCutAction.LoseSidecar);
                    state = loss.State; trace.Add(loss.Reason);
                }
                else { state = Accept(state, SharedCutAction.RestartApplication); }
                state = Accept(state, SharedCutAction.LoseAuthority);
                foreach (var action in new[] { SharedCutAction.Arm, SharedCutAction.Cut, SharedCutAction.Publish, SharedCutAction.ReplaceAuthority })
                { CustodySharedCutModel.Apply(state, action).Accepted.ShouldBeFalse(); }
                CustodySharedCutModel.Observe(state).Status.ShouldBe("Unavailable");
                Accept(state, SharedCutAction.Lookup).ShouldBe(state);
                state = Accept(state, SharedCutAction.RecoverSameAuthority);
                state.Head.ShouldBe(original.Head);
                Accept(state, SharedCutAction.Lookup).ShouldBe(state);
                Accept(state, SharedCutAction.DelayedDuplicateExecute).ShouldBe(state);
                CustodySharedCutModel.Apply(state, SharedCutAction.DelayedDuplicateKeyRelease).Accepted.ShouldBeFalse();
                state = Finish(state);
                state.CutCount.ShouldBe(1); state.PublishedOriginal.ToArray().ShouldBe(original.Original.SymbolicOriginal.ToArray());
                CustodySharedCutModel.Violations(state).ShouldBeEmpty();
                trace.Add("same-independent-head/original -> lookup-only -> original-worker-resumes -> B5");
                evidence.Add(new { Boundary = boundary.ToString(), Fault = fault, Trace = trace, FinalHead = state.Head, CutCount = state.CutCount });
            }
        }
        return evidence;
    }

    private static List<object> UnsafeCounterexamples()
    {
        var armed = RunTo(SharedCutBoundary.B3Armed); var selected = armed.Paths[0];
        var partial = armed with { Paths = armed.Paths.SetItem(0, selected with { Epochs = SharedCutEpoch.None }) };
        var survivorLoss = armed with { Boundary = SharedCutBoundary.B4Cut, CutCount = 1,
            Paths = armed.Paths.Select(p => p with { Epochs = SharedCutEpoch.None }).ToImmutableArray() };
        var early = armed with { PublishedOriginal = armed.Original.SymbolicOriginal };
        var unresolved = armed with { HolderUncertain = true, NativeSendUncertain = true };
        var resurrected = Finish(armed) with { Paths = armed.Paths };
        var cases = new[]
        {
            ("partial-target-cut", partial, "B0 -> B1 -> B2 -> B3 -> UNSAFE per-target delete(0); target(1) still reachable"),
            ("survivor-path-lost", survivorLoss, "B0 -> B1 -> UNSAFE skip survivor copy -> arm -> shared cut"),
            ("false-completion", early, "B0 -> B1 -> B2 -> B3 -> UNSAFE publish before physical cut/restore proof"),
            ("armed-with-unresolved-copy-or-holder", unresolved, "B1 -> sidecar loss -> UNSAFE treat timeout/restart as closure -> arm"),
            ("cut-left-selected-path", resurrected, "B5 -> UNSAFE restore provider epoch from old application snapshot -> targets revive"),
        };
        return cases.Select(c =>
        {
            var violations = CustodySharedCutModel.Violations(c.Item2);
            violations.ShouldContain(c.Item1);
            return (object)new { ExpectedViolation = c.Item1, Trace = c.Item3, Violations = violations };
        }).ToList();
    }

    private static SharedCutState Initial(int targets = 2, int survivors = 1)
        => CustodySharedCutModel.Create(Original(Enumerable.Range(0, targets).Select(Identity).ToArray()),
            Enumerable.Range(targets, survivors).Select(Identity));

    private static CustodyKeyObjectIdentity Identity(int index) => new("model-tenant", $"interaction-{index:D4}",
        PlatformKeyPurpose.InteractionRootDek, "model-alias", "original-root-v1", "model-lifecycle", "opaque-model-store", "model-contract");

    private static SharedCutOriginal Original(IReadOnlyList<CustodyKeyObjectIdentity> identities, string change = "")
    {
        string digest = DeletionBatchCapabilityIdentity.TargetManifestDigest(identities.Select(i => new ProtectionTarget(i.TenantId, i.ObjectId, i.KeyAlias)).ToArray());
        return new(change == "operation" ? "changed-operation" : "original-operation", change == "batch" ? "changed-batch" : "original-batch",
            change == "reservation" ? "changed-reservation" : "original-reservation", change == "request" ? "changed-request" : "original-request",
            digest, identities, Encoding.UTF8.GetBytes("OFFLINE MODEL ONLY: ordered symbolic outcomes/signature; " + digest + (change == "bytes" ? "changed" : "")));
    }

    private static SharedCutState Accept(SharedCutState state, SharedCutAction action)
    {
        var step = CustodySharedCutModel.Apply(state, action, state.Head);
        step.Accepted.ShouldBeTrue(action + ": " + step.Reason); return step.State;
    }

    private static SharedCutState RunTo(SharedCutBoundary boundary)
    {
        var state = Initial();
        foreach (var action in SuccessfulSchedule)
        {
            if (state.Boundary == boundary) { return state; }
            state = Accept(state, action);
        }
        state.Boundary.ShouldBe(boundary); return state;
    }

    private static SharedCutState Finish(SharedCutState state)
    {
        foreach (var action in SuccessfulSchedule)
        {
            var step = CustodySharedCutModel.Apply(state, action, state.Head);
            if (step.Accepted) { state = step.State; }
        }
        state.Boundary.ShouldBe(SharedCutBoundary.B5Published); return state;
    }
}
