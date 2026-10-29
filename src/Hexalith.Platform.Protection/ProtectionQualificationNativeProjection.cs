using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;

namespace Hexalith.Platform.Protection;

/// <summary>Source-owned owner evidence from freshly read native cells, current ERC originals and exact matching executed native calls. Configuration supplies locations only.</summary>
internal static class ProtectionQualificationNativeProjection
{
    internal static JsonObject Project(string caseId, ErasureRootHead head, IReadOnlyDictionary<string, JsonNode> sources,
        IReadOnlyDictionary<string, ProtectionQualificationNativeCell> cells, IReadOnlyList<ProtectionTarget> targets)
    {
        using var planStream = typeof(ProtectionQualificationSourceCatalog).Assembly.GetManifestResourceStream("ExtProtectionOwnerPlan")!;
        using var plan = JsonDocument.Parse(planStream);
        var ownerCase = plan.RootElement.GetProperty("cases").EnumerateArray().Single(value => value.GetProperty("id").GetString() == caseId);
        var checks = ownerCase.GetProperty("checks").EnumerateArray().Select(value => value.GetString()!).ToHashSet(StringComparer.Ordinal);
        var targetIds = targets.Select(ErasureRootCustody.TargetId).ToHashSet(StringComparer.Ordinal);
        var records = new JsonArray(); var replays = new JsonArray();
        foreach (var cell in cells.Where(entry => entry.Value.CopyKind is not null))
        {
            string kind = cell.Value.CopyKind!;
            if (kind is not ("source-outbox" or "target-event" or "snapshot" or "cache" or "backup" or "restored"))
            { throw new InvalidOperationException("Closed native copy purpose required."); }
            var value = sources[cell.Key] as JsonObject ?? throw new InvalidOperationException("Actual sealed copy required.");
            if (kind is "source-outbox" or "target-event")
            {
                var persisted = value.Deserialize<ProtectionQualificationPersistedEvent>() ?? throw new InvalidOperationException("Actual sealed source required.");
                var target = persisted.Binding.Identity.Target;
                if (!targetIds.Contains(ErasureRootCustody.TargetId(target)) || persisted.Protected.Metadata is not { Scheme: "hexalith-pdenc-v2", State: PayloadProtectionState.Protected }
                    || !head.SourceOriginals.Values.Any(original => original.Binding == persisted.Binding)) { throw new InvalidOperationException("Native copy binding unavailable."); }
                AddRecord(target, persisted.Protected.PayloadBytes, kind, cell.Key);
            }
            else
            {
                var snapshot = value.Deserialize<SnapshotProtectionResult>() ?? throw new InvalidOperationException("Actual sealed snapshot copy required.");
                var state = JsonSerializer.SerializeToNode(snapshot.State) as JsonObject ?? throw new InvalidOperationException("Actual sealed snapshot carrier required.");
                if (snapshot.Metadata is not { Scheme: "hexalith-pdenc-v2", State: PayloadProtectionState.Protected }) { throw new InvalidOperationException("Sealed snapshot metadata required."); }
                if (state["Format"]?.GetValue<string>() == "json+pdenc-v2-entries")
                {
                    var carrier = state.Deserialize<ProtectedSnapshotEntriesV2>()!;
                    foreach (var target in carrier.Entries.Select(entry => entry.Target).Distinct())
                    { if (!targetIds.Contains(ErasureRootCustody.TargetId(target))) { throw new InvalidOperationException("Native copy target outside fixture."); } AddRecord(target, JsonSerializer.SerializeToUtf8Bytes(carrier), kind, cell.Key); }
                }
                else
                {
                    string reference = snapshot.Metadata.KeyAlias ?? throw new InvalidOperationException("Actual V2 snapshot key required.");
                    var originals = head.SourceOriginals.Values.Where(original => original.Binding.KeyReference == reference).ToArray();
                    if (originals.Length != 1 || !targetIds.Contains(ErasureRootCustody.TargetId(originals[0].Binding.Identity.Target))) { throw new InvalidOperationException("Exact snapshot source original required."); }
                    AddRecord(originals[0].Binding.Identity.Target, JsonSerializer.SerializeToUtf8Bytes(state), kind, cell.Key);
                }
            }
            if (sources.TryGetValue(cell.Key + ":replay", out var replay))
            {
                foreach (var item in replay is JsonArray items ? items : new JsonArray(replay.DeepClone()))
                {
                    var projected = (JsonObject)item!.DeepClone();
                    if (projected["sourceIdentity"]?.GetValue<string>() != ProtectionQualificationNativeCell.LocationId(cell.Value)
                        || projected["sourceDigest"]?.GetValue<string>() != ErasureRootCustody.Hash(value))
                    { throw new InvalidOperationException("Replay must bind the exact native copy and persisted ciphertext."); }
                    projected["kind"] = kind;
                    projected["copyId"] = cell.Key + ":" + ErasureRootCustody.TargetId(projected["target"]!.Deserialize<ProtectionTarget>()!);
                    replays.Add(projected);
                }
            }
        }
        var observed = new JsonObject { ["records"] = records, ["replays"] = replays };
        var fixtureCalls = head.QualificationOperations.Values.Where(operation => operation.SafeRequest["caseId"]?.GetValue<string>() == caseId
            && operation.SafeRequest["targets"] is JsonArray selected && ErasureRootCustody.Hash(selected) == ErasureRootCustody.Hash(JsonSerializer.SerializeToNode(targets)))
            .SelectMany(operation => operation.Calls.Values).Where(call => call.Result is not null).ToArray();
        if (checks.Contains("no-effect"))
        {
            var audits = fixtureCalls.Select(call => call.Result).OfType<JsonObject>().Where(value => value["effectBefore"] is not null && value["effectAfter"] is not null).ToArray();
            if (audits.Length == 0) { throw new InvalidOperationException("Actual native rejection effect boundaries required."); }
            observed["effectRevisionBeforeRejectedOperation"] = ErasureRootCustody.Hash(audits.Select(value => value["effectBefore"]!.GetValue<string>()).ToArray());
            observed["effectRevisionAfterRejectedOperation"] = ErasureRootCustody.Hash(audits.Select(value => value["effectAfter"]!.GetValue<string>()).ToArray());
        }
        var persistedGuard = sources.Values.OfType<JsonObject>().SingleOrDefault(value => value.ContainsKey("Deletions") && value.ContainsKey("CompromisedKeyVersions"));
        if (checks.Contains("hold-order")) { ProjectHolds(observed, head, fixtureCalls, persistedGuard ?? throw new InvalidOperationException("Actual current hold source required.")); }
        if (checks.Contains("pin")) { ProjectPins(observed, head, fixtureCalls); }
        if (checks.Overlaps(["copies", "restore"])) { ProjectCopies(observed, fixtureCalls, sources, cells, checks.Contains("erased")); }
        void AddRecord(ProtectionTarget target, byte[] bytes, string kind, string copy)
        {
            var identities = head.SourceOriginals.Values.Where(original => original.Binding.Identity.Target == target).Select(original => original.RootIdentity).Distinct().ToArray();
            if (identities.Length != 1) { throw new InvalidOperationException("Exact shared root source identity required."); }
            records.Add(new JsonObject { ["target"] = JsonSerializer.SerializeToNode(target), ["kind"] = kind, ["bytesBase64"] = Convert.ToBase64String(bytes),
                ["protectedState"] = "Sealed", ["rootReference"] = ErasureRootCustody.Hash(identities[0]), ["copyId"] = copy + ":" + ErasureRootCustody.TargetId(target) });
        }
        if (!head.Anchors.TryGetValue(DeletionConsumptionActor.GetOwnerScope(head.TenantId), out var ownerAnchor))
        { if (checks.Overlaps(["receipts", "blocked", "replacement-blocked", "capability", "signing-attempts"]) || checks.Contains("race") && !checks.Contains("hold-order")) { throw new InvalidOperationException("Actual owner original required."); } return observed; }
        var committed = ownerAnchor.Completed.Values.SingleOrDefault(original => original.TargetRevision == ownerAnchor.Revision && original.TargetDigest == ownerAnchor.Digest)
            ?? (checks.Contains("hold-order") ? null : throw new InvalidOperationException("Current retained native owner required."));
        if (committed is null) { return observed; }
        var ledger = JsonNode.Parse(committed.TargetBytes) as JsonObject ?? throw new InvalidOperationException("Actual owner ledger required.");
        var batches = ((JsonArray)ledger["Batches"]!).OfType<JsonObject>().Where(batch => ((JsonArray)batch["Current"]!["Targets"]!).Any(target =>
            targetIds.Contains(ErasureRootCustody.TargetId(target!.Deserialize<ProtectionTarget>()!)))).ToArray();
        if (batches.Length == 0) { return observed; }
        var primary = batches.Where(batch => batch["Outcome"]!["Status"]!.GetValue<int>() != (int)DeletionConsumptionStatus.AlreadyDestroyedByBatch).ToArray();
        var ids = primary.Select(batch => batch["Current"]!["Capability"]!["BatchId"]!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
        var guard = persistedGuard
            ?? throw new InvalidOperationException("Fresh independently persisted guard source required.");
        var guardBatches = ((JsonArray)guard["Deletions"]!).OfType<JsonObject>().SelectMany(deletion => ((JsonArray)deletion["Batches"]!).OfType<JsonObject>())
            .Where(batch => ids.Contains(batch["BatchId"]!.GetValue<string>())).ToArray();
        observed["consumptionRequests"] = Array(primary.Select(batch => batch["Current"]));
        observed["guardBatches"] = Array(guardBatches);
        observed["guardReceipts"] = guard["Receipts"]!.DeepClone();
        observed["ownerBatches"] = Array(batches.Select(batch => Select(batch, "Original", "Current", "Outcome", "BlockedReplacement")));
        observed["ownerResults"] = Array(primary.Select(batch => batch["Outcome"]));
        observed["ownerOutcome"] = OwnerSummary((JsonObject)primary[0]["Outcome"]!);
        var providers = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var manifest in head.Manifests.Values.Where(value => ids.Contains(value.Request.Capability.BatchId)))
        {
            providers.Add(manifest.Request.Capability.BatchId, new JsonObject { ["TenantId"] = head.TenantId, ["BatchId"] = manifest.Request.Capability.BatchId,
                ["ReservationReceiptId"] = manifest.ReservationReceiptId, ["State"] = (int)DeletionManifestProviderState.Consumed, ["TargetReceipts"] = JsonSerializer.SerializeToNode(manifest.Receipts) });
        }
        observed["providerResults"] = Array(providers.Values);
        var calls = fixtureCalls;
        JsonArray ExactCalls(ProtectionQualificationNativeCallKind kind, IEnumerable<JsonNode?> originals)
        {
            var accepted = originals.Where(value => value is not null).ToDictionary(value => ErasureRootCustody.Hash(value), value => value!, StringComparer.Ordinal);
            return Array(calls.Where(call => call.Call.Kind == kind).Select(call => StripStatus(call.Result!)).Where(value => accepted.ContainsKey(ErasureRootCustody.Hash(value))));
        }
        observed["ownerRetryResults"] = ExactCalls(ProtectionQualificationNativeCallKind.Reserve, primary.Select(batch => batch["Outcome"]));
        observed["ownerLookupResults"] = ExactCalls(ProtectionQualificationNativeCallKind.Lookup, primary.Select(batch => batch["Outcome"]));
        observed["providerLookupResults"] = ExactCalls(ProtectionQualificationNativeCallKind.LookupProvider, providers.Values);
        observed["reservationRecoveries"] = Array(primary.Where(batch => batch["ReservationOutcome"] is JsonObject).Select(batch => (JsonNode)new JsonObject
            { ["reservationReceiptId"] = batch["ReservationOutcome"]!["ReceiptId"]!.DeepClone() }));
        var coverage = batches.SingleOrDefault(batch => batch["Outcome"]!["Status"]!.GetValue<int>() == (int)DeletionConsumptionStatus.AlreadyDestroyedByBatch);
        if (coverage is not null) { observed["coverageRequest"] = coverage["Current"]!.DeepClone(); observed["coverageOutcome"] = coverage["Outcome"]!.DeepClone(); }
        var targetCoverage = new JsonArray(); var coverageOriginals = new JsonArray();
        foreach (var batch in primary)
        {
            var request = batch["Current"]!; var capability = request["Capability"]!;
            foreach (var target in (JsonArray)request["Targets"]!)
            {
                string batchId = capability["BatchId"]!.GetValue<string>();
                var receipt = ((JsonArray)batch["Outcome"]!["TargetReceipts"]!).FirstOrDefault(value => ErasureRootCustody.Hash(value!["Target"]) == ErasureRootCustody.Hash(target));
                targetCoverage.Add(new JsonObject { ["target"] = target!.DeepClone(), ["batchIds"] = new JsonArray(batchId), ["ordinals"] = new JsonArray(capability["BatchOrdinal"]!.DeepClone()),
                    ["singletonManifest"] = ((JsonArray)request["Targets"]!).Count == 1, ["reusesAcceptedSetBatch"] = capability["BatchKind"]!.GetValue<string>() == "AcceptedSet" });
                if (receipt is not null) { coverageOriginals.Add(new JsonObject { ["Target"] = target.DeepClone(), ["BatchId"] = batchId, ["Ordinal"] = capability["BatchOrdinal"]!.DeepClone(),
                    ["ManifestDigest"] = capability["ManifestDigest"]!.DeepClone(), ["ReceiptId"] = receipt["ReceiptId"]!.DeepClone() }); }
            }
        }
        observed["targetCoverage"] = targetCoverage; observed["coverageOriginals"] = coverageOriginals;
        ProjectActivation(observed, ledger, primary[0], guard);
        ProjectHistory(observed, ownerAnchor, ids);
        if (checks.Contains("signing-attempts")) { ProjectSigning(observed, head, sources, ids); }
        if (checks.Contains("exact-results"))
        {
            var terminal = primary.Select(batch => batch["Outcome"]!).Concat(((JsonArray)ledger["Operations"]!).OfType<JsonObject>().Select(operation => operation["Outcome"]!)).ToArray();
            var exact = fixtureCalls.Where(call => call.Call.Kind is ProtectionQualificationNativeCallKind.Reserve or ProtectionQualificationNativeCallKind.Lookup
                or ProtectionQualificationNativeCallKind.Block or ProtectionQualificationNativeCallKind.LookupBlock or ProtectionQualificationNativeCallKind.Activate or ProtectionQualificationNativeCallKind.LookupActivation)
                .Select(call => StripStatus(call.Result!)).Where(result => terminal.Any(original => ErasureRootCustody.Hash(original) == ErasureRootCustody.Hash(result))).ToArray();
            var groups = exact.GroupBy(ErasureRootCustody.Hash).Where(group => group.Count() >= 2).ToArray();
            if (groups.Length != 1) { throw new InvalidOperationException("Unique exact retained native terminal result/lookup pair required."); }
            observed["exactResults"] = Array(groups[0]);
        }
        return observed;
    }

