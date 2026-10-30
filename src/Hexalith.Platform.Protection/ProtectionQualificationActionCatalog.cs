using System.Text.Json;

namespace Hexalith.Platform.Protection;

/// <summary>Source-owned closed mapping of every action in the unchanged 41-case owner plan to actual native driver primitives.</summary>
public static class ProtectionQualificationActionCatalog
{
    /// <summary>Gets every implemented action and its native effect kind. Preparatory/race/follow-up requests remain typed real native inputs.</summary>
    public static IReadOnlyDictionary<string, ProtectionQualificationNativeCallKind> Actions { get; } = new Dictionary<string, ProtectionQualificationNativeCallKind>(StringComparer.Ordinal)
    {
        ["SealDirectoryOutbox"] = ProtectionQualificationNativeCallKind.SealEvent,
        ["SealTargetEvent"] = ProtectionQualificationNativeCallKind.SealEvent,
        ["SealAllTargets"] = ProtectionQualificationNativeCallKind.SealEvent,
        ["RejectChangedPayload"] = ProtectionQualificationNativeCallKind.SealEvent,
        ["UnsealDirectoryOutbox"] = ProtectionQualificationNativeCallKind.UnsealEvent,
        ["UnsealTargetEvent"] = ProtectionQualificationNativeCallKind.UnsealEvent,
        ["ReplayAll"] = ProtectionQualificationNativeCallKind.ReplayEvent,
        ["SealSnapshotAndCache"] = ProtectionQualificationNativeCallKind.SealSnapshot,
        ["PinDek"] = ProtectionQualificationNativeCallKind.Lifecycle,
        ["UnpinDek"] = ProtectionQualificationNativeCallKind.Lifecycle,
        ["AuthorizeUnpinDek"] = ProtectionQualificationNativeCallKind.AuthorizeLifecycle,
        ["RejectPerKeyDestroy"] = ProtectionQualificationNativeCallKind.Lifecycle,
        ["DestroyDekManifest"] = ProtectionQualificationNativeCallKind.Reserve,
        ["DestroyWithLostAcknowledgement"] = ProtectionQualificationNativeCallKind.Reserve,
        ["RetryExactManifest"] = ProtectionQualificationNativeCallKind.Reserve,
        ["RejectManifestDestroy"] = ProtectionQualificationNativeCallKind.Reserve,
        ["RejectChangedManifest"] = ProtectionQualificationNativeCallKind.RejectSubstitutions,
        ["RejectCrossTenantAlias"] = ProtectionQualificationNativeCallKind.RejectForeignRoot,
        ["RejectCrossTenantManifest"] = ProtectionQualificationNativeCallKind.RejectSubstitutions,
        ["RejectCanonicalHeaderPayloadSubstitutions"] = ProtectionQualificationNativeCallKind.RejectSubstitutions,
        ["RejectStaleRevokedDispatch"] = ProtectionQualificationNativeCallKind.Reserve,
        ["RejectStaleWriter"] = ProtectionQualificationNativeCallKind.SealEvent,
        ["RejectTenantKeyAttestationDispatchRevisionSubstitutions"] = ProtectionQualificationNativeCallKind.RejectSubstitutions,
        ["IssueContainmentSingletons"] = ProtectionQualificationNativeCallKind.Guard,
        ["LinkSameTargetResource"] = ProtectionQualificationNativeCallKind.Guard,
        ["DelayAndRestoreDispatch"] = ProtectionQualificationNativeCallKind.Guard,
        ["HoldBeforeDispatch"] = ProtectionQualificationNativeCallKind.Guard,
        ["HoldAfterDispatch"] = ProtectionQualificationNativeCallKind.Guard,
        ["RaceHoldDispatch"] = ProtectionQualificationNativeCallKind.Guard,
        ["BlockAdmissionBeforeReserve"] = ProtectionQualificationNativeCallKind.Block,
        ["RaceAdmissionReserve"] = ProtectionQualificationNativeCallKind.Block,
        ["ReserveBeforeAdmissionBlock"] = ProtectionQualificationNativeCallKind.Block,
        ["LoseBlockDeliveryAndMirror"] = ProtectionQualificationNativeCallKind.Block,
        ["MakeBlockOutcomeUnknown"] = ProtectionQualificationNativeCallKind.Block,
        ["CompromiseBeforeRegistration"] = ProtectionQualificationNativeCallKind.Compromise,
        ["RaceCompromiseReserve"] = ProtectionQualificationNativeCallKind.Compromise,
        ["CompromiseAfterReserve"] = ProtectionQualificationNativeCallKind.Compromise,
        ["CompromiseAndReattest"] = ProtectionQualificationNativeCallKind.Compromise,
        ["CompromiseReplacementBeforeReplace"] = ProtectionQualificationNativeCallKind.Compromise,
        ["CompromiseReplacementBeforeDispatch"] = ProtectionQualificationNativeCallKind.Compromise,
        ["CompromiseReplacementBeforeActivation"] = ProtectionQualificationNativeCallKind.Compromise,
        ["CompromiseReplacementAfterActivation"] = ProtectionQualificationNativeCallKind.Compromise,
        ["ActivateReattestedDeletionBatch"] = ProtectionQualificationNativeCallKind.Activate,
        ["RaceReplacementActivation"] = ProtectionQualificationNativeCallKind.Activate,
        ["RejectActivation"] = ProtectionQualificationNativeCallKind.Activate,
        ["RejectReattestation"] = ProtectionQualificationNativeCallKind.Activate,
        ["LoseActivationResult"] = ProtectionQualificationNativeCallKind.Activate,
        ["LookupOriginalDestruction"] = ProtectionQualificationNativeCallKind.Lookup,
        ["LookupAlreadyDestroyed"] = ProtectionQualificationNativeCallKind.Lookup,
        ["LookupOriginalBlock"] = ProtectionQualificationNativeCallKind.LookupBlock,
        ["LookupOriginalActivation"] = ProtectionQualificationNativeCallKind.LookupActivation,
        ["TwoStaleSigningAttempts"] = ProtectionQualificationNativeCallKind.Sign,
        ["CreateBackup"] = ProtectionQualificationNativeCallKind.Backup,
        ["RestoreBackup"] = ProtectionQualificationNativeCallKind.Restore,
        ["RestoreStaleWriterAndOwnerState"] = ProtectionQualificationNativeCallKind.Restore,
        ["InjectPartialProviderFailure"] = ProtectionQualificationNativeCallKind.Fault,
        ["DisableGuardLookup"] = ProtectionQualificationNativeCallKind.Fault,
    };
    /// <summary>Validates the entire 41-case installed native recipe inventory before any fixture effect; missing or mislabelled operations refuse.</summary>
    public static void Validate(IReadOnlyDictionary<string, Dictionary<string, ProtectionQualificationActionRecipe>> recipes)
    {
        using Stream stream = typeof(ProtectionQualificationActionCatalog).Assembly.GetManifestResourceStream("ExtProtectionOwnerPlan")!;
        using var plan = JsonDocument.Parse(stream);
        var cases = plan.RootElement.GetProperty("cases").EnumerateArray().ToArray();
        if (cases.Length != 41 || recipes.Count != cases.Length) { throw new InvalidOperationException("DependencyNotAvailable: complete 41-case native recipes required."); }
        var actionNames = cases.SelectMany(value => value.GetProperty("steps").EnumerateArray().Select(step => step.GetString()!)).ToHashSet(StringComparer.Ordinal);
        if (!actionNames.SetEquals(Actions.Keys)) { throw new InvalidOperationException("Native action catalog differs from the owner plan."); }
        foreach (var ownerCase in cases)
        {
            string id = ownerCase.GetProperty("id").GetString()!;
            if (!recipes.TryGetValue(id, out var actions) || !actions.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(ownerCase.GetProperty("steps").EnumerateArray().Select(value => value.GetString()!)))
            { throw new InvalidOperationException("DependencyNotAvailable: incomplete native case recipe."); }
            foreach (var entry in actions)
            {
                var recipe = entry.Value;
                var expected = entry.Key == "RejectActivation" && id is "P30" or "P31" ? ProtectionQualificationNativeCallKind.ReconcileBlockedReplacement : Actions[entry.Key];
                if (entry.Key == "RejectManifestDestroy" && id == "P20") { expected = ProtectionQualificationNativeCallKind.ReserveIfDispatched; }
                if (recipe.Effect.Kind != expected || recipe.Before is null || recipe.After is null
                    || recipe.Before.Concat([recipe.Effect]).Concat(recipe.Race is { } race ? [race] : []).Concat(recipe.After).Any(call => string.IsNullOrWhiteSpace(call.Id) || call.Payload is null || !Enum.IsDefined(call.Kind)))
                { throw new InvalidOperationException("Actual typed native action recipe required."); }
            }
        }
    }
}
