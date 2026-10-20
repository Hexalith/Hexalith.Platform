using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Custody;

// I1/F0 only. These symbols are an offline transition/copy-graph model, not a lifecycle
// provider, physical proof, receipt implementation, Dapr component or DI service.
internal enum SharedCutBoundary { BeforeB0, B0ProtectionReserved, B1CustodyReserved, B2Prepared, B3Armed, B4Cut, B5Published }
internal enum SharedCutHold { None, Effective, Unknown, Released }
[Flags]
internal enum SharedCutEpoch { None = 0, Source = 1, Successor = 2, Alternate = 4, Tainted = 8 }
internal enum SharedCutAction
{
    ReserveProtection, ReserveCustody, PrepareSurvivors, CloseNativeSend, CloseHolders,
    RetireAlternate, RetireTainted, ConfirmPreparation, Arm, Cut, VerifyRestoreBarrier, Publish,
    EffectivePin, UnknownPin, ReleasePin, LoseAuthority, RecoverSameAuthority, RestoreApplication,
    RestartApplication, LoseSidecar, DelayedDuplicateExecute, DelayedDuplicateKeyRelease, Rotate,
    ReissueRoot, AllocateSurvivorRoot, ReplaceAuthority, Lookup,
}

/// <summary>Complete immutable original identity; supplied order is checked, never silently sorted or edited.</summary>
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
            { throw new ArgumentException("Model requires one tenant's complete interaction-root identities."); }
        }
        var vector = Targets.Select(i => new ProtectionTarget(i.TenantId, i.ObjectId, i.KeyAlias)).ToArray();
        if (DeletionBatchCapabilityIdentity.TargetManifestDigest(vector) != manifestDigest
            || vector.Zip(vector.Skip(1)).Any(p => Compare(p.First, p.Second) >= 0))
        { throw new ArgumentException("Model requires the exact sorted original manifest."); }
        OperationId = operationId; BatchId = batchId; ReservationId = reservationId;
        RequestDigest = requestDigest; ManifestDigest = manifestDigest;
        // Arbitrary value-free test bytes representing a retained result/signature, never a real receipt.
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

/// <summary>Independent state and application snapshot are deliberately separate; no key/content bytes exist.</summary>
internal sealed record SharedCutState(SharedCutOriginal Original, ImmutableArray<SharedCutPath> Paths)
{
    internal SharedCutBoundary Boundary { get; init; }
    internal int Head { get; init; }
    internal int ApplicationHead { get; init; }
    internal bool AuthorityAvailable { get; init; } = true;
    internal SharedCutHold Hold { get; init; }
    internal bool NativeSendUncertain { get; init; } = true;
    internal bool HolderUncertain { get; init; } = true;
    internal bool SidecarLost { get; init; }
    internal bool RestoreBarrierVerified { get; init; }
    internal int Rotation { get; init; }
    internal int CutCount { get; init; }
    internal bool NewSurvivorAdmitted { get; init; }
    internal ImmutableArray<byte> PublishedOriginal { get; init; } = [];
    internal bool Frozen => Boundary is >= SharedCutBoundary.B1CustodyReserved and < SharedCutBoundary.B3Armed;
}

internal sealed record SharedCutStep(SharedCutState State, bool Accepted, string Reason);
internal sealed record SharedCutObservation(string Status, ImmutableArray<byte> SymbolicOriginal);

/// <summary>
/// Executable conditional feasibility argument. Cut and closure evidence are abstract premises,
/// not implemented physical or transport guarantees. No external effects or authority enrollment.
/// </summary>
internal static class CustodySharedCutModel
{
    internal static SharedCutState Create(SharedCutOriginal original, IEnumerable<CustodyKeyObjectIdentity> survivors)
    {
        var controls = survivors.ToImmutableArray();
        if (original.Targets.Length + controls.Length > 1000) { throw new ArgumentException("Existing 1,000-registration model bound exceeded."); }
        foreach (var i in controls)
        {
            CustodyKeyLifecycleActor.ValidateIdentity(i);
            if (i.Purpose != PlatformKeyPurpose.InteractionRootDek || i.TenantId != original.Targets[0].TenantId || original.Targets.Any(t => t.ObjectId == i.ObjectId))
            { throw new ArgumentException("Survivor must be a distinct interaction in the same tenant."); }
        }
        if (controls.Distinct().Count() != controls.Length) { throw new ArgumentException("Duplicate survivor."); }
        return new(original, original.Targets.Select(i => new SharedCutPath(i, true, SharedCutEpoch.Source | SharedCutEpoch.Alternate | SharedCutEpoch.Tainted))
            .Concat(controls.Select(i => new SharedCutPath(i, false, SharedCutEpoch.Source | SharedCutEpoch.Alternate | SharedCutEpoch.Tainted))).ToImmutableArray());
    }