    private static void ProjectHistory(JsonObject observed, ErasureRootAnchor owner, IReadOnlySet<string> batches)
    {
        var previous = new Dictionary<string, DeletionConsumptionStatus>(StringComparer.Ordinal); var transitions = new JsonArray();
        foreach (var original in owner.Completed.Values.OrderBy(value => value.TargetRevision))
        {
            using var ledger = JsonDocument.Parse(original.TargetBytes);
            foreach (var batch in ledger.RootElement.GetProperty("Batches").EnumerateArray())
            {
                string id = batch.GetProperty("Current").GetProperty("Capability").GetProperty("BatchId").GetString()!;
                if (!batches.Contains(id)) { continue; }
                var outcome = batch.GetProperty("Outcome").Deserialize<DeletionConsumptionOutcome>()!;
                if (previous.TryGetValue(id, out var prior) && prior == DeletionConsumptionStatus.Unconsumed
                    && outcome.Status is DeletionConsumptionStatus.ConsumptionReserved or DeletionConsumptionStatus.ConsumptionBlocked)
                { transitions.Add(new JsonObject { ["from"] = prior.ToString(), ["to"] = outcome.Status.ToString(), ["receiptId"] = outcome.ReceiptId, ["ownerRevision"] = outcome.OwnerRevision }); }
                previous[id] = outcome.Status;
            }
        }
        observed["ownerTransitions"] = transitions;
    }

