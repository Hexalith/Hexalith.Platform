using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;
using Hexalith.EventStore.Server.Security;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Protection;

/// <summary>Owned executable native fixture driver. Every call goes through the shipped engine/owner/custody/signing ports or actual scoped native source storage.</summary>
public sealed class ProtectionQualificationNativeDriver(InteractionPayloadProtectionService engine, ErasureRootCustody custody, ErasureRootOccurrenceKeyProvider keys,
    ErasureRootNativeOccurrenceSource source, DaprClient native, IGovernanceScopeGuard guard, IDeletionProtectionOwner protection,
    IDeletionConsumptionActor consumption, IDeletionCapabilitySigningActor signer, DeletionCapabilityRevocationSubscriber compromise,
    ProtectionQualificationFaults faults, IReadOnlySet<string> fixtureSourceComponents, IErasureRootAuthorization authorization)
{
    /// <summary>Resolves only actual retained original outcomes. It never repeats a native effect, signs, reserves, restores, unwraps or arms another fault.</summary>
    public async Task<JsonNode?> RecoverAsync(ProtectionQualificationNativeCall step, CancellationToken token)
    {
        T Read<T>() => step.Payload.Deserialize<T>(ErasureRootCustody.Json) ?? throw new InvalidOperationException("Exact original native input required.");
        object? result = null;
        switch (step.Kind)
        {
            case ProtectionQualificationNativeCallKind.Register:
                result = (await consumption.ReadRegistrationOriginalAsync(Read<DeletionBatchConsumptionRequest>()).WaitAsync(token).ConfigureAwait(false))?.Outcome; break;
            case ProtectionQualificationNativeCallKind.Reserve:
                var request = Read<DeletionBatchConsumptionRequest>();
                var reserved = await consumption.ReadReservationOriginalAsync(request).WaitAsync(token).ConfigureAwait(false);
                var read = await custody.ReadAsync(request.Capability.TenantId, token).ConfigureAwait(false);
                if (reserved is not null && read is { } nativeHead && nativeHead.Head.Anchors.TryGetValue(DeletionConsumptionActor.GetOwnerScope(request.Capability.TenantId), out var anchor))
                {
                    var latest = anchor.Completed.Values.SingleOrDefault(value => value.TargetRevision == anchor.Revision && value.TargetDigest == anchor.Digest);
                    if (latest is not null)
                    {
                        using var ledger = JsonDocument.Parse(latest.TargetBytes);
                        var batch = ledger.RootElement.GetProperty("Batches").EnumerateArray().SingleOrDefault(value => value.GetProperty("Current").GetProperty("Capability").GetProperty("BatchId").GetString() == request.Capability.BatchId);
                        if (batch.ValueKind == JsonValueKind.Object && ErasureRootCustody.Hash(batch.GetProperty("Current").Deserialize<DeletionBatchConsumptionRequest>()) == ErasureRootCustody.Hash(request))
                        { result = batch.GetProperty("Outcome").Deserialize<DeletionConsumptionOutcome>(); }
                    }
                }
                break;
            case ProtectionQualificationNativeCallKind.Block:
                result = (await consumption.ReadBlockOriginalAsync(Read<DeletionBatchBlockRequest>()).WaitAsync(token).ConfigureAwait(false))?.Outcome; break;
            case ProtectionQualificationNativeCallKind.Activate:
                result = (await consumption.ReadActivationOriginalAsync(Read<DeletionReattestationActivation>()).WaitAsync(token).ConfigureAwait(false))?.Outcome; break;
            case ProtectionQualificationNativeCallKind.Sign:
            case ProtectionQualificationNativeCallKind.ObsoleteUnissued:
                result = await signer.LookupAsync(Read<DeletionBatchCapabilityV1>()).WaitAsync(token).ConfigureAwait(false); break;
            case ProtectionQualificationNativeCallKind.Lifecycle:
                var lifecycle = step.Payload["request"]!.Deserialize<CustodyKeyLifecycleRequest>()!;
                result = await custody.LookupAsync(step.Payload["registration"]!.Deserialize<CustodyKeyRegistration>()!, lifecycle, ErasureRootCustody.Hash(lifecycle), token).ConfigureAwait(false); break;
            case ProtectionQualificationNativeCallKind.Guard:
                var transition = step.Payload["transition"]!.Deserialize<GovernanceGuardTransition>()!;
                var targets = step.Payload["targets"]!.Deserialize<GuardedStateMutation[]>()!;
                string intent = ErasureRootCustody.Hash(new { Transition = transition, TargetMutationDigest = ErasureRootCustody.Hash(targets) });
                var guardState = await guard.ReadAsync(transition.TenantId, token).ConfigureAwait(false);
                result = guardState?.Receipts.SingleOrDefault(receipt => receipt.OperationId == transition.OperationId && receipt.IntentDigest == intent); break;
            case ProtectionQualificationNativeCallKind.Compromise:
                var envelope = step.Payload["envelope"]!.Deserialize<DeletionCapabilityRevocationEnvelope>()!;
                var revocation = await consumption.LookupRevocationAsync(envelope).WaitAsync(token).ConfigureAwait(false);
                var mirrored = await guard.ReadAsync(envelope.TenantId, token).ConfigureAwait(false);
                if (revocation is not null && mirrored is not null && mirrored.Revocations.Any(value => ErasureRootCustody.Hash(value) == ErasureRootCustody.Hash(revocation)))
                { result = new { status = "Completed", receipt = revocation }; }
                break;
            case ProtectionQualificationNativeCallKind.SealEvent:
                var sealedInput = Read<ProtectionQualificationNativeSeal>();
                var stored = await ReadEventAsync(sealedInput.Source, token).ConfigureAwait(false);
                if (stored.Binding == sealedInput.Enrollment.Binding && stored.Protected.Metadata is { State: PayloadProtectionState.Protected, Scheme: "hexalith-pdenc-v2" }
                    && stored.Protected.SerializationFormat == "json+pdenc-v2")
                { result = new { status = "Completed", protectionResult = stored.Protected }; }
                break;
            case ProtectionQualificationNativeCallKind.SealSnapshot:
                var snapshotCell = (step.Payload["Source"] ?? step.Payload["source"])!.Deserialize<ProtectionQualificationNativeCell>()!; Check(snapshotCell);
                var snapshot = await native.GetStateAsync<SnapshotProtectionResult>(snapshotCell.Component, snapshotCell.Key, ConsistencyMode.Strong, snapshotCell.Metadata, token).ConfigureAwait(false);
                if (snapshot?.Metadata is { State: PayloadProtectionState.Protected, Scheme: "hexalith-pdenc-v2" }) { result = new { status = "Completed", protectionResult = snapshot }; }
                break;
            case ProtectionQualificationNativeCallKind.ReplayEvent:
                var replay = Read<ProtectionQualificationNativeCell>(); Check(replay);
                result = await native.GetStateAsync<JsonElement>(replay.Component, replay.Key + ":replay", ConsistencyMode.Strong, replay.Metadata, token).ConfigureAwait(false); break;
            case ProtectionQualificationNativeCallKind.Backup:
            case ProtectionQualificationNativeCallKind.Restore:
                var cell = Read<ProtectionQualificationNativeCell>(); Check(cell);
                var backup = await native.GetStateAsync<JsonElement>(cell.Component, cell.Key + ":backup", ConsistencyMode.Strong, cell.Metadata, token).ConfigureAwait(false);
                if (backup.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
                {
                    var actual = await native.GetStateAsync<JsonElement>(cell.Component, cell.Key, ConsistencyMode.Strong, cell.Metadata, token).ConfigureAwait(false);
                    if (step.Kind == ProtectionQualificationNativeCallKind.Backup || ErasureRootCustody.Hash(backup) == ErasureRootCustody.Hash(actual))
                    { result = new { status = "Completed", receiptId = ErasureRootCustody.Hash(new { cell, backup }) }; }
                }
                break;
        }
        return Result(result);
    }
    /// <summary>Runs an exact native operation once. Native failures remain unknown and are never converted into a passing label.</summary>
    public async Task<JsonNode?> ExecuteAsync(ProtectionQualificationNativeCall step, JsonObject request, CancellationToken token)
    {
        await faults.CheckAsync(step.Id, "before", token).ConfigureAwait(false);
        T Read<T>() => step.Payload.Deserialize<T>(ErasureRootCustody.Json) ?? throw new InvalidOperationException("Exact typed native input required.");
        object? result;
        switch (step.Kind)
        {
            case ProtectionQualificationNativeCallKind.SealEvent:
                result = await SealAsync(Read<ProtectionQualificationNativeSeal>(), request, step.Payload["changedCanaryBase64"]?.GetValue<string>(), token).ConfigureAwait(false); break;
            case ProtectionQualificationNativeCallKind.UnsealEvent:
                var opening = await ReadEventAsync(Read<ProtectionQualificationNativeCell>(), token).ConfigureAwait(false);
                var opened = await engine.TryUnprotectEventPayloadAsync(opening.Binding.Identity.Owner, opening.Binding.Identity.PayloadTypeId, opening.Protected.PayloadBytes,
                    opening.Protected.SerializationFormat, opening.Protected.Metadata, ErasureRootOccurrenceBindings.Occurrence(opening.Binding.Identity), token).ConfigureAwait(false);
                if (!opened.IsReadable) { throw new InvalidOperationException("Actual native payload unreadable."); }
                try
                {
                    using var clear = JsonDocument.Parse(opened.PayloadBytes!);
                    result = new { canaryBase64 = clear.RootElement.GetProperty("ProtectedContent").GetProperty("Value").GetProperty("CanaryBase64").GetString() };
                }
                finally { CryptographicOperations.ZeroMemory(opened.PayloadBytes!); }
                break;
            case ProtectionQualificationNativeCallKind.ReplayEvent:
                var replayCell = Read<ProtectionQualificationNativeCell>(); var replaying = await ReadEventAsync(replayCell, token).ConfigureAwait(false);
                var replay = await engine.TryReplayEventPayloadAsync(replaying.Binding.Identity.Owner, replaying.Binding.Identity.PayloadTypeId, replaying.Protected.PayloadBytes,
                    replaying.Protected.SerializationFormat, replaying.Protected.Metadata, ErasureRootOccurrenceBindings.Occurrence(replaying.Binding.Identity), token).ConfigureAwait(false);
                try
                {
                    JsonElement? erased = null;
                    if (replay.HasErasedContent && replay.Outcome.PayloadBytes is { } replayBytes)
                    {
                        using var replayJson = JsonDocument.Parse(replayBytes);
                        var field = replayJson.RootElement.GetProperty("ProtectedContent").GetProperty("Value");
                        if (field.GetProperty("State").GetString() != "Erased") { throw new InvalidOperationException("Actual typed erased replay required."); }
                        erased = field.Clone();
                    }
                    var external = await engine.TryUnprotectEventPayloadAsync(replaying.Binding.Identity.Owner, replaying.Binding.Identity.PayloadTypeId, replaying.Protected.PayloadBytes,
                        replaying.Protected.SerializationFormat, replaying.Protected.Metadata, ErasureRootOccurrenceBindings.Occurrence(replaying.Binding.Identity), token).ConfigureAwait(false);
                    try
                    {
                        var projection = new { target = replaying.Binding.Identity.Target, field = erased, externalOutcome = external.IsReadable ? "Readable" : "Unreadable" };
                        await native.SaveStateAsync(replayCell.Component, replayCell.Key + ":replay", projection, metadata: replayCell.Metadata, cancellationToken: token).ConfigureAwait(false);
                        result = projection;
                    }
                    finally { if (external.PayloadBytes is { } openedBytes) { CryptographicOperations.ZeroMemory(openedBytes); } }
                }
                finally { if (replay.Outcome.PayloadBytes is { } replayBytes) { CryptographicOperations.ZeroMemory(replayBytes); } }
                break;
            case ProtectionQualificationNativeCallKind.Lifecycle:
                var lifecycle = step.Payload["request"]!.Deserialize<CustodyKeyLifecycleRequest>()!;
                var registration = step.Payload["registration"]!.Deserialize<CustodyKeyRegistration>()!;
                result = await custody.ExecuteAsync(registration, lifecycle, ErasureRootCustody.Hash(lifecycle), token).ConfigureAwait(false); break;
            case ProtectionQualificationNativeCallKind.AuthorizeLifecycle:
                var exactFence = Read<CustodyKeyLifecycleRequest>();
                bool permitted = await authorization.VerifyLifecycleAsync(exactFence, token).ConfigureAwait(false);
                result = new { status = permitted ? "Authorized" : "Denied", requestDigest = ErasureRootCustody.Hash(exactFence), fenceTokenId = exactFence.FenceTokenId }; break;
            case ProtectionQualificationNativeCallKind.Guard:
                result = await guard.ExecuteAsync(step.Payload["transition"]!.Deserialize<GovernanceGuardTransition>()!,
                    step.Payload["targets"]!.Deserialize<GuardedStateMutation[]>()!, token).ConfigureAwait(false); break;
            case ProtectionQualificationNativeCallKind.Register: result = await protection.RegisterAsync(Read<DeletionBatchConsumptionRequest>(), token).ConfigureAwait(false); break;
            case ProtectionQualificationNativeCallKind.Reserve: result = await protection.ReserveAndConsumeAsync(Read<DeletionBatchConsumptionRequest>(), token).ConfigureAwait(false); break;
            case ProtectionQualificationNativeCallKind.Block: result = await consumption.BlockAsync(Read<DeletionBatchBlockRequest>()).WaitAsync(token).ConfigureAwait(false); break;
            case ProtectionQualificationNativeCallKind.Activate: result = await protection.ActivateAsync(Read<DeletionReattestationActivation>(), token).ConfigureAwait(false); break;
            case ProtectionQualificationNativeCallKind.Sign: result = await signer.SignAsync(Read<DeletionBatchCapabilityV1>()).WaitAsync(token).ConfigureAwait(false); break;
            case ProtectionQualificationNativeCallKind.ObsoleteUnissued: result = await signer.ObsoleteUnissuedAsync(Read<DeletionBatchCapabilityV1>()).WaitAsync(token).ConfigureAwait(false); break;
            case ProtectionQualificationNativeCallKind.Compromise:
                result = await compromise.ReceiveAsync(step.Payload["envelope"]!.Deserialize<DeletionCapabilityRevocationEnvelope>()!, step.Payload["signedEvidence"]!.GetValue<string>(), token).ConfigureAwait(false); break;
            case ProtectionQualificationNativeCallKind.Lookup:
                result = await protection.LookupAsync(step.Payload["tenantId"]!.GetValue<string>(), step.Payload["batchId"]!.GetValue<string>(), token).ConfigureAwait(false); break;
            case ProtectionQualificationNativeCallKind.Backup:
                var backup = Read<ProtectionQualificationNativeCell>(); Check(backup);
                var actual = await native.GetStateAsync<JsonElement>(backup.Component, backup.Key, ConsistencyMode.Strong, backup.Metadata, token).ConfigureAwait(false);
                if (actual.ValueKind == JsonValueKind.Undefined) { throw new InvalidOperationException("Missing native backup source."); }
                await native.SaveStateAsync(backup.Component, backup.Key + ":backup", actual, metadata: backup.Metadata, cancellationToken: token).ConfigureAwait(false);
                result = new { receiptId = ErasureRootCustody.Hash(new { backup, actual }) }; break;
            case ProtectionQualificationNativeCallKind.Restore:
                var restore = Read<ProtectionQualificationNativeCell>(); Check(restore);
                var saved = await native.GetStateAsync<JsonElement>(restore.Component, restore.Key + ":backup", ConsistencyMode.Strong, restore.Metadata, token).ConfigureAwait(false);
                if (saved.ValueKind == JsonValueKind.Undefined) { throw new InvalidOperationException("Missing exact original backup."); }
                await native.SaveStateAsync(restore.Component, restore.Key, saved, metadata: restore.Metadata, cancellationToken: token).ConfigureAwait(false);
                result = new { receiptId = ErasureRootCustody.Hash(new { restore, saved }) }; break;
            case ProtectionQualificationNativeCallKind.Fault:
                await faults.ArmAsync(step.Payload["stepId"]!.GetValue<string>(), step.Payload["boundary"]!.GetValue<string>(), token).ConfigureAwait(false);
                result = new { originalStepId = step.Payload["stepId"]!.GetValue<string>(), armed = true }; break;
            case ProtectionQualificationNativeCallKind.SealSnapshot:
                if (step.Payload.ContainsKey("Entries"))
                { result = await SealMixedSnapshotAsync(Read<ProtectionQualificationNativeSnapshot>(), request, token).ConfigureAwait(false); break; }
                var snapshot = new ProtectionQualificationNativeSeal(step.Payload["enrollment"]!.Deserialize<ErasureRootOccurrenceEnrollment>()!, step.Payload["source"]!.Deserialize<ProtectionQualificationNativeCell>()!); Check(snapshot.Source);
                if (!await source.RegisterOriginalAsync(snapshot.Enrollment, token).ConfigureAwait(false)) { throw new InvalidOperationException("Independent snapshot original unavailable."); }
                var state = JsonSerializer.SerializeToElement(new { CanaryBase64 = request["canaryBase64"]!.GetValue<string>() });
                var type = (JsonTypeInfo)new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() }.GetTypeInfo(typeof(JsonElement));
                var protectedSnapshot = await engine.ProtectSnapshotAsync(snapshot.Enrollment.Binding.Identity.Owner, state, type,
                    ErasureRootOccurrenceBindings.Occurrence(snapshot.Enrollment.Binding.Identity), token).ConfigureAwait(false);
                await engine.AcquirePayloadProtectionCompletionLeaseAsync(protectedSnapshot.CompletionContext!, token).ConfigureAwait(false);
                var snapshotWriter = snapshot.Enrollment.Writer ?? throw new InvalidOperationException("Original snapshot writer unavailable.");
                if (snapshotWriter.Component != snapshot.Source.Component) { throw new InvalidOperationException("Atomic source partition required."); }
                var snapshotProof = new ErasureRootWriterProof(snapshot.Enrollment.Binding, Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(protectedSnapshot.ProtectionResult.State))),
                    true, true, "snapshot-source:" + ErasureRootCustody.Hash(snapshot.Source));
                await native.ExecuteStateTransactionAsync(snapshot.Source.Component,
                    [new(snapshot.Source.Key, JsonSerializer.SerializeToUtf8Bytes(protectedSnapshot.ProtectionResult), StateOperationType.Upsert),
                     new(snapshot.Source.Key + ":cache", JsonSerializer.SerializeToUtf8Bytes(protectedSnapshot.ProtectionResult), StateOperationType.Upsert),
                     new(snapshotWriter.Key, JsonSerializer.SerializeToUtf8Bytes(snapshotProof), StateOperationType.Upsert)], snapshot.Source.Metadata, token).ConfigureAwait(false);
                await engine.CompletePayloadProtectionAsync(protectedSnapshot.CompletionContext!, PayloadProtectionPersistenceOutcome.Persisted, token).ConfigureAwait(false);
                result = protectedSnapshot; break;
            default: throw new InvalidOperationException("Unsupported native operation.");
        }
        await keys.CompleteDisposedUsesAsync(token).ConfigureAwait(false);
        await faults.CheckAsync(step.Id, "after", token).ConfigureAwait(false);
        return Result(result);
    }
    private static JsonNode? Result(object? result)
    {
        var node = JsonSerializer.SerializeToNode(result);
        if (node is JsonObject outcome)
        {
            string? status = result switch
            {
                DeletionConsumptionOutcome actual => actual.Status.ToString(),
                CustodyKeyLifecycleOutcome actual => actual.Status.ToString(),
                DeletionCapabilitySigningOutcome actual => actual.State.ToString(),
                _ => null,
            };
            if (status is not null) { outcome["status"] = status; }
        }
        return node;
    }

    private async Task<PayloadProtectionWriteResult> SealAsync(ProtectionQualificationNativeSeal input, JsonObject request, string? changedCanary, CancellationToken token)
    {
        Check(input.Source);
        var binding = input.Enrollment.Binding;
        if (input.Enrollment.Writer is not { } writer || writer.Component != input.Source.Component
            || !await source.RegisterOriginalAsync(input.Enrollment, token).ConfigureAwait(false)) { throw new InvalidOperationException("Independent native source original unavailable."); }
        byte[] canary = Convert.FromBase64String(changedCanary ?? request["canaryBase64"]!.GetValue<string>());
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new { ProtectedContent = new { Target = binding.Identity.Target, Value = new { CanaryBase64 = Convert.ToBase64String(canary) } } });
        try
        {
            var sealedResult = await engine.ProtectEventPayloadAsync(binding.Identity.Owner, new ProtectionQualificationEventPayload(), binding.Identity.PayloadTypeId, payload, "json",
                ErasureRootOccurrenceBindings.Occurrence(binding.Identity), token).ConfigureAwait(false);
            await engine.AcquirePayloadProtectionCompletionLeaseAsync(sealedResult.CompletionContext!, token).ConfigureAwait(false);
            var sourceEvent = new ProtectionQualificationPersistedEvent(binding, sealedResult.ProtectionResult);
            var proof = new ErasureRootWriterProof(binding, Convert.ToHexString(SHA256.HashData(sealedResult.ProtectionResult.PayloadBytes)), true, true,
                "source:" + ErasureRootCustody.Hash(new { binding, input.Source }));
            await native.ExecuteStateTransactionAsync(input.Source.Component,
                [new(input.Source.Key, JsonSerializer.SerializeToUtf8Bytes(sourceEvent), StateOperationType.Upsert), new(writer.Key, JsonSerializer.SerializeToUtf8Bytes(proof), StateOperationType.Upsert)],
                input.Source.Metadata, token).ConfigureAwait(false);
            await engine.CompletePayloadProtectionAsync(sealedResult.CompletionContext!, PayloadProtectionPersistenceOutcome.Persisted, token).ConfigureAwait(false);
            return sealedResult;
        }
        finally { CryptographicOperations.ZeroMemory(canary); CryptographicOperations.ZeroMemory(payload); }
    }
    private async Task<SnapshotProtectionWriteResult> SealMixedSnapshotAsync(ProtectionQualificationNativeSnapshot input, JsonObject request, CancellationToken token)
    {
        Check(input.Source);
        if (string.IsNullOrWhiteSpace(input.Source.CompletionKey)) { throw new InvalidOperationException("Explicit native composite completion journal required."); }
        var targets = request["targets"]!.Deserialize<ProtectionTarget[]>()!;
        var state = JsonSerializer.SerializeToElement(new { Entries = targets.Select(target => new { ProtectedContent = new { Target = target,
            Value = new { CanaryBase64 = request["canaryBase64"]!.GetValue<string>() } } }).ToArray() });
        var layout = InteractionSnapshotEntries.Describe(JsonSerializer.SerializeToUtf8Bytes(state), targets[0].TenantId, token);
        if (input.Entries.Length != layout.Entries.Count || input.Entries.Length is < 2 or > 1000) { throw new InvalidOperationException("Complete mixed-root native source manifest required."); }
        var originals = new List<ErasureRootOccurrenceEnrollment>();
        for (int index = 0; index < input.Entries.Length; index++)
        {
            var original = input.Entries[index]; var described = layout.Entries[index];
            if (original.Writer is not { } writer || writer.Component != input.Source.Component || original.Binding.Identity.Target != described.Target
                || ErasureRootCustody.Hash(writer.Metadata) != ErasureRootCustody.Hash(input.Source.Metadata)
                || ErasureRootOccurrenceBindings.Occurrence(original.Binding.Identity) != InteractionSnapshotEntries.EntryOccurrence(input.Parent, described.Path))
            { throw new InvalidOperationException("Exact entry target, source partition and sub-occurrence required."); }
            originals.Add(original with { SnapshotEntry = new(input.Parent, described.Path, layout.ContainerDigest, layout.Entries.Count) });
        }
        foreach (var original in originals)
        { if (!await source.RegisterOriginalAsync(original, token).ConfigureAwait(false)) { throw new InvalidOperationException("Independent mixed snapshot original unavailable."); } }
        var type = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() }.GetTypeInfo(typeof(JsonElement));
        var write = await engine.ProtectSnapshotAsync(originals[0].Binding.Identity.Owner, state, type, input.Parent, token).ConfigureAwait(false);
        await engine.AcquirePayloadProtectionCompletionLeaseAsync(write.CompletionContext!, token).ConfigureAwait(false);
        var carrier = write.ProtectionResult.State as ProtectedSnapshotEntriesV2 ?? throw new InvalidOperationException("Actual mixed-root snapshot carrier required.");
        var operations = new List<StateTransactionRequest>
        {
            new(input.Source.Key, JsonSerializer.SerializeToUtf8Bytes(write.ProtectionResult), StateOperationType.Upsert),
            new(input.Source.Key + ":cache", JsonSerializer.SerializeToUtf8Bytes(write.ProtectionResult), StateOperationType.Upsert),
            new(input.Source.CompletionKey, JsonSerializer.SerializeToUtf8Bytes(write.CompletionContext), StateOperationType.Upsert),
        };
        for (int index = 0; index < originals.Count; index++)
        {
            byte[] sealedBytes = JsonSerializer.SerializeToUtf8Bytes(carrier.Entries[index].Protected);
            var proof = new ErasureRootWriterProof(originals[index].Binding, Convert.ToHexString(SHA256.HashData(sealedBytes)), true, true,
                "mixed-snapshot-source:" + ErasureRootCustody.Hash(new { input.Source, path = carrier.Entries[index].Path }));
            operations.Add(new(originals[index].Writer!.Key, JsonSerializer.SerializeToUtf8Bytes(proof), StateOperationType.Upsert));
        }
        await native.ExecuteStateTransactionAsync(input.Source.Component, operations, input.Source.Metadata, token).ConfigureAwait(false);
        await engine.CompletePayloadProtectionAsync(write.CompletionContext!, PayloadProtectionPersistenceOutcome.Persisted, token).ConfigureAwait(false);
        return write;
    }
    private async Task<ProtectionQualificationPersistedEvent> ReadEventAsync(ProtectionQualificationNativeCell cell, CancellationToken token)
    {
        Check(cell);
        return await native.GetStateAsync<ProtectionQualificationPersistedEvent>(cell.Component, cell.Key, ConsistencyMode.Strong, cell.Metadata, token).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Original native source unavailable.");
    }
    private void Check(ProtectionQualificationNativeCell cell)
    {
        if (!fixtureSourceComponents.Contains(cell.Component) || string.IsNullOrWhiteSpace(cell.Key)) { throw new InvalidOperationException("Only isolated non-ERC source cells are permitted."); }
    }
}
