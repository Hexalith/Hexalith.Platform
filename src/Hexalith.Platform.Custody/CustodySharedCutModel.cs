using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Custody;

// Offline I1/F0 symbols only. No provider, registration, physical proof, key bytes or network.
internal enum SharedCutBoundary { BeforeDispatch, Dispatched, B0ProtectionReserved, B1CustodyReserved, B2Prepared, B3Armed, B4Cut, B5Published }
internal enum SharedCutHoldKind { Effective, Unknown, Released }
internal enum SharedCutNativePhase { Prepared, SendCommitted, ResultDurable, Closed }
[Flags]
internal enum SharedCutEpoch { None = 0, Source = 1, Successor = 2, Alternate = 4, Tainted = 8 }
internal enum SharedCutAction
{
    CommitNativeSend, FirstStagingDelivery, CompleteNativeResult, CloseNativeIntent, CloseHolders,
    DispatchGuard, ReserveProtection, ReserveCustody, PrepareSurvivors, RetireAlternate, RetireTainted,
    ConfirmPreparation, Arm, Cut, VerifyRestoreBarrier, Publish, EffectivePin, UnknownPin, ReleasePin,
    AllocateRoot, Rotate, LoseAuthority, RecoverSameAuthority, RestoreApplication, CrashApplication,
    RestartApplication, LoseAcknowledgment, LoseSidecar, DelayedDuplicateExecute, DelayedDuplicateKeyRelease,
    ReplaceAuthority, Lookup,
}

/// <summary>Complete immutable original identity; supplied manifest order and digest are checked.</summary>
internal sealed class SharedCutOriginal
{
    internal SharedCutOriginal(string operationId, string batchId, string reservationId, string requestDigest,
        string manifestDigest, IEnumerable<CustodyKeyObjectIdentity> targets, ReadOnlySpan<byte> symbolicOriginal)
    {
        foreach (string value in new[] { operationId, batchId, reservationId, requestDigest, manifestDigest })
        { ArgumentException.ThrowIfNullOrWhiteSpace(value); }
        Targets = targets.ToImmutableArray();
        if (Targets.Length is < 1 or > 1000) { throw new ArgumentException("Model manifest must contain 1–1,000 targets."); }
        foreach (var identity in Targets)
        {
            CustodyKeyLifecycleActor.ValidateIdentity(identity);
            if (identity.Purpose != PlatformKeyPurpose.InteractionRootDek || identity.TenantId != Targets[0].TenantId)
            { throw new ArgumentException("One tenant's complete interaction-root identities are required."); }
        }
        var vector = Targets.Select(i => new ProtectionTarget(i.TenantId, i.ObjectId, i.KeyAlias)).ToArray();
        if (DeletionBatchCapabilityIdentity.TargetManifestDigest(vector) != manifestDigest
            || vector.Zip(vector.Skip(1)).Any(p => Compare(p.First, p.Second) >= 0)
            || Targets.Select(i => i.ObjectId).Distinct(StringComparer.Ordinal).Count() != Targets.Length)
        { throw new ArgumentException("Exact sorted original manifest required."); }
        OperationId = operationId; BatchId = batchId; ReservationId = reservationId;
        RequestDigest = requestDigest; ManifestDigest = manifestDigest;
        SymbolicOriginal = symbolicOriginal.ToArray().ToImmutableArray();
        if (SymbolicOriginal.IsEmpty) { throw new ArgumentException("Missing symbolic original."); }
        IdentityDigest = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        { operationId, batchId, reservationId, requestDigest, manifestDigest, Targets, SymbolicOriginal })));
    }
    internal string OperationId { get; }
    internal string BatchId { get; }
    internal string ReservationId { get; }
    internal string RequestDigest { get; }
    internal string ManifestDigest { get; }
    internal ImmutableArray<CustodyKeyObjectIdentity> Targets { get; }
    internal ImmutableArray<byte> SymbolicOriginal { get; }
    internal string IdentityDigest { get; }
    private static int Compare(ProtectionTarget a, ProtectionTarget b)
    {
        int result = StringComparer.Ordinal.Compare(a.TenantId, b.TenantId);
        if (result == 0) { result = StringComparer.Ordinal.Compare(a.AgentInteractionId, b.AgentInteractionId); }
        return result == 0 ? StringComparer.Ordinal.Compare(a.TargetProtectionKeyAlias, b.TargetProtectionKeyAlias) : result;
    }
}