    internal static void ProjectCopies(JsonObject observed, IReadOnlyList<ProtectionQualificationNativeOriginal> calls, IReadOnlyDictionary<string, JsonNode> sources,
        IReadOnlyDictionary<string, ProtectionQualificationNativeCell> cells, bool erased)
    {
        var inventory = new JsonArray(); var states = new JsonArray(); var restoreOriginals = new JsonArray();
        var backups = calls.Where(call => call.Call.Kind == ProtectionQualificationNativeCallKind.Backup).ToArray();
        foreach (var record in ((JsonArray)observed["records"]!).OfType<JsonObject>())
        {
            string id = record["copyId"]!.GetValue<string>(), kind = record["kind"]!.GetValue<string>();
            var replay = ((JsonArray)observed["replays"]!).OfType<JsonObject>().SingleOrDefault(value => value["copyId"]?.GetValue<string>() == id
                && ErasureRootCustody.Hash(value["target"]) == ErasureRootCustody.Hash(record["target"]));
            if (replay is null && erased) { throw new InvalidOperationException("Every native sealed source/cache/copy needs its actual replay original."); }
            string read = replay?["field"]?["State"]?.GetValue<string>() == "Erased" && replay["externalOutcome"]?.GetValue<string>() == "Unreadable" ? "Erased"
                : replay?["externalOutcome"]?.GetValue<string>() == "Readable" ? "Readable" : "Unreadable";
            inventory.Add(id); states.Add(new JsonObject { ["copyId"] = id, ["readOutcome"] = read });
            restoreOriginals.Add(new JsonObject { ["CopyId"] = id, ["ReadOutcome"] = read, ["Kind"] = kind });
        }
        foreach (var call in backups)
        {
            var cell = call.Call.Payload.Deserialize<ProtectionQualificationNativeCell>()!;
            var matches = cells.Where(entry => entry.Value.Component == cell.Component && entry.Value.Key == cell.Key
                && ErasureRootCustody.Hash(entry.Value.Metadata) == ErasureRootCustody.Hash(cell.Metadata)).ToArray();
            if (matches.Length != 1 || !sources.TryGetValue(matches[0].Key + ":backup", out var saved))
            { throw new InvalidOperationException("Exact independently persisted native backup source required."); }
            var actual = JsonSerializer.SerializeToElement(saved);
            if (call.Result?["receiptId"]?.GetValue<string>() != ErasureRootCustody.Hash(new { backup = cell, actual }))
            { throw new InvalidOperationException("Actual backup original fingerprint mismatch."); }
            var restores = calls.Where(value => value.Call.Kind == ProtectionQualificationNativeCallKind.Restore
                && ErasureRootCustody.Hash(value.Call.Payload.Deserialize<ProtectionQualificationNativeCell>()) == ErasureRootCustody.Hash(cell)).ToArray();
            foreach (var restore in restores)
            {
                var savedElement = JsonSerializer.SerializeToElement(saved);
                if (restore.Result?["receiptId"]?.GetValue<string>() != ErasureRootCustody.Hash(new { restore = cell, saved = savedElement }))
                { throw new InvalidOperationException("Actual restore original fingerprint mismatch."); }
            }
            if (erased) { continue; }
            string id = "native-original:" + ErasureRootCustody.Hash(cell);
            inventory.Add(id); states.Add(new JsonObject { ["copyId"] = id, ["readOutcome"] = "Readable" });
            restoreOriginals.Add(new JsonObject { ["CopyId"] = id, ["ReadOutcome"] = "Readable", ["Kind"] = restores.Length == 0 ? "backup" : "restored" });
        }
        if (inventory.Count == 0) { throw new InvalidOperationException("Actual complete native copy inventory required."); }
        observed["copyInventoryBefore"] = inventory; observed["copyStatesAfter"] = states;
        if (backups.Length != 0)
        {
            observed["restoreOriginals"] = restoreOriginals;
        }
    }

