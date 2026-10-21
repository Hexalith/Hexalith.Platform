using System.Text;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Custody.Component;

/// <summary>Runs the offline B0–B5 model as a sequencing gate. Publication here is not physical proof and writes nothing.</summary>
internal static class CustodySharedCutGate
{
    internal const string Published = "published";
    internal const string Blocked = "blocked-by-pin";
    internal const string Refused = "refused";

    private static readonly SharedCutAction[] Schedule =
    [
        SharedCutAction.CommitNativeSend, SharedCutAction.DispatchGuard, SharedCutAction.ReserveProtection,
        SharedCutAction.ReserveCustody, SharedCutAction.PrepareSurvivors, SharedCutAction.CompleteNativeResult,
        SharedCutAction.CloseNativeIntent, SharedCutAction.CloseHolders, SharedCutAction.RetireAlternate,
        SharedCutAction.RetireTainted, SharedCutAction.ConfirmPreparation, SharedCutAction.Arm, SharedCutAction.Cut,
        SharedCutAction.VerifyRestoreBarrier, SharedCutAction.Publish,
    ];

    internal static string Run(CustodyOriginalManifest manifest, string requestDigest, IReadOnlyList<CustodyKeyObjectIdentity> survivors, IReadOnlyList<CustodyWitnessHold> pins)
    {
        try
        {
            var targets = manifest.Targets.Select(i => new ProtectionTarget(i.TenantId, i.ObjectId, i.KeyAlias)).ToArray();
            string digest = DeletionBatchCapabilityIdentity.TargetManifestDigest(targets);
            if (!string.Equals(digest, manifest.ManifestDigest, StringComparison.Ordinal)) { return Refused; }
            var original = new SharedCutOriginal(manifest.ReservationId, manifest.BatchId, manifest.ReservationId, requestDigest, digest, manifest.Targets,
                Encoding.UTF8.GetBytes("disposable-cut:" + digest));
            SharedCutState state = CustodySharedCutModel.Create(original, survivors);
            foreach (var pin in pins)
            {
                var step = CustodySharedCutModel.Apply(state, SharedCutAction.EffectivePin, state.Head, identity: pin.Identity, holdId: pin.HoldId);
                if (!step.Accepted) { return Refused; }
                state = step.State;
            }
            foreach (var action in Schedule)
            {
                if (action == SharedCutAction.PrepareSurvivors && survivors.Count == 0)
                {
                    var skipped = CustodySharedCutModel.Apply(state, action, state.Head);
                    if (!skipped.Accepted) { continue; }
                }
                var step = CustodySharedCutModel.Apply(state, action, state.Head);
                if (!step.Accepted)
                {
                    return step.Reason == "effective-or-unknown-selected-hold" ? Blocked : Refused;
                }
                state = step.State;
            }
            return state.Boundary == SharedCutBoundary.B5Published && CustodySharedCutModel.Violations(state).IsEmpty
                && CustodySharedCutModel.Observe(state).Status == "ModelPublished" ? Published : Refused;
        }
        catch (ArgumentException) { return Refused; }
    }
}