    internal static SharedCutStep Apply(SharedCutState state, SharedCutAction action, int? expectedHead = null, SharedCutOriginal? original = null)
    {
        SharedCutStep Refuse(string reason) => new(state, false, reason);
        SharedCutStep Change(SharedCutState next, string reason, bool advance = true)
            => new(next with { Head = state.Head + (advance ? 1 : 0), ApplicationHead = advance ? state.Head + 1 : next.ApplicationHead }, true, reason);
        // Recovery/lookup cannot choose another original, even following restore or authority loss.
        if (original is not null && original.IdentityDigest != state.Original.IdentityDigest) { return Refuse("original-conflict"); }
        if (action == SharedCutAction.Lookup) { return new(state, true, Observe(state).Status); }
        if (action == SharedCutAction.LoseAuthority) { return Change(state with { AuthorityAvailable = false }, "current-authority-lost", false); }
        if (action == SharedCutAction.RecoverSameAuthority) { return Change(state with { AuthorityAvailable = true }, "same-independent-authority", false); }
        if (action == SharedCutAction.RestoreApplication) { return Change(state with { ApplicationHead = 0 }, "old-app-config-key-receipt-snapshot", false); }
        if (action == SharedCutAction.RestartApplication) { return Change(state with { ApplicationHead = state.Head }, "reconcile-surviving-head", false); }
        // Losing an incarnation is not proof that its old send/buffers finished. This is not a new native send.
        if (action == SharedCutAction.LoseSidecar)
        {
            return state.HolderUncertain && !state.SidecarLost
                ? Change(state with { SidecarLost = true }, "old-sidecar-holder-still-uncertain", false)
                : Refuse("no-open-sidecar-holder");
        }
        if (!state.AuthorityAvailable) { return Refuse("current-authority-unavailable"); }
        if (expectedHead.HasValue && expectedHead.Value != state.Head) { return Refuse("stale-current-head"); }
        if (action == SharedCutAction.ReplaceAuthority) { return Refuse("first-release-authority-replacement-unsupported"); }
        if (action == SharedCutAction.DelayedDuplicateExecute) { return new(state, true, "original-status-only-no-new-send"); }
        if (action == SharedCutAction.DelayedDuplicateKeyRelease) { return Refuse("admitted-send-not-replayed-no-new-key-holder"); }
        if (action == SharedCutAction.ReissueRoot) { return Refuse("permanent-interaction-allocation-exclusion"); }
        if (action == SharedCutAction.AllocateSurvivorRoot)
        {
            if (state.Frozen || state.Boundary < SharedCutBoundary.B3Armed || state.NewSurvivorAdmitted || state.Paths.Length >= 1000) { return Refuse("whole-tenant-freeze-or-no-current-successor"); }
            var identity = state.Original.Targets[0] with { ObjectId = state.Original.Targets[0].ObjectId + "/model-new-survivor" };
            if (state.Paths.Any(p => p.Identity.ObjectId == identity.ObjectId)) { return Refuse("interaction-already-allocated"); }
            return Change(state with { NewSurvivorAdmitted = true, Paths = state.Paths.Add(new(identity, false, SharedCutEpoch.Successor)) }, "survivor-only-allocation-under-successor");
        }
        if (action is SharedCutAction.EffectivePin or SharedCutAction.UnknownPin)
        {
            if (state.Boundary != SharedCutBoundary.BeforeB0 || state.Hold != SharedCutHold.None) { return Refuse("post-dispatch-pin-cannot-conflict"); }
            return Change(state with { Hold = action == SharedCutAction.EffectivePin ? SharedCutHold.Effective : SharedCutHold.Unknown }, "prior-restrictive-pin");
        }
        if (action == SharedCutAction.ReleasePin)
        {
            return state.Hold is SharedCutHold.Effective or SharedCutHold.Unknown
                ? Change(state with { Hold = SharedCutHold.Released }, "exact-authorized-hold-release-assumed") : Refuse("no-original-pin");
        }
        if (action == SharedCutAction.Rotate)
        {
            if (state.Frozen || state.Rotation != 0) { return Refuse("rotation-frozen-or-already-modeled"); }
            return Change(state with { Rotation = 1 }, "kek-signing-rotation-preserves-paths-and-original-aad");
        }
        if (state.Hold is SharedCutHold.Effective or SharedCutHold.Unknown) { return Refuse("effective-or-unknown-pin"); }
        switch (action)
        {
            case SharedCutAction.ReserveProtection when state.Boundary == SharedCutBoundary.BeforeB0:
                return Change(state with { Boundary = SharedCutBoundary.B0ProtectionReserved }, "B0-original-protection-reservation");
            case SharedCutAction.ReserveCustody when state.Boundary == SharedCutBoundary.B0ProtectionReserved:
                return Change(state with { Boundary = SharedCutBoundary.B1CustodyReserved }, "B1-whole-tenant-freeze");
            case SharedCutAction.PrepareSurvivors when state.Frozen:
                if (state.Paths.All(p => p.Selected)) { return new(state, true, "no-survivors-to-copy"); }
                if (state.Paths.Where(p => !p.Selected).All(p => p.Epochs.HasFlag(SharedCutEpoch.Successor))) { return Refuse("survivors-already-prepared"); }
                return Change(state with { Paths = state.Paths.Select(p => p.Selected ? p : p with { Epochs = p.Epochs | SharedCutEpoch.Successor }).ToImmutableArray() }, "survivors-only-independent-successor-path");
            case SharedCutAction.CloseNativeSend when state.Frozen && state.NativeSendUncertain:
                return Change(state with { NativeSendUncertain = false }, "assumed-real-original-io-completion-no-resend");
            case SharedCutAction.CloseHolders when state.Frozen && !state.NativeSendUncertain && state.HolderUncertain:
                return Change(state with { HolderUncertain = false }, "assumed-all-sdk-sidecar-component-transport-holder-closure");
            case SharedCutAction.RetireAlternate when state.Frozen && !state.NativeSendUncertain && !state.HolderUncertain:
                return Retire(SharedCutEpoch.Alternate);
            case SharedCutAction.RetireTainted when state.Frozen && !state.NativeSendUncertain && !state.HolderUncertain:
                return Retire(SharedCutEpoch.Tainted);
            case SharedCutAction.ConfirmPreparation when state.Boundary == SharedCutBoundary.B1CustodyReserved && Prepared(state):
                return Change(state with { Boundary = SharedCutBoundary.B2Prepared }, "B2-complete-copy-and-holder-preparation");
            case SharedCutAction.Arm when state.Boundary == SharedCutBoundary.B2Prepared && Prepared(state):
                return Change(state with { Boundary = SharedCutBoundary.B3Armed }, "B3-current-successor-adopted-targets-fenced");
            case SharedCutAction.Cut when state.Boundary == SharedCutBoundary.B3Armed && Prepared(state):
                return Change(state with { Boundary = SharedCutBoundary.B4Cut, CutCount = state.CutCount + 1,
                    Paths = state.Paths.Select(p => p with { Epochs = p.Epochs & ~SharedCutEpoch.Source }).ToImmutableArray() }, "B4-ASSUMED-one-shared-physical-cut");
            case SharedCutAction.VerifyRestoreBarrier when state.Boundary == SharedCutBoundary.B4Cut && !state.RestoreBarrierVerified && Closure(state):
                return Change(state with { RestoreBarrierVerified = true }, "assumed-independent-copy-survivor-restore-proof");
            case SharedCutAction.Publish when state.Boundary == SharedCutBoundary.B4Cut && state.RestoreBarrierVerified && Closure(state):
                return Change(state with { Boundary = SharedCutBoundary.B5Published, PublishedOriginal = state.Original.SymbolicOriginal }, "B5-publish-exact-symbolic-original");
            default: return Refuse("boundary-or-incomplete-preparation");
        }

        SharedCutStep Retire(SharedCutEpoch epoch)
        {
            if (state.Paths.All(p => (p.Epochs & epoch) == 0)) { return Refuse("epoch-already-retired"); }
            return Change(state with { Paths = state.Paths.Select(p => p with { Epochs = p.Epochs & ~epoch }).ToImmutableArray() }, "assumed-permanent-retirement-" + epoch);
        }
    }