    private static void ProjectPins(JsonObject observed, ErasureRootHead head, IReadOnlyList<ProtectionQualificationNativeOriginal> calls)
    {
        string[] actions = ["PinDek", "RejectPerKeyDestroy", "RejectManifestDestroy", "AuthorizeUnpinDek", "UnpinDek"];
        var summaries = new JsonArray(); var originals = new JsonArray();
        foreach (string action in actions)
        {
            var candidates = calls.Where(call => call.Call.Id.Contains("/" + action + "/", StringComparison.Ordinal))
                .Where(call => call.Call.Kind is ProtectionQualificationNativeCallKind.Lifecycle or ProtectionQualificationNativeCallKind.Reserve or ProtectionQualificationNativeCallKind.AuthorizeLifecycle)
                .OrderBy(call => int.Parse(call.Call.Id.Split('/')[2], System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            if (candidates.LastOrDefault() is not { Result: JsonObject result } selected) { throw new InvalidOperationException("Actual ordered pin/unpin fence original required."); }
            string status = result["status"]!.GetValue<string>(); ErasureRootPinRefusal original;
            if (selected.Call.Kind == ProtectionQualificationNativeCallKind.Lifecycle)
            {
                var request = selected.Call.Payload["request"]!.Deserialize<Hexalith.Platform.Custody.CustodyKeyLifecycleRequest>()!;
                if (!head.LifecycleOutcomes.TryGetValue(request.OperationId, out var outcome) || outcome.RequestDigest != ErasureRootCustody.Hash(request)
                    || ErasureRootCustody.Hash(StripStatus(result)) != ErasureRootCustody.Hash(outcome) || outcome.ReceiptId is null)
                { throw new InvalidOperationException("Actual retained lifecycle result required."); }
                original = new(request.OperationId, status, outcome.ReceiptId, new(request.Identity.TenantId, request.Identity.ObjectId, request.Identity.KeyAlias));
            }
            else if (selected.Call.Kind == ProtectionQualificationNativeCallKind.Reserve)
            {
                var values = result["pinRefusals"]!.Deserialize<ErasureRootPinRefusal[]>()!;
                var retained = head.PinRefusals.Values.SingleOrDefault(value => value.RequestDigest == ErasureRootCustody.Hash(selected.Call.Payload.Deserialize<DeletionBatchConsumptionRequest>())
                    && ErasureRootCustody.Hash(value.Refusals) == ErasureRootCustody.Hash(values));
                if (retained is null || values.Length == 0) { throw new InvalidOperationException("Actual native pinned manifest refusal required."); }
                original = values[^1];
            }
            else
            {
                if (result["receiptId"]?.GetValue<string>() is not { Length: > 0 } receipt || result["operationId"]?.GetValue<string>() is not { Length: > 0 } operation)
                { throw new InvalidOperationException("Actual independent lifecycle authorization receipt required."); }
                original = new(operation, status, receipt, result["target"]!.Deserialize<ProtectionTarget>()!);
            }
            summaries.Add(new JsonObject { ["status"] = status, ["target"] = JsonSerializer.SerializeToNode(original.Target) }); originals.Add(JsonSerializer.SerializeToNode(original));
        }
        observed["pinOutcomes"] = summaries; observed["pinOriginals"] = originals;
    }

    private static void ProjectSigning(JsonObject observed, ErasureRootHead head, IReadOnlyDictionary<string, JsonNode> sources, IReadOnlySet<string> batches)
    {
        var outcomes = sources.Values.OfType<JsonObject>().Where(value => value["SigningRequestId"] is not null && value["Payload"] is JsonObject payload
            && batches.Contains(payload["BatchId"]!.GetValue<string>())).OrderBy(value => value["Payload"]!["SigningAttemptOrdinal"]!.GetValue<long>()).ToArray();
        if (outcomes.Length < 3) { throw new InvalidOperationException("Every independently persisted signing actor original required."); }
        var summaries = new JsonArray(); var receipts = new JsonArray(); var recoveries = new JsonArray(); var intents = new JsonArray();
        foreach (var outcome in outcomes)
        {
            var payload = outcome["Payload"]!; var proof = outcome["NoIssueProof"] as JsonObject;
            summaries.Add(new JsonObject { ["DestructionSealId"] = payload["DestructionSealId"]!.DeepClone(), ["BatchId"] = payload["BatchId"]!.DeepClone(),
                ["ManifestDigest"] = payload["ManifestDigest"]!.DeepClone(), ["AttestationOrdinal"] = payload["AttestationOrdinal"]!.DeepClone(),
                ["SigningAttemptOrdinal"] = payload["SigningAttemptOrdinal"]!.DeepClone(), ["status"] = outcome["State"]!.GetValue<int>() == 5 ? "SignedAttestationObsoleteUnissued" : "Signed",
                ["exactNoIssueReceiptId"] = proof?["ProofId"]?.DeepClone() });
            if (proof is null) { continue; }
            string proofId = proof["ProofId"]!.GetValue<string>();
            var attempt = head.GuardAttempts.Values.SingleOrDefault(value => value.Receipt.ReceiptId == proofId)
                ?? throw new InvalidOperationException("Exact independently retained stale issuance intent required.");
            receipts.Add(JsonSerializer.SerializeToNode(attempt.Receipt));
            intents.Add(new JsonObject { ["BytesBase64"] = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new { attempt.Transition, attempt.TargetMutationDigest })) });
            var originalProof = proof.Deserialize<Hexalith.Platform.Custody.DeletionCapabilityNoIssueProof>()!;
            var recovery = head.SigningRecoveries.Values.Where(value => value.NoIssueReceiptId == proofId && value.Payload == originalProof.Payload
                && value.SigningRequestId == originalProof.SigningRequestId && value.DetachedJwsDigest == originalProof.DetachedJwsDigest && value.ObservedAt >= originalProof.ObservedAt)
                .OrderBy(value => value.ObservedAt).FirstOrDefault() ?? throw new InvalidOperationException("Exact independent signing recovery original required.");
            recoveries.Add(JsonSerializer.SerializeToNode(recovery));
        }
        observed["signingOriginals"] = Array(outcomes); observed["signingAttempts"] = summaries; observed["noIssueOriginals"] = receipts;
        observed["signingRecoveries"] = recoveries; observed["noIssueIntents"] = intents;
    }

