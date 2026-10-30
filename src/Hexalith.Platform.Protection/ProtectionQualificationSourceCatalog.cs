using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Protection;

/// <summary>Closed source-owned native programs for every action in all 41 owner cases. Installed arguments supply only exact native input values; they cannot choose operations, ordering, race branches or observations.</summary>
public static class ProtectionQualificationSourceCatalog
{
    private static readonly string[] Issue = ["Guard:AuthorizeAdmissionFence", "Guard:CommitAdmissionFence", "GuardOwnersEffective", "Guard:AuthorizeContentBinding", "Guard:CommitContentBinding", "Guard:AuthorizeDestructionStart", "Guard:CommitDestructionStart",
        "Sign", "Guard:RecordBatchIssued", "Guard:AuthorizeDispatch", "Guard:CommitDispatch", "Register"];

    /// <summary>Lists the complete closed native input inventory, including specific guard operations, for independently installed source values.</summary>
    public static IReadOnlyDictionary<string, string> RequiredArguments()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in Programs())
        {
            int index = 0;
            foreach (string token in entry.Value.Before.Concat([entry.Value.Effect]).Concat(entry.Value.Race is { } race ? [race] : []).Concat(entry.Value.After ?? []))
            { result.Add(entry.Key + "/" + (++index).ToString(System.Globalization.CultureInfo.InvariantCulture), token); }
        }
        return result;
    }

    /// <summary>Compiles the entire unchanged plan before any effect. Incomplete/extra arguments or substituted guard opcodes refuse the whole installation.</summary>
    public static Dictionary<string, Dictionary<string, ProtectionQualificationActionRecipe>> Compile(IReadOnlyDictionary<string, JsonObject> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var required = RequiredArguments();
        if (!required.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(arguments.Keys)) { throw new InvalidOperationException("Complete exact 41-case native argument inventory required."); }
        var result = new Dictionary<string, Dictionary<string, ProtectionQualificationActionRecipe>>(StringComparer.Ordinal);
        foreach (var entry in Programs())
        {
            string[] name = entry.Key.Split('/'); int ordinal = 0;
            ProtectionQualificationNativeCall Resolve(string token)
            {
                string id = entry.Key + "/" + (++ordinal).ToString(System.Globalization.CultureInfo.InvariantCulture);
                var payload = (JsonObject)arguments[id].DeepClone(); string[] parts = token.Split(':');
                if (parts[0] == "ContinueHoldAfterDispatch")
                { payload = (JsonObject)arguments[entry.Key + "/" + (ordinal - 1).ToString(System.Globalization.CultureInfo.InvariantCulture)].DeepClone(); }
                var kind = parts[0] == "Guard" ? ProtectionQualificationNativeCallKind.Guard : Enum.Parse<ProtectionQualificationNativeCallKind>(parts[0]);
                if (kind == ProtectionQualificationNativeCallKind.SealEvent)
                {
                    if (payload.ContainsKey("ChangePayload")) { throw new InvalidOperationException("The source owns negative payload selection."); }
                    if (name[1] is "RejectChangedPayload" or "RejectStaleWriter") { payload["ChangePayload"] = true; }
                }
                if (kind == ProtectionQualificationNativeCallKind.Guard)
                {
                    var expected = Enum.Parse<GovernanceGuardOperation>(parts[1]);
                    if (payload["transition"] is not JsonObject transition || transition["Operation"]?.GetValue<int>() != (int)expected)
                    { throw new InvalidOperationException("Source-owned guard operation cannot be substituted: " + id); }
                    if (payload.ContainsKey("UseCurrentGuardRevision")) { throw new InvalidOperationException("The source owns native comparison selection."); }
                    if ((name[1] == "ActivateReattestedDeletionBatch" || name[0] == "P36" && name[1] == "LookupOriginalActivation") && expected == GovernanceGuardOperation.RecordProtectionOutcome && transition["Batch"] is JsonObject batch)
                    {
                        string activation = entry.Key + "/1";
                        JsonObject Original(string pointer) => new() { ["$original"] = new JsonObject { ["id"] = activation, ["pointer"] = pointer } };
                        batch["ProtectionOutcome"] = "Activated"; batch["ProtectionReceiptId"] = Original("/ReceiptId");
                        batch["BlockSetRevision"] = Original("/KeyBlockSetRevision"); batch["TargetReceiptIds"] = new JsonArray();
                    }
                    payload["UseCurrentGuardRevision"] = expected is not (GovernanceGuardOperation.RecordBatchIssued or GovernanceGuardOperation.ReplaceAttestation)
                        && !(name[1] == "DelayAndRestoreDispatch" && expected == GovernanceGuardOperation.CommitDispatch)
                        && !(name[1] == "RaceHoldDispatch" && expected is GovernanceGuardOperation.CommitDispatch or GovernanceGuardOperation.RegisterHold);
                }
                return new(id, kind, payload);
            }
            var before = entry.Value.Before.Select(Resolve).ToArray(); var effect = Resolve(entry.Value.Effect);
            var race = entry.Value.Race is { } competing ? Resolve(competing) : null; var after = (entry.Value.After ?? []).Select(Resolve).ToArray();
            if (!result.TryGetValue(name[0], out var actions)) { actions = new(StringComparer.Ordinal); result.Add(name[0], actions); }
            actions.Add(name[1], new(before, effect, race, after));
        }
        ProtectionQualificationActionCatalog.Validate(result); return result;
    }

    /// <summary>Completes every erased scenario with actual per-copy trusted replay, including cases without an explicit ReplayAll action.</summary>
    internal static ProtectionQualificationActionRecipe WithRequiredReplays(string caseId, string action, ProtectionQualificationActionRecipe recipe,
        ProtectionQualificationObservationRecipe observation)
    {
        using Stream stream = typeof(ProtectionQualificationSourceCatalog).Assembly.GetManifestResourceStream("ExtProtectionOwnerPlan")!;
        using var plan = JsonDocument.Parse(stream);
        var scenario = plan.RootElement.GetProperty("cases").EnumerateArray().Single(value => value.GetProperty("id").GetString() == caseId);
        if (scenario.GetProperty("steps").EnumerateArray().Last().GetString() != action
            || !scenario.GetProperty("checks").EnumerateArray().Any(value => value.GetString() == "erased")) { return recipe; }
        var calls = observation.Sources.Where(value => value.Value.CopyKind is not null).OrderBy(value => value.Key, StringComparer.Ordinal)
            .Select(value => new ProtectionQualificationNativeCall(caseId + "/" + action + "/replay-copy:" + ErasureRootCustody.Hash(value.Value),
                ProtectionQualificationNativeCallKind.ReplayCopy, (JsonObject)JsonSerializer.SerializeToNode(value.Value)!)).ToArray();
        if (calls.Length == 0) { throw new InvalidOperationException("Complete native sealed-copy inventory required for erased replay."); }
        return recipe with { After = recipe.After.Concat(calls).ToArray() };
    }

    private static Dictionary<string, ProtectionQualificationSourceProgram> Programs()
    {
        using Stream stream = typeof(ProtectionQualificationSourceCatalog).Assembly.GetManifestResourceStream("ExtProtectionOwnerPlan")!;
        using var plan = JsonDocument.Parse(stream); var result = new Dictionary<string, ProtectionQualificationSourceProgram>(StringComparer.Ordinal);
        var ids = plan.RootElement.GetProperty("cases").EnumerateArray().Select(value => value.GetProperty("id").GetString()!).ToArray();
        if (!ids.SequenceEqual(Enumerable.Range(1, 41).Select(index => "P" + index.ToString("D2", System.Globalization.CultureInfo.InvariantCulture))))
        { throw new InvalidOperationException("The closed 41-case source inventory differs from the owner plan."); }
        foreach (var ownerCase in plan.RootElement.GetProperty("cases").EnumerateArray())
        {
            string id = ownerCase.GetProperty("id").GetString()!;
            string[] actions = ownerCase.GetProperty("steps").EnumerateArray().Select(step => step.GetString()!).ToArray();
            bool prepared = false;
            foreach (string action in actions)
            {
                var program = Action(id, action);
                bool needsOwner = program.Before.Concat([program.Effect]).Concat(program.Race is { } race ? [race] : []).Concat(program.After ?? [])
                    .Any(token => token is "Reserve" or "RejectSubstitutions" or "Block" or "Compromise" or "Activate" || token.StartsWith("Guard:", StringComparison.Ordinal))
                    || action is "InjectPartialProviderFailure" or "DisableGuardLookup" or "RestoreStaleWriterAndOwnerState";
                if (needsOwner && !prepared)
                {
                    // Source-only signing-staleness deliberately owns its initial issuance sequence.
                    string[] preparation = id switch { "P13" => new[] { "WrapFixtureRoot", "Guard:AppendWrite", "Guard:AppendWrite" }.Concat(Issue).ToArray(), "P14" or "P15" => new[] { "Guard:AppendWrite" }.Concat(Issue).ToArray(), "P39" => Issue[..7], "P20" or "P21" => Issue[..9], "P26" => Issue[..11], _ => Issue };
                    program = program with { Before = preparation.Concat(program.Before).ToArray() }; prepared = true;
                }
                if (action == "RejectManifestDestroy" && id is "P20" or "P26")
                { program = program with { Before = (id == "P20" ? new[] { "Guard:AuthorizeDispatch", "Guard:CommitDispatch", "RegisterIfDispatched" } : new[] { "Register" }).Concat(program.Before).ToArray() };
                  if (id == "P20") { program = program with { Effect = "ReserveIfDispatched" }; } }
                result.Add(id + "/" + action, program);
            }
        }
        return result;
    }

    private static ProtectionQualificationSourceProgram Action(string id, string action)
    {
        static ProtectionQualificationSourceProgram Plain(string kind, params string[] before) => new(before, kind);
        static ProtectionQualificationSourceProgram Replacement(string[] before, string[] after) => new(before, "Compromise", null, after);
        return action switch
        {
            "SealDirectoryOutbox" => Plain("SealEvent", "SealEvent"),
            "SealTargetEvent" => Plain("SealEvent", "SealEvent"),
            "SealAllTargets" => Plain("SealEvent", "SealEvent", "SealEvent", "SealEvent"),
            "UnsealDirectoryOutbox" or "UnsealTargetEvent" => Plain("UnsealEvent", "UnsealEvent"),
            "SealSnapshotAndCache" => Plain("SealSnapshot"),
            "PinDek" => Plain("Lifecycle", "Guard:RegisterHold", "Lifecycle"),
            "UnpinDek" => new(["Lifecycle"], "Lifecycle", null, ["Guard:ReleaseHold"]),
            "AuthorizeUnpinDek" => Plain("AuthorizeLifecycle", "AuthorizeLifecycle"),
            "RejectPerKeyDestroy" => Plain("Lifecycle", "Lifecycle"),
            "DestroyDekManifest" when id == "P13" => new(["Reserve", "Guard:RecordProtectionOutcome", "Reserve", "Lookup", "LookupProvider"], "Reserve", null,
                ["Guard:RecordProtectionOutcome", "Reserve", "Lookup", "LookupProvider"]),
            "DestroyDekManifest" => new([], "Reserve", null, ["Guard:RecordProtectionOutcome", "Reserve", "Lookup", "LookupProvider"]),
            "RejectManifestDestroy" or "RetryExactManifest" => Plain("Reserve"),
            "RejectChangedManifest" or "RejectCrossTenantManifest" or "RejectCanonicalHeaderPayloadSubstitutions" or "RejectTenantKeyAttestationDispatchRevisionSubstitutions" => Plain("RejectSubstitutions"),
            "RejectCrossTenantAlias" => Plain("RejectForeignRoot"),
            "RejectStaleRevokedDispatch" => Plain("Reserve", "Compromise"),
            "RejectStaleWriter" => Plain("SealEvent"),
            "DestroyWithLostAcknowledgement" => Plain("Reserve", "Fault"),
            "RejectChangedPayload" => Plain("SealEvent"),
            "ReplayAll" when id is "P10" => new(["ReplayEvent", "ReplayEvent", "ReplayEvent"], "ReplayEvent", null, ["ReplaySnapshot", "ReplaySnapshot"]),
            "ReplayAll" when id is "P11" => new(["ReplayEvent", "ReplayEvent", "ReplayEvent"], "ReplayEvent", null, ["ReplaySnapshot", "ReplaySnapshot", "ReplaySnapshot"]),
            "ReplayAll" => Plain("ReplayEvent", "ReplayEvent", "ReplayEvent", "ReplayEvent"),
            "CreateBackup" => Plain("Backup", "SealSnapshot"),
            "RestoreBackup" => Plain("Restore"),
            "RestoreStaleWriterAndOwnerState" => new(["Backup", "Backup", "SealEvent", "Compromise"], "Restore", null, ["Restore"]),
            "InjectPartialProviderFailure" or "DisableGuardLookup" => Plain("Fault"),
            "IssueContainmentSingletons" => new(["Guard:RecordViolation", "Guard:AuthorizeContainment"], "Guard:CommitContainment", null,
                ["Sign", "Guard:RecordBatchIssued", "Guard:AuthorizeDispatch", "Guard:CommitDispatch", "Register", "Guard:RecordViolation", "Guard:AuthorizeContainment", "Guard:CommitContainment",
                 "Sign", "Guard:RecordBatchIssued", "Guard:AuthorizeDispatch", "Guard:CommitDispatch", "Register"]),
            "LinkSameTargetResource" => Plain("Guard:AuthorizeContainment", "Guard:RecordViolation"),
            "DelayAndRestoreDispatch" => Plain("Guard:CommitDispatch", "Backup", "Restore"),
            "HoldBeforeDispatch" => Plain("Guard:RegisterHold"),
            "HoldAfterDispatch" => Plain("Guard:RegisterHold"),
            "RaceHoldDispatch" => new(["Guard:AuthorizeDispatch"], "Guard:CommitDispatch", "Guard:RegisterHold", ["ContinueHoldAfterDispatch", "RegisterIfDispatched", "ReserveIfDispatched"]),
            "BlockAdmissionBeforeReserve" => Plain("Block"),
            "RaceAdmissionReserve" => new([], "Block", "Reserve"),
            "ReserveBeforeAdmissionBlock" => new(["Reserve", "LookupProvider"], "Block", null, ["Reserve", "Guard:RecordProtectionOutcome", "Lookup", "LookupProvider"]),
            "CompromiseBeforeRegistration" => Plain("Compromise"),
            "RaceCompromiseReserve" => new([], "Compromise", "Reserve"),
            "CompromiseAfterReserve" => new(["Reserve", "LookupProvider"], "Compromise", null, ["Reserve", "Guard:RecordProtectionOutcome", "Lookup", "LookupProvider"]),
            "CompromiseAndReattest" => Replacement([], ["Sign", "Guard:ReplaceAttestation", "Guard:AuthorizeDispatch", "Guard:CommitDispatch"]),
            "CompromiseReplacementBeforeReplace" => Replacement(["Compromise", "Sign"], ["Guard:ReplaceAttestation"]),
            "CompromiseReplacementBeforeDispatch" => Replacement(["Compromise", "Sign", "Guard:ReplaceAttestation"], ["Guard:AuthorizeDispatch", "Guard:CommitDispatch"]),
            "CompromiseReplacementBeforeActivation" => Replacement(["Compromise", "Sign", "Guard:ReplaceAttestation", "Guard:AuthorizeDispatch", "Guard:CommitDispatch"], []),
            "CompromiseReplacementAfterActivation" => Plain("Compromise", "Compromise", "Sign", "Guard:ReplaceAttestation", "Guard:AuthorizeDispatch", "Guard:CommitDispatch", "Activate"),
            "RejectActivation" when id is "P30" or "P31" => new([], "ReconcileBlockedReplacement", null, ["Guard:RecordProtectionOutcome"]),
            "ActivateReattestedDeletionBatch" => new([], "Activate", null, ["Guard:RecordProtectionOutcome"]),
            "RejectActivation" or "RejectReattestation" => Plain("Activate"),
            "RaceReplacementActivation" => new(["Compromise", "Sign", "Guard:ReplaceAttestation", "Guard:AuthorizeDispatch", "Guard:CommitDispatch"], "Activate", "Compromise"),
            "LoseBlockDeliveryAndMirror" => Plain("Block", "Fault"),
            "MakeBlockOutcomeUnknown" => Plain("Block", "Fault"),
            "LoseActivationResult" => Plain("Activate", "Compromise", "Sign", "Guard:ReplaceAttestation", "Guard:AuthorizeDispatch", "Guard:CommitDispatch", "Fault"),
            "LookupOriginalDestruction" when id == "P06" => new(["Lookup", "LookupProvider", "Reserve", "Guard:RecordProtectionOutcome", "LookupProvider"], "Lookup"),
            "LookupOriginalDestruction" => Plain("Lookup"),
            "LookupAlreadyDestroyed" => new(Issue, "Lookup"),
            "LookupOriginalBlock" => Plain("LookupBlock"),
            "LookupOriginalActivation" => new([], "LookupActivation", null, ["Guard:RecordProtectionOutcome"]),
            "TwoStaleSigningAttempts" => new(["Sign", "Guard:RegisterHold", "Guard:RecordBatchIssued", "ObsoleteUnissued", "Sign", "Guard:ReleaseHold", "Guard:RecordBatchIssued", "ObsoleteUnissued"],
                "Sign", null, ["Guard:RecordBatchIssued", "Guard:AuthorizeDispatch", "Guard:CommitDispatch", "Register"]),
            _ => throw new InvalidOperationException("Missing source-owned owner action: " + action),
        };
    }
}