    internal static SharedCutObservation Observe(SharedCutState state)
        => !state.AuthorityAvailable ? new("Unavailable", [])
            : state.Boundary == SharedCutBoundary.B5Published ? new("ModelPublished", state.PublishedOriginal)
            : new("Unknown", []); // The admitted original never becomes NotStarted, even at B0/loss.

    internal static ImmutableArray<string> Violations(SharedCutState state)
    {
        var violations = ImmutableArray.CreateBuilder<string>();
        var selected = state.Paths.Where(p => p.Selected).ToArray();
        int reachable = selected.Count(p => p.Epochs != SharedCutEpoch.None);
        if (reachable != 0 && reachable != selected.Length) { violations.Add("partial-target-cut"); }
        if (state.Paths.Any(p => !p.Selected && p.Epochs == SharedCutEpoch.None)) { violations.Add("survivor-path-lost"); }
        if (!selected.Select(p => p.Identity).SequenceEqual(state.Original.Targets)) { violations.Add("original-vector-changed"); }
        if (state.CutCount > 1) { violations.Add("physical-original-repeated"); }
        if (state.Boundary >= SharedCutBoundary.B3Armed && (state.NativeSendUncertain || state.HolderUncertain
            || state.Paths.Any(p => (p.Epochs & (SharedCutEpoch.Alternate | SharedCutEpoch.Tainted)) != 0))) { violations.Add("armed-with-unresolved-copy-or-holder"); }
        if (state.Boundary >= SharedCutBoundary.B3Armed && state.Hold is SharedCutHold.Effective or SharedCutHold.Unknown) { violations.Add("effect-conflicts-with-prior-pin"); }
        if (state.Boundary >= SharedCutBoundary.B4Cut && reachable != 0) { violations.Add("cut-left-selected-path"); }
        if (!state.PublishedOriginal.IsEmpty && (state.Boundary != SharedCutBoundary.B5Published || !state.RestoreBarrierVerified || !Closure(state)))
        { violations.Add("false-completion"); }
        if (state.Boundary == SharedCutBoundary.B5Published && !state.PublishedOriginal.SequenceEqual(state.Original.SymbolicOriginal)) { violations.Add("terminal-original-bytes-changed"); }
        return violations.ToImmutable();
    }

    internal static string Fingerprint(SharedCutState state) => string.Join('|', (int)state.Boundary, state.Head, state.ApplicationHead,
        state.AuthorityAvailable, (int)state.Hold, state.NativeSendUncertain, state.HolderUncertain, state.SidecarLost, state.RestoreBarrierVerified,
        state.Rotation, state.CutCount, state.NewSurvivorAdmitted, state.PublishedOriginal.IsEmpty,
        string.Join(',', state.Paths.Select(p => (int)p.Epochs)));

    private static bool Prepared(SharedCutState state) => !state.NativeSendUncertain && !state.HolderUncertain
        && state.Paths.All(p => p.Selected ? p.Epochs == SharedCutEpoch.Source
            : p.Epochs is SharedCutEpoch.Successor or (SharedCutEpoch.Source | SharedCutEpoch.Successor));
    private static bool Closure(SharedCutState state) => state.CutCount == 1 && !state.NativeSendUncertain && !state.HolderUncertain
        && state.Paths.All(p => p.Selected ? p.Epochs == SharedCutEpoch.None : p.Epochs == SharedCutEpoch.Successor);
}