    internal static void ProjectHolds(JsonObject observed, ErasureRootHead head, IReadOnlyList<ProtectionQualificationNativeOriginal> calls, JsonObject guard)
    {
        var intents = calls.Where(call => call.Call.Kind is ProtectionQualificationNativeCallKind.Guard or ProtectionQualificationNativeCallKind.ContinueHoldAfterDispatch).Select(call =>
        {
            var transition = call.Call.Payload["transition"]!.Deserialize<GovernanceGuardTransition>()!;
            var targets = call.Call.Payload["targets"]!.Deserialize<GuardedStateMutation[]>()!;
            return ErasureRootCustody.Hash(new { Transition = transition, TargetMutationDigest = ErasureRootCustody.Hash(targets) });
        }).ToHashSet(StringComparer.Ordinal);
        var originals = head.GuardAttempts.Where(entry => intents.Contains(entry.Key)).Select(entry => entry.Value).ToArray();
        var holds = originals.Where(value => value.Transition.Operation == GovernanceGuardOperation.RegisterHold && value.Receipt.Status == "Committed").ToArray();
        var orders = new JsonArray();
        foreach (var hold in holds)
        {
            var dispatch = originals.Where(value => value.Transition.Operation == GovernanceGuardOperation.CommitDispatch
                && value.Transition.DeletionRequestId == hold.Transition.DeletionRequestId).OrderBy(value => value.Receipt.GuardHighWater).LastOrDefault()
                ?? throw new InvalidOperationException("Actual dispatched or refused guard original required.");
            orders.Add(new JsonObject { ["holdOperationId"] = hold.Transition.OperationId, ["holdRevision"] = hold.Receipt.GuardHighWater,
                ["dispatchOperationId"] = dispatch.Transition.OperationId, ["dispatchRevision"] = dispatch.Receipt.GuardHighWater,
                ["dispatchWon"] = dispatch.Receipt.Status == "Committed" && dispatch.Receipt.GuardHighWater < hold.Receipt.GuardHighWater,
                ["dispatchStatus"] = dispatch.Receipt.Status });
        }
        if (orders.Count == 0) { throw new InvalidOperationException("Actual current hold originals unavailable."); }
        observed["holdOrders"] = orders; observed["guardAttempts"] = JsonSerializer.SerializeToNode(originals);
        observed["guardReceipts"] = guard["Receipts"]!.DeepClone();
    }