internal sealed record SharedCutPath(CustodyKeyObjectIdentity Identity, bool Selected, SharedCutEpoch Epochs);
internal sealed record SharedCutHold(string HoldId, CustodyKeyObjectIdentity Identity, SharedCutHoldKind Kind);
internal sealed record SharedCutNativeIntent(string IntentId, string ProducerIncarnation, string DeliveryId,
    SharedCutEpoch Destination, SharedCutNativePhase Phase, bool FirstDelivered);

/// <summary>All members except ApplicationHead/Running and fault markers belong to the independent authority.</summary>
internal sealed record SharedCutState(SharedCutOriginal Original, ImmutableArray<CustodyKeyObjectIdentity> Survivors,
    ImmutableHashSet<string> AllocationExclusions, ImmutableArray<SharedCutPath> Paths)
{
    internal SharedCutBoundary Boundary { get; init; }
    internal int Head { get; init; }
    internal int ApplicationHead { get; init; }
    internal bool ApplicationRunning { get; init; } = true;
    internal bool AuthorityAvailable { get; init; } = true;
    internal ImmutableArray<SharedCutHold> Holds { get; init; } = [];
    internal SharedCutNativeIntent Native { get; init; } = new("original-intent", "original-producer", "original-delivery", SharedCutEpoch.Tainted, SharedCutNativePhase.Prepared, false);
    internal bool HoldersClosed { get; init; }
    internal bool AlternateRetired { get; init; }
    internal bool TaintedRetired { get; init; }
    internal bool SidecarLost { get; init; }
    internal string? LostSidecarIncarnation { get; init; }
    internal bool CrashInjected { get; init; }
    internal bool AcknowledgmentLost { get; init; }
    internal bool OldSnapshotRestored { get; init; }
    internal bool RestoreBarrierVerified { get; init; }
    internal int Rotation { get; init; }
    internal int CutCount { get; init; }
    internal bool NewSurvivorAdmitted { get; init; }
    internal ImmutableArray<byte> PublishedOriginal { get; init; } = [];
    internal bool Frozen => Boundary is >= SharedCutBoundary.B1CustodyReserved and < SharedCutBoundary.B3Armed;
}
internal sealed record SharedCutStep(SharedCutState State, bool Accepted, string Reason);
internal sealed record SharedCutObservation(string Status, ImmutableArray<byte> SymbolicOriginal);

/// <summary>Conditional copy/protocol feasibility model; every physical closure/cut action is an unproved premise.</summary>
internal static class CustodySharedCutModel
{
    internal static SharedCutState Create(SharedCutOriginal original, IEnumerable<CustodyKeyObjectIdentity> survivors)
    {
        var controls = survivors.ToImmutableArray();
        if (original.Targets.Length + controls.Length > 1000) { throw new ArgumentException("1,000 total registrations exceeded."); }
        foreach (var i in controls)
        {
            CustodyKeyLifecycleActor.ValidateIdentity(i);
            if (i.Purpose != PlatformKeyPurpose.InteractionRootDek || i.TenantId != original.Targets[0].TenantId)
            { throw new ArgumentException("Survivor identity is not owned by this tenant."); }
        }
        var all = original.Targets.Concat(controls).ToArray();
        if (all.Select(AllocationKey).Distinct(StringComparer.Ordinal).Count() != all.Length)
        { throw new ArgumentException("An interaction is already allocated."); }
        return new(original, controls, all.Select(AllocationKey).ToImmutableHashSet(StringComparer.Ordinal),
            all.Select(i => new SharedCutPath(i, original.Targets.Contains(i), SharedCutEpoch.Source | SharedCutEpoch.Alternate)).ToImmutableArray());
    }

