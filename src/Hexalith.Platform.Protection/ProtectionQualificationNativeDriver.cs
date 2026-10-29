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
    IErasureRootOccurrenceSource source, DaprClient native, IGovernanceScopeGuard guard, IDeletionProtectionOwner protection,
    IDeletionConsumptionActor consumption, IDeletionCapabilitySigningActor signer, DeletionCapabilityRevocationSubscriber compromise,
    ProtectionQualificationFaults faults, IReadOnlySet<string> fixtureSourceComponents, IErasureRootAuthorization authorization)
{
    /// <summary>Captures the actual source-owned compare/census before the durable native-call admission. Execution never refreshes an admitted compare.</summary>
    public async Task<ProtectionQualificationNativeCall> PrepareCallAsync(ProtectionQualificationNativeCall input, CancellationToken token)
    {
        var payload = (JsonObject)input.Payload.DeepClone();
        if (input.Kind == ProtectionQualificationNativeCallKind.Sign)
        {
            var capability = payload.Deserialize<DeletionBatchCapabilityV1>()!;
            var current = await guard.ReadAsync(capability.TenantId, token).ConfigureAwait(false) ?? throw new InvalidOperationException("Native signing compare unavailable.");
            var deletion = current.Deletions.Single(value => value.RequestId == capability.DeletionRequestId);
            var batch = deletion.Batches.Single(value => value.BatchId == capability.BatchId);
            if (deletion.SealId != capability.DestructionSealId || batch.ManifestDigest != capability.ManifestDigest || batch.Kind != capability.BatchKind || batch.Ordinal != capability.BatchOrdinal)
            { throw new InvalidOperationException("Exact native sealed batch required before signing."); }
            payload = (JsonObject)JsonSerializer.SerializeToNode(capability with { IntendedIssuedGuardRevision = current.Revision })!;
        }
        if (input.Kind == ProtectionQualificationNativeCallKind.Guard)
        {
            var transition = payload["transition"]!.Deserialize<GovernanceGuardTransition>()!;
            if (payload["UseCurrentGuardRevision"]?.GetValue<bool>() is true)
            { transition = transition with { ExpectedGuardRevision = (await guard.ReadAsync(transition.TenantId, token).ConfigureAwait(false))?.Revision ?? throw new InvalidOperationException("Native guard compare unavailable.") }; }
            else if (transition.Operation is GovernanceGuardOperation.RecordBatchIssued or GovernanceGuardOperation.ReplaceAttestation or GovernanceGuardOperation.RecordBatchIssuanceStale)
            { transition = transition with { ExpectedGuardRevision = transition.Batch?.Capability?.IntendedIssuedGuardRevision ?? throw new InvalidOperationException("Exact signed intended compare required.") }; }
            if (transition.Operation is GovernanceGuardOperation.RecordBatchIssued or GovernanceGuardOperation.ReplaceAttestation)
            {
                var batch = transition.Batch ?? throw new InvalidOperationException("Actual signed batch required.");
                string signingId = DeletionBatchCapabilityCodec.SigningRequestId(batch.Capability!);
                string signedDigest = DeletionBatchCapabilityIdentity.SignedArtifactDigest(batch.DetachedJws);
                if (batch.SigningRequestId != signingId || batch.AttestationDigest != signedDigest) { throw new InvalidOperationException("Actual canonical signed artifact required."); }
                transition = transition with { OperationId = DeletionBatchCapabilityIdentity.IssueOperationId(signingId, signedDigest) };
            }
            payload["transition"] = JsonSerializer.SerializeToNode(transition); payload.Remove("UseCurrentGuardRevision");
        }
        if (input.Kind == ProtectionQualificationNativeCallKind.GuardOwnersEffective)
        {
            var transition = payload["transition"]!.Deserialize<GovernanceGuardTransition>()!;
            var state = await guard.ReadAsync(transition.TenantId, token).ConfigureAwait(false) ?? throw new InvalidOperationException("Native owner census unavailable.");
            var deletion = state.Deletions.Single(value => value.RequestId == transition.DeletionRequestId);
            payload["transition"] = JsonSerializer.SerializeToNode(transition with { ExpectedGuardRevision = state.Revision });
            payload["ownerIds"] = JsonSerializer.SerializeToNode(deletion.RequiredOwnerIds.Order(StringComparer.Ordinal).ToArray());
            payload["expectedOrdinal"] = deletion.Ordinal; payload["obligationDigest"] = GovernanceScopeGuardReducer.ObligationDigest(deletion.ObligationIds);
        }
        if (input.Kind == ProtectionQualificationNativeCallKind.LookupProvider)
        {
            var request = payload["request"]!.Deserialize<DeletionBatchConsumptionRequest>()!;
            var original = await consumption.ReadReservationOriginalAsync(request).WaitAsync(token).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Actual original reservation required for provider lookup.");
            payload["reservationReceiptId"] = original.Outcome.ReceiptId ?? throw new InvalidOperationException("Actual reserved receipt required.");
        }
        return input with { Payload = payload };
    }
    /// <summary>Identifies only a previously admitted native reservation. Fixture preparation may complete that original; it cannot authorize a new manifest.</summary>
    public async Task<ProtectionQualificationNativeCall?> PendingRecoveryAsync(string tenant, string fixtureId, CancellationToken token)
    {
        var current = await custody.ReadAsync(tenant, token).ConfigureAwait(false);
        if (current is not { } read || read.Head.Pending is not { } pending) { return null; }
        return new(fixtureId + ":prior-reservation-original", ProtectionQualificationNativeCallKind.LookupProvider,
            new JsonObject { ["request"] = JsonSerializer.SerializeToNode(pending.Request), ["reservationReceiptId"] = pending.ReservationReceiptId });
    }
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
            case ProtectionQualificationNativeCallKind.LookupBlock:
                result = (await consumption.ReadBlockOriginalAsync(Read<DeletionBatchBlockRequest>()).WaitAsync(token).ConfigureAwait(false))?.Outcome; break;
            case ProtectionQualificationNativeCallKind.Activate:
            case ProtectionQualificationNativeCallKind.LookupActivation:
                result = (await consumption.ReadActivationOriginalAsync(Read<DeletionReattestationActivation>()).WaitAsync(token).ConfigureAwait(false))?.Outcome; break;
            case ProtectionQualificationNativeCallKind.ReconcileBlockedReplacement:
                var reconciliation = Read<DeletionBlockedReplacementReconciliation>();
                var phase = await protection.ReadBlockedReplacementAsync(reconciliation.Capability, token).ConfigureAwait(false);
                result = phase?.Original.OperationId == reconciliation.OperationId && phase.Original.SigningRequestId == reconciliation.SigningRequestId
                    && phase.Original.DetachedJws == reconciliation.DetachedJws ? phase.Outcome : null; break;
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
            case ProtectionQualificationNativeCallKind.GuardOwnersEffective:
                var template = step.Payload["transition"]!.Deserialize<GovernanceGuardTransition>()!;
                var currentOwners = await guard.ReadAsync(template.TenantId, token).ConfigureAwait(false);
                if (currentOwners is null) { break; }
                var originals = step.Payload["ownerIds"]!.Deserialize<string[]>()!.Select((owner, index) =>
                {
                    var command = OwnerCycleCommand(step.Payload, owner, index);
                    string intent = ErasureRootCustody.Hash(new { Transition = command, TargetMutationDigest = ErasureRootCustody.Hash(Array.Empty<GuardedStateMutation>()) });
                    return currentOwners.Receipts.SingleOrDefault(receipt => receipt.OperationId == command.OperationId && receipt.IntentDigest == intent);
                }).ToArray();
                if (originals.All(original => original?.Status == "Committed")) { result = new { status = "Completed", outcomes = originals }; }
                break;
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
        string tenant = request["targets"]![0]!["TenantId"]!.GetValue<string>();
        string? effectBefore = request["action"]!.GetValue<string>().StartsWith("Reject", StringComparison.Ordinal) ? await EffectAsync(tenant, token).ConfigureAwait(false) : null;
        T Read<T>() => step.Payload.Deserialize<T>(ErasureRootCustody.Json) ?? throw new InvalidOperationException("Exact typed native input required.");
        object? result;
        switch (step.Kind)
        {
            case ProtectionQualificationNativeCallKind.SealEvent:
                var sealing = Read<ProtectionQualificationNativeSeal>();
                try { result = await SealAsync(sealing, request, sealing.ChangePayload ? Convert.ToBase64String("different-synthetic-owner-payload"u8) : null, token).ConfigureAwait(false); }
                catch (PayloadProtectionOccurrenceConflictException) when (sealing.ChangePayload)
                { result = new { status = "Conflict" }; }
                break;
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
            case ProtectionQualificationNativeCallKind.ReplaySnapshot:
                result = await ReplaySnapshotAsync(Read<ProtectionQualificationNativeSnapshotRead>(), token).ConfigureAwait(false); break;
            case ProtectionQualificationNativeCallKind.AuthorizeLifecycle:
                var exactFence = Read<CustodyKeyLifecycleRequest>();
                var lifecycleEvidence = await authorization.ReadLifecycleEvidenceAsync(exactFence, token).ConfigureAwait(false);
                result = new { status = lifecycleEvidence is not null ? "Authorized" : "Denied", requestDigest = ErasureRootCustody.Hash(exactFence), fenceTokenId = exactFence.FenceTokenId,
                    operationId = exactFence.OperationId, receiptId = lifecycleEvidence?.ReceiptId, target = new ProtectionTarget(exactFence.Identity.TenantId, exactFence.Identity.ObjectId, exactFence.Identity.KeyAlias) }; break;
            case ProtectionQualificationNativeCallKind.Guard:
                var transition = step.Payload["transition"]!.Deserialize<GovernanceGuardTransition>()!;
                result = await guard.ExecuteAsync(transition, step.Payload["targets"]!.Deserialize<GuardedStateMutation[]>()!, token).ConfigureAwait(false); break;
            case ProtectionQualificationNativeCallKind.GuardOwnersEffective:
                result = await MakeOwnersEffectiveAsync(step.Payload, token).ConfigureAwait(false); break;
            case ProtectionQualificationNativeCallKind.RegisterIfDispatched:
            case ProtectionQualificationNativeCallKind.ReserveIfDispatched:
                var conditional = Read<DeletionBatchConsumptionRequest>();
                if (await IsHoldPreventedAsync(conditional, token).ConfigureAwait(false)) { result = new { status = "HoldBlocked" }; break; }
                result = step.Kind == ProtectionQualificationNativeCallKind.RegisterIfDispatched ? await protection.RegisterAsync(conditional, token).ConfigureAwait(false)
                    : await protection.ReserveAndConsumeAsync(conditional, token).ConfigureAwait(false); break;
            case ProtectionQualificationNativeCallKind.Register: result = await protection.RegisterAsync(Read<DeletionBatchConsumptionRequest>(), token).ConfigureAwait(false); break;
            case ProtectionQualificationNativeCallKind.Reserve: result = await protection.ReserveAndConsumeAsync(Read<DeletionBatchConsumptionRequest>(), token).ConfigureAwait(false); break;
            case ProtectionQualificationNativeCallKind.RejectSubstitutions:
                var denied = new JsonArray(); bool accepted = false;
                foreach (var variant in ProtectionQualificationSubstitutions.Create(request["action"]!.GetValue<string>(), Read<DeletionBatchConsumptionRequest>(), request["otherTenantReference"]!.GetValue<string>()))
                {
                    JsonNode? outcome;
                    try
                    {
                        var nativeOutcome = await protection.ReserveAndConsumeAsync(variant.Request, token).ConfigureAwait(false);
                        accepted |= nativeOutcome.Status is DeletionConsumptionStatus.ConsumptionReserved or DeletionConsumptionStatus.Consumed or DeletionConsumptionStatus.AlreadyDestroyedByBatch;
                        outcome = Result(nativeOutcome);
                    }
                    catch (ArgumentException) { outcome = new JsonObject { ["status"] = "Denied", ["validation"] = "ArgumentRejected" }; }
                    denied.Add(new JsonObject { ["mutation"] = variant.Mutation, ["outcome"] = outcome });
                }
                result = new JsonObject { ["status"] = accepted ? "UnexpectedAccepted" : "Denied", ["outcomes"] = denied }; break;
            case ProtectionQualificationNativeCallKind.RejectForeignRoot:
                var root = Read<CustodyKeyRegistration>();
                string other = request["otherTenantReference"]!.GetValue<string>();
                if (root.Identity.TenantId != tenant || other == tenant || !request["targets"]!.Deserialize<ProtectionTarget[]>()!.Any(target => ErasureRootCustody.TargetId(target) == ErasureRootCustody.TargetId(root.Identity)))
                { throw new InvalidOperationException("Exact original same-alias tenant-isolation fixture required."); }
                var foreign = await custody.WrapAsync(root.Identity with { TenantId = other }, request["operationId"]!.GetValue<string>(), 0, token).ConfigureAwait(false);
                result = new { status = foreign is null ? "Denied" : "UnexpectedAccepted" }; break;
            case ProtectionQualificationNativeCallKind.Block: result = await consumption.BlockAsync(Read<DeletionBatchBlockRequest>()).WaitAsync(token).ConfigureAwait(false); break;
            case ProtectionQualificationNativeCallKind.Activate: result = await protection.ActivateAsync(Read<DeletionReattestationActivation>(), token).ConfigureAwait(false); break;
            case ProtectionQualificationNativeCallKind.ReconcileBlockedReplacement:
                var reconciliation = Read<DeletionBlockedReplacementReconciliation>();
                if (request["caseId"]!.GetValue<string>() == "P30")
                {
                    if (reconciliation.UnissuedProof is not null) { throw new InvalidOperationException("Rejected issue evidence must come from the actual private guard port."); }
                    var noIssue = await guard.ReadRejectedIssueAsync(reconciliation.Capability, reconciliation.SigningRequestId,
                        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(reconciliation.DetachedJws))), token).ConfigureAwait(false)
                        ?? throw new InvalidOperationException("Exact independently retained rejected-issue original unavailable.");
                    reconciliation = reconciliation with { UnissuedProof = noIssue, CommittedIssuedGuardRevision = 0, GuardReplacementReceiptId = noIssue.Refusal.ReceiptId };
                }
                result = await protection.ReconcileBlockedReplacementAsync(reconciliation, token).ConfigureAwait(false); break;
            case ProtectionQualificationNativeCallKind.Sign: result = await signer.SignAsync(Read<DeletionBatchCapabilityV1>()).WaitAsync(token).ConfigureAwait(false); break;
            case ProtectionQualificationNativeCallKind.ObsoleteUnissued: result = await signer.ObsoleteUnissuedAsync(Read<DeletionBatchCapabilityV1>()).WaitAsync(token).ConfigureAwait(false); break;
            case ProtectionQualificationNativeCallKind.Compromise:
                var envelope = step.Payload["envelope"]!.Deserialize<DeletionCapabilityRevocationEnvelope>()!;
                if (!await compromise.ReceiveAsync(envelope, step.Payload["signedEvidence"]!.GetValue<string>(), token).ConfigureAwait(false))
                { throw new InvalidOperationException("Exact original compromise or guard mirror remains unresolved."); }
                result = new { status = "Completed", receipt = await consumption.LookupRevocationAsync(envelope).WaitAsync(token).ConfigureAwait(false) }; break;
            case ProtectionQualificationNativeCallKind.Lookup:
                result = await protection.LookupAsync(step.Payload["tenantId"]!.GetValue<string>(), step.Payload["batchId"]!.GetValue<string>(), token).ConfigureAwait(false); break;
            case ProtectionQualificationNativeCallKind.LookupBlock:
                result = (await consumption.ReadBlockOriginalAsync(Read<DeletionBatchBlockRequest>()).WaitAsync(token).ConfigureAwait(false))?.Outcome
                    ?? throw new InvalidOperationException("Exact production block original unavailable."); break;
            case ProtectionQualificationNativeCallKind.LookupActivation:
                result = (await consumption.ReadActivationOriginalAsync(Read<DeletionReattestationActivation>()).WaitAsync(token).ConfigureAwait(false))?.Outcome
                    ?? throw new InvalidOperationException("Exact production activation original unavailable."); break;
            case ProtectionQualificationNativeCallKind.LookupProvider:
                result = await custody.LookupAsync(step.Payload["request"]!.Deserialize<DeletionBatchConsumptionRequest>()!, step.Payload["reservationReceiptId"]!.GetValue<string>(), token).ConfigureAwait(false); break;
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
                await faults.ArmAsync(step.Payload["stepId"]!.GetValue<string>(), step.Payload["boundary"]!.GetValue<string>(), request["operationId"]!.GetValue<string>(), token).ConfigureAwait(false);
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
        bool negative = request["action"]!.GetValue<string>().StartsWith("Reject", StringComparison.Ordinal);
        if (!negative && result is DeletionConsumptionOutcome { Status: DeletionConsumptionStatus.Unavailable })
        { throw new InvalidOperationException("Original native owner result unresolved; read-only original recovery required."); }
        var captured = Result(result);
        if (request["action"]!.GetValue<string>() == "RejectStaleRevokedDispatch" && result is DeletionConsumptionOutcome { Status: DeletionConsumptionStatus.ConsumptionBlocked } && captured is JsonObject stale)
        { stale["status"] = "Denied"; }
        if (negative && step.Kind == ProtectionQualificationNativeCallKind.Reserve && result is DeletionConsumptionOutcome { Status: DeletionConsumptionStatus.Unavailable or DeletionConsumptionStatus.ConsumptionReserved }
            && captured is JsonObject refusedManifest)
        {
            var proof = await custody.CapturePinnedManifestRefusalAsync(Read<DeletionBatchConsumptionRequest>(), request["operationId"]!.GetValue<string>() + ":" + step.Id, token).ConfigureAwait(false);
            if (proof is not null) { refusedManifest["status"] = "HoldPinned"; refusedManifest["pinRefusals"] = JsonSerializer.SerializeToNode(proof); }
            else if (await IsHoldPreventedAsync(Read<DeletionBatchConsumptionRequest>(), token).ConfigureAwait(false)) { refusedManifest["status"] = "HoldBlocked"; }
        }
        if (negative && result is DeletionConsumptionOutcome { Status: DeletionConsumptionStatus.ConsumptionReserved } && captured is JsonObject pendingResult && pendingResult["status"]?.GetValue<string>() != "HoldPinned")
        { pendingResult["status"] = "ProviderUnavailable"; }
        if (effectBefore is not null && captured is JsonObject audit)
        { audit["effectBefore"] = effectBefore; audit["effectAfter"] = await EffectAsync(tenant, token).ConfigureAwait(false); }
        return captured;
    }
    private static JsonNode? Result(object? result)
    {
        var node = JsonSerializer.SerializeToNode(result);
        if (node is JsonObject outcome)
        {
            string? status = result switch
            {
                DeletionConsumptionOutcome { Status: DeletionConsumptionStatus.Unavailable } => "ProviderUnavailable",
                DeletionConsumptionOutcome actual => actual.Status.ToString(),
                CustodyKeyLifecycleOutcome { Status: CustodyKeyLifecycleStatus.BlockedByPin } => "HoldPinned",
                CustodyKeyLifecycleOutcome actual => actual.Status.ToString(),
                DeletionCapabilitySigningOutcome actual => actual.State.ToString(),
                _ => null,
            };
            if (status is not null) { outcome["status"] = status; }
        }
        return node;
    }
    private async Task<string> EffectAsync(string tenant, CancellationToken token)
    {
        var read = await custody.ReadAsync(tenant, token).ConfigureAwait(false);
        if (read is not { } head) { throw new InvalidOperationException("Independent current native effect boundary unavailable."); }
        return ErasureRootCustody.Hash(new { head.Head.Epoch, Targets = head.Head.Targets.ToDictionary(entry => entry.Key,
            entry => new { entry.Value.Registration, entry.Value.Pins }, StringComparer.Ordinal), head.Head.Erased, head.Head.Occurrences });
    }
    private async Task<JsonObject> MakeOwnersEffectiveAsync(JsonObject input, CancellationToken token)
    {
        var template = input["transition"]!.Deserialize<GovernanceGuardTransition>()!;
        if (template.Operation != GovernanceGuardOperation.RecordOwnerCycleEffective) { throw new InvalidOperationException("Source-owned owner-cycle operation required."); }
        var state = await guard.ReadAsync(template.TenantId, token).ConfigureAwait(false) ?? throw new InvalidOperationException("Current native owner census unavailable.");
        var deletion = state.Deletions.Single(value => value.RequestId == template.DeletionRequestId);
        var owners = input["ownerIds"]!.Deserialize<string[]>()!;
        if (deletion.Ordinal != input["expectedOrdinal"]!.GetValue<long>() || GovernanceScopeGuardReducer.ObligationDigest(deletion.ObligationIds) != input["obligationDigest"]!.GetValue<string>())
        { throw new InvalidOperationException("Admitted native owner ordinal or obligations changed."); }
        if (owners.Length is < 1 or > 1000 || owners.Distinct(StringComparer.Ordinal).Count() != owners.Length) { throw new InvalidOperationException("Complete finite native owner census required."); }
        var outcomes = new JsonArray();
        for (int index = 0; index < owners.Length; index++)
        {
            string owner = owners[index];
            state = await guard.ReadAsync(template.TenantId, token).ConfigureAwait(false) ?? throw new InvalidOperationException("Native owner-cycle compare unavailable.");
            var current = state.Deletions.Single(value => value.RequestId == template.DeletionRequestId);
            if (!current.RequiredOwnerIds.Order(StringComparer.Ordinal).SequenceEqual(owners) || current.Ordinal != deletion.Ordinal) { throw new InvalidOperationException("Captured native owner census changed."); }
            var command = OwnerCycleCommand(input, owner, index);
            var actual = await guard.ExecuteAsync(command, [], token).ConfigureAwait(false);
            if (actual?.Status != "Committed") { throw new InvalidOperationException("Actual owner Effective evidence unavailable."); }
            outcomes.Add(JsonSerializer.SerializeToNode(actual));
        }
        return new JsonObject { ["status"] = "Completed", ["outcomes"] = outcomes };
    }
    private static GovernanceGuardTransition OwnerCycleCommand(JsonObject input, string owner, int index)
    {
        var template = input["transition"]!.Deserialize<GovernanceGuardTransition>()!;
        var ordinal = new GovernanceOrdinalCommand(input["expectedOrdinal"]!.GetValue<long>(), "", "", owner, input["obligationDigest"]!.GetValue<string>(), "", "", 0, 0, "", [], "");
        return template with { OperationId = template.OperationId + ":" + ErasureRootCustody.Hash(owner), ExpectedGuardRevision = checked(template.ExpectedGuardRevision + index), Ordinal = ordinal };
    }
    private async Task<bool> IsHoldPreventedAsync(DeletionBatchConsumptionRequest request, CancellationToken token)
    {
        var current = await guard.ReadAsync(request.Capability.TenantId, token).ConfigureAwait(false);
        if (current is null) { return false; }
        var deletion = current.Deletions.SingleOrDefault(value => value.RequestId == request.Capability.DeletionRequestId);
        var batch = deletion?.Batches.SingleOrDefault(value => value.BatchId == request.Capability.BatchId);
        return deletion is not null && batch?.Capability == request.Capability && batch.DetachedJws == request.DetachedJws && batch.DispatchReceiptId.Length == 0
            && current.Holds.Any(hold => hold.Active && ErasureRootCustody.Hash(hold.Scope) == ErasureRootCustody.Hash(deletion.Scope));
    }

    private async Task<PayloadProtectionWriteResult> SealAsync(ProtectionQualificationNativeSeal input, JsonObject request, string? changedCanary, CancellationToken token)
    {
        Check(input.Source);
        var binding = input.Enrollment.Binding;
        if (input.Enrollment.Writer is not { } writer || writer.Component != input.Source.Component
            || !await source.RegisterOriginalAsync(input.Enrollment, token).ConfigureAwait(false)) { throw new InvalidOperationException("Independent native source original unavailable."); }
        byte[] canary = Convert.FromBase64String(changedCanary ?? request["canaryBase64"]?.GetValue<string>() ?? await ReadSeedAsync(input.SeedSource, token).ConfigureAwait(false));
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
        string canary = request["canaryBase64"]?.GetValue<string>() ?? await ReadSeedAsync(input.SeedSource, token).ConfigureAwait(false);
        var state = JsonSerializer.SerializeToElement(new { Entries = targets.Select(target => new { ProtectedContent = new { Target = target,
            Value = new { CanaryBase64 = canary } } }).ToArray() });
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
    private async Task<JsonArray> ReplaySnapshotAsync(ProtectionQualificationNativeSnapshotRead input, CancellationToken token)
    {
        Check(input.Source);
        var snapshot = await native.GetStateAsync<SnapshotProtectionResult>(input.Source.Component, input.Source.Key, ConsistencyMode.Strong, input.Source.Metadata, token).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Exact original sealed snapshot unavailable.");
        var type = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() }.GetTypeInfo(typeof(JsonElement));
        var trusted = await engine.TryReplaySnapshotAsync(input.Owner, snapshot.State, snapshot.Metadata, type, input.Parent, token).ConfigureAwait(false);
        var external = await engine.TryUnprotectSnapshotAsync(input.Owner, snapshot.State, snapshot.Metadata, type, input.Parent, token).ConfigureAwait(false);
        if (!trusted.IsReadable || trusted.State is not JsonElement restored) { throw new InvalidOperationException("Actual trusted snapshot replay unavailable."); }
        var projected = new JsonArray();
        void Visit(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                if (value.TryGetProperty("ProtectedContent", out var content) && content.ValueKind == JsonValueKind.Object
                    && content.TryGetProperty("Target", out var target) && content.TryGetProperty("Value", out var field))
                {
                    JsonNode? erased = field.ValueKind == JsonValueKind.Object && field.EnumerateObject().Count() == 1
                        && field.TryGetProperty("State", out var state) && state.GetString() == "Erased" ? JsonNode.Parse(field.GetRawText()) : null;
                    projected.Add(new JsonObject { ["target"] = JsonNode.Parse(target.GetRawText()), ["field"] = erased, ["externalOutcome"] = external.IsReadable ? "Readable" : "Unreadable" });
                    return;
                }
                foreach (var property in value.EnumerateObject()) { Visit(property.Value); }
            }
            else if (value.ValueKind == JsonValueKind.Array) { foreach (var child in value.EnumerateArray()) { Visit(child); } }
        }
        Visit(restored);
        if (projected.Count == 0) { throw new InvalidOperationException("Actual typed per-entry snapshot replay required."); }
        await native.SaveStateAsync(input.Source.Component, input.Source.Key + ":replay", projected, metadata: input.Source.Metadata, cancellationToken: token).ConfigureAwait(false);
        return projected;
    }
    private async Task<string> ReadSeedAsync(ProtectionQualificationNativeCell? cell, CancellationToken token)
    {
        if (cell is null) { throw new InvalidOperationException("Explicit sealed native seed required for a content-free backup request."); }
        var sourceEvent = await ReadEventAsync(cell, token).ConfigureAwait(false);
        var opened = await engine.TryUnprotectEventPayloadAsync(sourceEvent.Binding.Identity.Owner, sourceEvent.Binding.Identity.PayloadTypeId,
            sourceEvent.Protected.PayloadBytes, sourceEvent.Protected.SerializationFormat, sourceEvent.Protected.Metadata,
            ErasureRootOccurrenceBindings.Occurrence(sourceEvent.Binding.Identity), token).ConfigureAwait(false);
        if (!opened.IsReadable || opened.PayloadBytes is null) { throw new InvalidOperationException("Actual sealed seed unreadable."); }
        try { using var clear = JsonDocument.Parse(opened.PayloadBytes); return clear.RootElement.GetProperty("ProtectedContent").GetProperty("Value").GetProperty("CanaryBase64").GetString()!; }
        finally { CryptographicOperations.ZeroMemory(opened.PayloadBytes); }
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