    private static void ProjectActivation(JsonObject observed, JsonObject ledger, JsonObject batch, JsonObject guard)
    {
        string batchId = batch["Current"]!["Capability"]!["BatchId"]!.GetValue<string>();
        var blocked = ((JsonArray)ledger["Operations"]!).OfType<JsonObject>().LastOrDefault(value => value["BlockedReplacementOriginal"] is JsonObject original
            && original["Capability"]!["BatchId"]!.GetValue<string>() == batchId);
        if (blocked?["BlockedReplacementOriginal"] is JsonObject rejected)
        {
            var rejectedOutcome = blocked["Outcome"]!;
            observed["activationOutcome"] = new JsonObject { ["status"] = "ActivationBlockedByReplacementKeyCompromise", ["newBlockReceiptId"] = rejectedOutcome["ReceiptId"]!.DeepClone(),
                ["newKeyVersion"] = rejectedOutcome["BlockedKeyVersion"]!.DeepClone(), ["revocationRevision"] = rejectedOutcome["RevocationRevision"]!.DeepClone() };
            observed[rejected["UnissuedProof"] is null ? "blockedReplacementOriginal" : "rejectedUnissuedOriginal"] = new JsonObject
                { ["Original"] = rejected.DeepClone(), ["Outcome"] = rejectedOutcome.DeepClone(), ["IssuedRequest"] = batch["Current"]!.DeepClone() };
            observed["ownerRevocations"] = ledger["Revocations"]!.DeepClone(); observed["guardRevocations"] = guard["Revocations"]!.DeepClone();
            return;
        }
        var operation = ((JsonArray)ledger["Operations"]!).OfType<JsonObject>().LastOrDefault(value => value["ActivationOriginal"] is JsonObject original
            && original["Replacement"]!["Capability"]!["BatchId"]!.GetValue<string>() == batchId);
        if (operation?["ActivationOriginal"] is not JsonObject activation) { return; }
        var outcome = (JsonObject)operation["Outcome"]!; var replacement = activation["Replacement"]!["Capability"]!;
        if (outcome["Status"]!.GetValue<int>() == (int)DeletionConsumptionStatus.Unconsumed
            && batch["Outcome"]!["Status"]!.GetValue<int>() == (int)DeletionConsumptionStatus.ConsumptionBlocked
            && batch["Outcome"]!["BlockedKeyVersion"]?.GetValue<string>() == replacement["CapabilityKeyVersion"]!.GetValue<string>())
        {
            var currentBlock = batch["Outcome"]!;
            var revocation = ((JsonArray)ledger["Revocations"]!).OfType<JsonObject>().Single(value => value["Envelope"]!["KeyVersion"]!.GetValue<string>() == replacement["CapabilityKeyVersion"]!.GetValue<string>()
                && value["Envelope"]!["RevocationRevision"]!.GetValue<long>() == currentBlock["RevocationRevision"]!.GetValue<long>());
            observed["postActivationBlockOriginal"] = new JsonObject { ["Activation"] = activation.DeepClone(), ["ActivatedOutcome"] = outcome.DeepClone(),
                ["RevocationReceipt"] = revocation.DeepClone(), ["BlockedOutcome"] = currentBlock.DeepClone() };
            observed["activationOutcome"] = new JsonObject { ["status"] = "ActivatedThenReplacementBlocked", ["newBlockReceiptId"] = currentBlock["ReceiptId"]!.DeepClone(),
                ["newKeyVersion"] = currentBlock["BlockedKeyVersion"]!.DeepClone(), ["revocationRevision"] = currentBlock["RevocationRevision"]!.DeepClone() };
            observed["ownerRevocations"] = ledger["Revocations"]!.DeepClone(); observed["guardRevocations"] = guard["Revocations"]!.DeepClone(); return;
        }
        if (outcome["Status"]!.GetValue<int>() == (int)DeletionConsumptionStatus.ActivationBlockedByReplacementKeyCompromise)
        {
            observed["activationOutcome"] = new JsonObject { ["status"] = "ActivationBlockedByReplacementKeyCompromise", ["newBlockReceiptId"] = outcome["ReceiptId"]!.DeepClone(),
                ["newKeyVersion"] = outcome["BlockedKeyVersion"]!.DeepClone(), ["revocationRevision"] = outcome["RevocationRevision"]!.DeepClone() };
            observed["activationOriginal"] = new JsonObject { ["OperationId"] = activation["OperationId"]!.DeepClone(), ["NewBlockReceiptId"] = outcome["ReceiptId"]!.DeepClone(),
                ["NewKeyVersion"] = outcome["BlockedKeyVersion"]!.DeepClone(), ["RevocationRevision"] = outcome["RevocationRevision"]!.DeepClone(), ["BatchId"] = batchId }; return;
        }
        var original = batch["Original"]!["Capability"]!;
        bool revoked = ((JsonArray)guard["CompromisedKeyVersions"]!).Any(version => version!.GetValue<string>() == original["CapabilityKeyVersion"]!.GetValue<string>());
        int active = ((JsonArray)guard["CompromisedKeyVersions"]!).Any(version => version!.GetValue<string>() == replacement["CapabilityKeyVersion"]!.GetValue<string>()) ? 0 : 1;
        observed["reattestation"] = new JsonObject { ["sameBatch"] = original["BatchId"]!.GetValue<string>() == replacement["BatchId"]!.GetValue<string>(),
            ["sameManifest"] = original["ManifestDigest"]!.GetValue<string>() == replacement["ManifestDigest"]!.GetValue<string>(), ["oldAttestationRevoked"] = revoked, ["activeCredentialCount"] = active,
            ["compromiseBlockReceiptId"] = activation["CompromiseBlockReceiptId"]!.DeepClone(), ["expectedBlockSetRevision"] = activation["ExpectedKeyBlockSetRevision"]!.DeepClone(),
            ["activatedBlockSetRevision"] = outcome["KeyBlockSetRevision"]!.DeepClone() };
        observed["activationOriginal"] = new JsonObject { ["OperationId"] = activation["OperationId"]!.DeepClone(), ["CompromiseBlockReceiptId"] = activation["CompromiseBlockReceiptId"]!.DeepClone(),
            ["ExpectedKeyBlockSetRevision"] = activation["ExpectedKeyBlockSetRevision"]!.DeepClone(), ["ActivatedBlockSetRevision"] = outcome["KeyBlockSetRevision"]!.DeepClone(),
            ["GuardReplacementReceiptId"] = activation["GuardReplacementReceiptId"]!.DeepClone(), ["BatchId"] = batchId, ["ManifestDigest"] = replacement["ManifestDigest"]!.DeepClone(),
            ["AttestationOrdinal"] = replacement["AttestationOrdinal"]!.DeepClone(), ["OldAttestationRevoked"] = revoked, ["ActiveCredentialCount"] = active };
    }
    private static JsonObject OwnerSummary(JsonObject outcome) => new() { ["status"] = ((DeletionConsumptionStatus)outcome["Status"]!.GetValue<int>()).ToString(),
        ["receiptId"] = outcome["ReceiptId"]?.DeepClone(), ["blockedKeyVersion"] = outcome["BlockedKeyVersion"]?.DeepClone(), ["revocationRevision"] = outcome["RevocationRevision"]?.DeepClone() };
    private static JsonObject Select(JsonObject value, params string[] fields) { var result = new JsonObject(); foreach (string field in fields) { result[field] = value[field]?.DeepClone(); } return result; }
    private static JsonNode StripStatus(JsonNode node) { var value = node.DeepClone(); if (value is JsonObject obj) { obj.Remove("status"); obj.Remove("effectBefore"); obj.Remove("effectAfter"); obj.Remove("pinRefusals"); } return value; }
    private static JsonArray Array(IEnumerable<JsonNode?> values) => new(values.Select(value => value?.DeepClone()).ToArray());
}