    internal static SharedCutStep Apply(SharedCutState state, SharedCutAction action, int? expectedHead = null,
        SharedCutOriginal? original = null, CustodyKeyObjectIdentity? identity = null, string? holdId = null)
    {
        SharedCutStep Refuse(string reason) => new(state, false, reason);
        SharedCutStep Change(SharedCutState next, string reason)
            => new(next with { Head = state.Head + 1, ApplicationHead = state.Head + 1 }, true, reason);
        SharedCutStep Fault(SharedCutState next, string reason) => new(next, true, reason);
        if (original is not null && original.IdentityDigest != state.Original.IdentityDigest) { return Refuse("original-conflict"); }
        if (action is SharedCutAction.Lookup or SharedCutAction.DelayedDuplicateExecute)
        { return new(state, true, Observe(state).Status + ":original-read-only-no-new-attempt"); }
        // An already SendCommitted transport can deliver while application/authority calls are down.
        // This is an observation by the admitted original worker, never permission for a new send.
        // Its independent forward revision changes; a crashed/restored application learns it only on reconciliation.
        if (action == SharedCutAction.FirstStagingDelivery)
        {
            return state.Native.Phase == SharedCutNativePhase.SendCommitted && !state.Native.FirstDelivered
                ? new(state with { Head = state.Head + 1, Native = state.Native with { FirstDelivered = true }, TaintedRetired = false,
                    Paths = state.Paths.SetItem(0, state.Paths[0] with { Epochs = state.Paths[0].Epochs | state.Native.Destination }) }, true,
                    "delayed-first-original-delivery-taints-destination-even-during-outage")
                : Refuse("original-delivery-already-observed-or-actually-completed");
        }
        // Environment events do not advance or replace the independent authority. Each marker records the actual fault.
        switch (action)
        {
            case SharedCutAction.LoseAuthority: return Fault(state with { AuthorityAvailable = false }, "current-authority-lost");
            case SharedCutAction.RecoverSameAuthority: return Fault(state with { AuthorityAvailable = true }, "same-authority-recovered");
            case SharedCutAction.RestoreApplication: return Fault(state with { ApplicationHead = 0, OldSnapshotRestored = true }, "old-application-snapshot-restored");
            case SharedCutAction.CrashApplication: return Fault(state with { ApplicationRunning = false, CrashInjected = true }, "application-process-crashed");
            case SharedCutAction.RestartApplication: return Fault(state with { ApplicationRunning = true,
                ApplicationHead = state.AuthorityAvailable ? state.Head : state.ApplicationHead },
                state.AuthorityAvailable ? "same-authority-current-revision-reconciled" : "process-restarted-current-authority-still-unavailable");
            case SharedCutAction.LoseAcknowledgment: return Fault(state with { AcknowledgmentLost = true }, "original-response-acknowledgment-lost");
            case SharedCutAction.LoseSidecar: return Fault(state with { SidecarLost = true, LostSidecarIncarnation = "original-sidecar" },
                state.HoldersClosed ? "original-sidecar-lost-after-buffer-closure" : "original-sidecar-lost-buffers-still-unresolved");
        }
        if (!state.AuthorityAvailable) { return Refuse("current-authority-unavailable"); }
        if (!state.ApplicationRunning) { return Refuse("application-not-running"); }
        if (!expectedHead.HasValue) { return Refuse("expected-current-revision-required"); }
        if (expectedHead.Value != state.Head) { return Refuse("stale-current-revision"); }
        if (action == SharedCutAction.ReplaceAuthority) { return Refuse("authority-replacement-unsupported"); }
        if (action == SharedCutAction.DelayedDuplicateKeyRelease) { return Refuse("original-delivery-never-replayed"); }
        if (!Violations(state).IsEmpty) { return Refuse("invalid-independent-state"); }
        if (action == SharedCutAction.AllocateRoot)
        {
            if (identity is null) { return Refuse("complete-allocation-identity-required"); }
            CustodyKeyLifecycleActor.ValidateIdentity(identity);
            if (identity.Purpose != PlatformKeyPurpose.InteractionRootDek || identity.TenantId != state.Original.Targets[0].TenantId)
            { return Refuse("allocation-owner-mismatch"); }
            if (state.AllocationExclusions.Contains(AllocationKey(identity))) { return Refuse("permanent-interaction-allocation-exclusion"); }
            if (state.Frozen || state.NewSurvivorAdmitted || state.AllocationExclusions.Count >= 1000)
            { return Refuse("tenant-freeze-or-registration-bound"); }
            var epoch = state.Boundary >= SharedCutBoundary.B3Armed ? SharedCutEpoch.Successor : SharedCutEpoch.Source;
            return Change(state with { NewSurvivorAdmitted = true, Survivors = state.Survivors.Add(identity),
                AllocationExclusions = state.AllocationExclusions.Add(AllocationKey(identity)),
                Paths = state.Paths.Add(new(identity, false, epoch)) }, "fresh-survivor-allocated-under-" + epoch);
        }
        if (action is SharedCutAction.EffectivePin or SharedCutAction.UnknownPin)
        {
            if (identity is null || string.IsNullOrWhiteSpace(holdId) || !state.Paths.Any(p => p.Identity == identity)) { return Refuse("exact-hold-identity-required"); }
            if (state.Holds.Any(h => h.HoldId == holdId)) { return Refuse("hold-id-already-retained"); }
            if (state.Original.Targets.Contains(identity) && state.Boundary >= SharedCutBoundary.Dispatched) { return Refuse("selected-pin-after-guard-dispatch"); }
            return Change(state with { Holds = state.Holds.Add(new(holdId, identity,
                action == SharedCutAction.EffectivePin ? SharedCutHoldKind.Effective : SharedCutHoldKind.Unknown)) }, "exact-identity-hold-registered");
        }
        if (action == SharedCutAction.ReleasePin)
        {
            int index = -1;
            for (int i = 0; i < state.Holds.Length; i++)
            { if (state.Holds[i].HoldId == holdId && state.Holds[i].Identity == identity && state.Holds[i].Kind != SharedCutHoldKind.Released) { index = i; } }
            return index < 0 ? Refuse("exact-original-hold-release-required")
                : Change(state with { Holds = state.Holds.SetItem(index, state.Holds[index] with { Kind = SharedCutHoldKind.Released }) }, "exact-original-hold-released");
        }
        if (action == SharedCutAction.Rotate)
        {
            return state.Frozen || state.Rotation != 0 ? Refuse("rotation-frozen-or-bounded")
                : Change(state with { Rotation = 1 }, "rotation-preserves-original-identity-and-paths");
        }
        if (SelectedHeld(state)) { return Refuse("effective-or-unknown-selected-hold"); }
        switch (action)
        {
            case SharedCutAction.CommitNativeSend when state.Native.Phase == SharedCutNativePhase.Prepared && !state.Frozen && state.Boundary < SharedCutBoundary.B3Armed:
                return Change(state with { Native = state.Native with { Phase = SharedCutNativePhase.SendCommitted } }, "original-send-committed-once-before-freeze");
            case SharedCutAction.CompleteNativeResult when state.Native.Phase == SharedCutNativePhase.SendCommitted:
                return Change(state with { Native = state.Native with { Phase = SharedCutNativePhase.ResultDurable } }, "actual-original-producer-io-result-durable");
            case SharedCutAction.CloseNativeIntent when state.Native.Phase == SharedCutNativePhase.ResultDurable:
                return Change(state with { Native = state.Native with { Phase = SharedCutNativePhase.Closed } }, "actual-original-producer-completion-no-pending-delivery");
            case SharedCutAction.CloseHolders when state.Native.Phase == SharedCutNativePhase.Closed && !state.HoldersClosed:
                return Change(state with { HoldersClosed = true }, "actual-original-sdk-sidecar-transport-component-buffer-closure");
            case SharedCutAction.DispatchGuard when state.Boundary == SharedCutBoundary.BeforeDispatch:
                return Change(state with { Boundary = SharedCutBoundary.Dispatched }, "owner-guard-dispatched-before-B0");
            case SharedCutAction.ReserveProtection when state.Boundary == SharedCutBoundary.Dispatched:
                return Change(state with { Boundary = SharedCutBoundary.B0ProtectionReserved }, "B0-exact-owner-protection-reservation");
            case SharedCutAction.ReserveCustody when state.Boundary == SharedCutBoundary.B0ProtectionReserved && state.Native.Phase != SharedCutNativePhase.Prepared:
                return Change(state with { Boundary = SharedCutBoundary.B1CustodyReserved }, "B1-complete-original-whole-tenant-freeze");
            case SharedCutAction.PrepareSurvivors when state.Frozen && state.Paths.Any(p => !p.Selected && !p.Epochs.HasFlag(SharedCutEpoch.Successor)):
                return Change(state with { Paths = state.Paths.Select(p => p.Selected ? p : p with { Epochs = p.Epochs | SharedCutEpoch.Successor }).ToImmutableArray() }, "all-original-survivors-copied-to-successor");
            case SharedCutAction.RetireAlternate when state.Frozen && Drained(state) && !state.AlternateRetired: return Retire(SharedCutEpoch.Alternate);
            case SharedCutAction.RetireTainted when state.Frozen && Drained(state) && !state.TaintedRetired: return Retire(SharedCutEpoch.Tainted);
            case SharedCutAction.ConfirmPreparation when state.Boundary == SharedCutBoundary.B1CustodyReserved && Prepared(state):
                return Change(state with { Boundary = SharedCutBoundary.B2Prepared }, "B2-complete-copy-and-holder-preparation");
            case SharedCutAction.Arm when state.Boundary == SharedCutBoundary.B2Prepared && Prepared(state):
                return Change(state with { Boundary = SharedCutBoundary.B3Armed }, "B3-current-successor-adopted-selected-fenced");
            case SharedCutAction.Cut when state.Boundary == SharedCutBoundary.B3Armed && Prepared(state):
                return Change(state with { Boundary = SharedCutBoundary.B4Cut, CutCount = 1,
                    Paths = state.Paths.Select(p => p with { Epochs = p.Epochs & ~SharedCutEpoch.Source }).ToImmutableArray() }, "B4-ASSUMED-single-shared-physical-cut");
            case SharedCutAction.VerifyRestoreBarrier when state.Boundary == SharedCutBoundary.B4Cut && !state.RestoreBarrierVerified && Closure(state):
                return Change(state with { RestoreBarrierVerified = true }, "independent-survivor-recovery-and-restore-barrier-premise");
            case SharedCutAction.Publish when state.Boundary == SharedCutBoundary.B4Cut && state.RestoreBarrierVerified && Closure(state):
                return Change(state with { Boundary = SharedCutBoundary.B5Published, PublishedOriginal = state.Original.SymbolicOriginal }, "B5-exact-symbolic-original-published");
            default: return Refuse("boundary-or-incomplete-preparation");
        }
        SharedCutStep Retire(SharedCutEpoch epoch) => Change(state with
        {
            AlternateRetired = state.AlternateRetired || epoch == SharedCutEpoch.Alternate,
            TaintedRetired = state.TaintedRetired || epoch == SharedCutEpoch.Tainted,
            Paths = state.Paths.Select(p => p with { Epochs = p.Epochs & ~epoch }).ToImmutableArray(),
        }, "assumed-permanent-physical-retirement-" + epoch);
    }

    internal static SharedCutObservation Observe(SharedCutState state) => !state.AuthorityAvailable ? new("Unavailable", [])
        : state.Boundary == SharedCutBoundary.B5Published && Violations(state).IsEmpty
            ? new("ModelPublished", state.PublishedOriginal) : new("Unknown", []);

    internal static ImmutableArray<string> Violations(SharedCutState state)
    {
        var errors = ImmutableArray.CreateBuilder<string>();
        var selected = state.Paths.Where(p => p.Selected).ToArray();
        var controls = state.Paths.Where(p => !p.Selected).ToArray();
        if (!selected.Select(p => p.Identity).SequenceEqual(state.Original.Targets)) { errors.Add("original-vector-changed"); }
        if (controls.Length != state.Survivors.Length || !controls.Select(p => p.Identity).ToHashSet().SetEquals(state.Survivors)) { errors.Add("survivor-membership-changed"); }
        if (state.Paths.Any(p => !state.AllocationExclusions.Contains(AllocationKey(p.Identity)))
            || state.AllocationExclusions.Count != state.Original.Targets.Length + state.Survivors.Length) { errors.Add("permanent-allocation-inventory-changed"); }
        int reachable = selected.Count(p => p.Epochs != SharedCutEpoch.None);
        if (reachable != 0 && reachable != selected.Length) { errors.Add("partial-target-cut"); }
        if (state.Boundary < SharedCutBoundary.B4Cut && reachable == 0) { errors.Add("whole-selected-set-lost-before-B4"); }
        if (controls.Any(p => p.Epochs == SharedCutEpoch.None)) { errors.Add("survivor-path-lost"); }
        if ((state.Boundary < SharedCutBoundary.B4Cut && state.CutCount != 0) || (state.Boundary >= SharedCutBoundary.B4Cut && state.CutCount != 1))
        { errors.Add("cut-count-boundary-mismatch"); }
        if (state.Boundary >= SharedCutBoundary.B3Armed && (!Drained(state) || !state.AlternateRetired || !state.TaintedRetired
            || state.Paths.Any(p => (p.Epochs & (SharedCutEpoch.Alternate | SharedCutEpoch.Tainted)) != 0))) { errors.Add("armed-with-unresolved-copy-or-holder"); }
        if (state.Boundary == SharedCutBoundary.B3Armed && selected.Any(p => p.Epochs != SharedCutEpoch.Source)) { errors.Add("armed-selected-path-not-sole-source"); }
        if (state.Boundary >= SharedCutBoundary.B4Cut && controls.Any(p => p.Epochs != SharedCutEpoch.Successor)) { errors.Add("post-cut-survivor-not-successor-only"); }
        if (state.Boundary >= SharedCutBoundary.Dispatched && SelectedHeld(state)) { errors.Add("dispatch-conflicts-with-selected-hold"); }
        if (state.Boundary >= SharedCutBoundary.B4Cut && reachable != 0) { errors.Add("cut-left-selected-path"); }
        if ((!state.PublishedOriginal.IsEmpty || state.Boundary == SharedCutBoundary.B5Published)
            && (state.Boundary != SharedCutBoundary.B5Published || !state.RestoreBarrierVerified || !Closure(state))) { errors.Add("false-completion"); }
        if (state.Boundary == SharedCutBoundary.B5Published && !state.PublishedOriginal.SequenceEqual(state.Original.SymbolicOriginal)) { errors.Add("terminal-original-bytes-changed"); }
        if (state.Head < state.ApplicationHead) { errors.Add("application-ahead-of-independent-head"); }
        return errors.ToImmutable();
    }
    // Head is retained numerically in real transitions. Exploration quotients equivalent revision histories by
    // freshness only; stale/omitted numeric revisions are separately exercised, never authorized by this quotient.
    internal static string Fingerprint(SharedCutState s) => string.Join('|', (int)s.Boundary, s.ApplicationHead == s.Head,
        s.ApplicationRunning, s.AuthorityAvailable, s.Native.Phase, s.Native.FirstDelivered, s.HoldersClosed, s.AlternateRetired,
        s.TaintedRetired, s.SidecarLost, s.CrashInjected, s.AcknowledgmentLost, s.OldSnapshotRestored, s.RestoreBarrierVerified,
        s.Rotation, s.CutCount, s.NewSurvivorAdmitted, s.PublishedOriginal.IsEmpty,
        string.Join(',', s.Holds.Select(h => h.HoldId + ":" + h.Kind)), string.Join(',', s.Paths.Select(p => (int)p.Epochs)));
    private static string AllocationKey(CustodyKeyObjectIdentity i) => JsonSerializer.Serialize(new[] { i.TenantId, i.ObjectId });
    private static bool SelectedHeld(SharedCutState s) => s.Holds.Any(h => h.Kind != SharedCutHoldKind.Released && s.Original.Targets.Contains(h.Identity));
    private static bool Drained(SharedCutState s) => s.Native.Phase == SharedCutNativePhase.Closed && s.HoldersClosed;
    private static bool Prepared(SharedCutState s) => Drained(s) && s.AlternateRetired && s.TaintedRetired
        && s.Paths.All(p => p.Selected ? p.Epochs == SharedCutEpoch.Source : p.Epochs is SharedCutEpoch.Successor or (SharedCutEpoch.Source | SharedCutEpoch.Successor));
    private static bool Closure(SharedCutState s) => s.CutCount == 1 && Drained(s) && s.AlternateRetired && s.TaintedRetired
        && s.Paths.Where(p => p.Selected).Select(p => p.Identity).SequenceEqual(s.Original.Targets)
        && s.Paths.Count(p => !p.Selected) == s.Survivors.Length
        && s.Paths.Where(p => !p.Selected).Select(p => p.Identity).ToHashSet().SetEquals(s.Survivors)
        && s.Paths.All(p => p.Selected ? p.Epochs == SharedCutEpoch.None : p.Epochs == SharedCutEpoch.Successor);
}
