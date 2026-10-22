using Hexalith.Platform.Secrets.Runtime;
namespace Hexalith.Platform.Custody.Dapr;

internal sealed record WorkerOperation(string Operation, string OriginalOperationId, string IntentId, string SourceEpoch, string DestinationEpoch, string TargetReference);
internal sealed record NativeCompletion(string IntentId, string ProducerIncarnation, string ResultSha256,
    string IoCompletionEvidenceDigest, string BufferCompletionEvidenceDigest, string HolderClosureEvidenceDigest);
internal sealed record PreparationEvidence(PreparationClaim Claim, ClosureInventory Inventory);
internal sealed record PublicationEvidence(FinalClaim Claim, ClosureInventory Inventory);
internal sealed record CopyResult(string OpaqueReference, string DestinationEpoch, byte[] EncryptedOuterKey);

// Separately invoked durable worker. Caller duplicate/lookup never enters these methods.
// Every native send has a permanently committed SendCommitted intent before I/O.
internal sealed class RecoverableLifecycle(ComponentProfile profile, IAuthorityStore store, IEpochOperations epochs, StatementVerifier verifier)
{
    private void Authorize(CurrentState current, WorkerOperation operation, SignedStatement permission)
    {
        if (!verifier.Qualified(current) || !verifier.Verify(permission, "private-operation", current,
            CustodyJson.Hash(CustodyJson.Bytes(operation)), TimeSpan.FromSeconds(30))
            || current.Checkpoint.Cycle?.OperationId != operation.OriginalOperationId)
            throw new AuthorityUnavailableException("Exact current worker permission or qualified installation missing.");
    }
    internal async Task SelectSuccessorAsync(string tenant, WorkerOperation operation, SignedStatement permission, CancellationToken token)
    {
        var current = await store.ReadAsync(tenant, token); Authorize(current, operation, permission);
        var cycle = current.Checkpoint.Cycle!;
        if (operation.Operation != "select-successor" || cycle.Phase != CyclePhase.Reserved || cycle.SuccessorEpoch is not null
            || operation.SourceEpoch != cycle.SourceEpoch || operation.DestinationEpoch == cycle.SourceEpoch
            || !profile.Epochs.Any(e => e.OpaqueId == operation.DestinationEpoch && e.TenantId == tenant)
            || current.Checkpoint.Registrations.Values.Any(r => r.Epoch == operation.DestinationEpoch))
            throw new AuthorityUnavailableException("Successor must be a separately enrolled unused generation.");
        var next = SemanticProvider.Clone(current) with { Cycle = cycle with { SuccessorEpoch = operation.DestinationEpoch } };
        await store.CommitAsync(current, next, token);
    }
    internal async Task CopySurvivorAsync(string tenant, WorkerOperation operation, SignedStatement permission, CancellationToken token)
    {
        var current = await store.ReadAsync(tenant, token); Authorize(current, operation, permission);
        var cycle = current.Checkpoint.Cycle!;
        if (current.Checkpoint.NativeIntents.ContainsKey(operation.IntentId)) return; // Even Prepared is explicit recovery only.
        if (operation.Operation != "copy-survivor" || cycle.Phase != CyclePhase.Reserved || cycle.SuccessorEpoch != operation.DestinationEpoch
            || cycle.SourceEpoch != operation.SourceEpoch || !current.Checkpoint.Registrations.TryGetValue(operation.TargetReference, out var registration)
            || registration.Epoch != cycle.SourceEpoch || IsTarget(cycle, registration))
            throw new AuthorityUnavailableException("Only non-targets may move to the exact successor.");
        var intent = new NativeIntentV1(tenant, operation.OriginalOperationId, operation.IntentId,
            CustodyJson.Hash(CustodyJson.Bytes(operation)), permission.SignerId, cycle.SourceEpoch, cycle.SuccessorEpoch!,
            cycle.Manifest.Envelope.Proof.TargetVectorDigest, current.Head.Revision, CustodyBindingIntentPhase.SendCommitted);
        var next = SemanticProvider.Clone(current); next.NativeIntents.Add(intent.IntentId, intent);
        var sent = await store.CommitAsync(current, next, token);
        // Any exception/cancellation after this point leaves a permanent send-unknown. No automatic resend.
        byte[] ciphertext = await epochs.ReencryptOuterKeyAsync(cycle.SourceEpoch, cycle.SuccessorEpoch!, tenant, registration.EncryptedOuterKey, token);
        var result = new CopyResult(operation.TargetReference, cycle.SuccessorEpoch!, ciphertext);
        var retained = await store.PutImmutableAsync(CustodyJson.Bytes(result), "journal", token);
        var durable = SemanticProvider.Clone(sent);
        durable.NativeResults.Add(intent.IntentId, retained);
        durable.NativeIntents[intent.IntentId] = intent with { Phase = CustodyBindingIntentPhase.ResultDurable };
        durable.Registrations[operation.TargetReference] = registration with { EncryptedOuterKey = ciphertext, Epoch = cycle.SuccessorEpoch! };
        await store.CommitAsync(sent, durable, token); // Stale workers cannot overwrite newer holds or native results.
    }
    internal async Task CloseNativeIntentAsync(string tenant, SignedStatement evidence, CancellationToken token)
    {
        var current = await store.ReadAsync(tenant, token);
        var completion = CustodyJson.Read<NativeCompletion>(CustodyJson.Bytes(evidence.Payload));
        if (!verifier.Verify(evidence, "independent-observer", current, CustodyJson.Hash(CustodyJson.Bytes(completion)), TimeSpan.FromSeconds(30))
            || !current.Checkpoint.NativeIntents.TryGetValue(completion.IntentId, out var intent)
            || intent.Phase != CustodyBindingIntentPhase.ResultDurable || intent.ProducerIncarnation != completion.ProducerIncarnation
            || !current.Checkpoint.NativeResults.TryGetValue(intent.IntentId, out var result) || result.Sha256 != completion.ResultSha256
            || !new[] { completion.IoCompletionEvidenceDigest, completion.BufferCompletionEvidenceDigest, completion.HolderClosureEvidenceDigest }.All(CustodyBindingContract.Digest))
            throw new AuthorityUnavailableException("Independent native completion evidence missing.");
        _ = await store.ReadImmutableAsync(result, token);
        var next = SemanticProvider.Clone(current); next.NativeIntents[intent.IntentId] = intent with { Phase = CustodyBindingIntentPhase.Closed };
        await store.CommitAsync(current, next, token);
    }
    internal async Task ArmAsync(string tenant, SignedStatement evidence, CancellationToken token)
    {
        var current = await store.ReadAsync(tenant, token); var cycle = current.Checkpoint.Cycle;
        var preparation = CustodyJson.Read<PreparationEvidence>(CustodyJson.Bytes(evidence.Payload)); var p = preparation.Claim;
        if (!verifier.Qualified(current) || !verifier.Verify(evidence, "independent-observer", current,
                CustodyJson.Hash(CustodyJson.Bytes(preparation)), TimeSpan.FromSeconds(30))
            || cycle is null || cycle.Phase != CyclePhase.Reserved || cycle.SuccessorEpoch is null
            || p.SourceEpoch != cycle.SourceEpoch || p.SuccessorEpoch != cycle.SuccessorEpoch
            || p.ManifestDigest != cycle.Manifest.Manifest!.Original.Capability.ManifestDigest
            || p.InventoryDigest != CustodyJson.Hash(CustodyJson.Bytes(preparation.Inventory))
            || !ClosureRules.Complete(preparation.Inventory, current.Checkpoint, final: false)
            || !new[] { p.SurvivorCheckpointDigest, p.ProducerClosureDigest, p.HolderClosureDigest, p.NativeIntegrityEvidenceDigest, p.RestoreEvidenceDigest }.All(CustodyBindingContract.Digest)
            || !p.PermanentlyDeletedAlternateEpochs.Order(StringComparer.Ordinal).SequenceEqual(cycle.AlternateEpochs.Concat(cycle.TaintedEpochs).Distinct().Order(StringComparer.Ordinal)))
            throw new AuthorityUnavailableException("Complete independent preparation/retirement proof missing.");
        var targets = current.Checkpoint.Registrations.Values.Where(r => IsTarget(cycle, r)).ToArray();
        var survivors = current.Checkpoint.Registrations.Values.Where(r => !IsTarget(cycle, r)).ToArray();
        if (targets.Length != cycle.Manifest.Manifest.Original.Targets.Count || targets.Any(r => r.Epoch != cycle.SourceEpoch)
            || survivors.Any(r => r.Epoch != cycle.SuccessorEpoch)
            || targets.Any(r => current.Checkpoint.Pins.Values.Any(pin => pin.Target == CustodyJson.Target(r.Registration.Identity)))
            || !p.TargetOpaqueReferences.Order(StringComparer.Ordinal).SequenceEqual(targets.Select(r => r.Registration.OpaqueWrappedKeyReference).Order(StringComparer.Ordinal))
            || !p.SurvivorOpaqueReferences.Order(StringComparer.Ordinal).SequenceEqual(survivors.Select(r => r.Registration.OpaqueWrappedKeyReference).Order(StringComparer.Ordinal))
            || p.SurvivorCheckpointDigest != CustodyJson.Hash(CustodyJson.Bytes(survivors.OrderBy(r => r.Registration.OpaqueWrappedKeyReference, StringComparer.Ordinal).ToArray())))
            throw new AuthorityUnavailableException("Survivor or target lineage mismatch.");
        // One CAS both adopts the successor and releases the whole-tenant preparation fence.
        var next = SemanticProvider.Clone(current) with { CurrentEpoch = cycle.SuccessorEpoch,
            Cycle = cycle with { Phase = CyclePhase.Armed, PreparationDigest = CustodyJson.Hash(CustodyJson.Bytes(preparation)) } };
        await store.CommitAsync(current, next, token);
    }
    internal async Task ScheduleCutAsync(string tenant, WorkerOperation operation, SignedStatement permission, CancellationToken token)
    {
        var current = await store.ReadAsync(tenant, token); Authorize(current, operation, permission);
        var cycle = current.Checkpoint.Cycle!;
        if (current.Checkpoint.NativeIntents.ContainsKey(operation.IntentId)) return;
        if (operation.Operation != "schedule-cut" || cycle.Phase != CyclePhase.Armed || cycle.SourceEpoch != operation.SourceEpoch
            || cycle.SuccessorEpoch != operation.DestinationEpoch || cycle.PreparationDigest is null)
            throw new AuthorityUnavailableException("Only one armed source cut is admissible.");
        var intent = new NativeIntentV1(tenant, operation.OriginalOperationId, operation.IntentId, CustodyJson.Hash(CustodyJson.Bytes(operation)),
            permission.SignerId, cycle.SourceEpoch, cycle.SuccessorEpoch!, cycle.Manifest.Envelope.Proof.TargetVectorDigest,
            current.Head.Revision, CustodyBindingIntentPhase.SendCommitted);
        var next = SemanticProvider.Clone(current) with { Cycle = cycle with { Phase = CyclePhase.SendCommitted, CutIntentId = intent.IntentId } };
        next.NativeIntents.Add(intent.IntentId, intent);
        var sent = await store.CommitAsync(current, next, token);
        await epochs.ScheduleSourceCutAsync(cycle.SourceEpoch, tenant, token);
        var retained = await store.PutImmutableAsync(CustodyJson.Bytes(new { intent.IntentId, Status = "ScheduleAcknowledgedNotErased" }), "journal", token);
        var durable = SemanticProvider.Clone(sent) with { Cycle = next.Cycle! with { Phase = CyclePhase.AwaitingFinalEvidence } };
        durable.NativeResults.Add(intent.IntentId, retained);
        durable.NativeIntents[intent.IntentId] = intent with { Phase = CustodyBindingIntentPhase.ResultDurable };
        await store.CommitAsync(sent, durable, token);
    }
    internal async Task PublishAsync(string tenant, SignedStatement evidence, byte[] exactOriginal, CancellationToken token)
    {
        var current = await store.ReadAsync(tenant, token); var cycle = current.Checkpoint.Cycle;
        var publication = CustodyJson.Read<PublicationEvidence>(CustodyJson.Bytes(evidence.Payload)); var p = publication.Claim;
        if (!verifier.Qualified(current) || !verifier.Verify(evidence, "independent-observer", current,
                CustodyJson.Hash(CustodyJson.Bytes(publication)), TimeSpan.FromSeconds(30))
            || cycle is null || cycle.Phase != CyclePhase.AwaitingFinalEvidence
            || p.ManifestDigest != cycle.Manifest.Manifest!.Original.Capability.ManifestDigest || p.SourceEpoch != cycle.SourceEpoch
            || p.SuccessorEpoch != cycle.SuccessorEpoch || p.PreparationDigest != cycle.PreparationDigest
            || p.OriginalSha256 != CustodyJson.Hash(exactOriginal) || !CustodyBindingContract.Text(p.RestoreBarrierReceiptId)
            || !new[] { p.HolderClosureDigest, p.SurvivorRecoveryDigest, p.NativeFinalEvidenceDigest }.All(CustodyBindingContract.Digest)
            || p.InventoryDigest != CustodyJson.Hash(CustodyJson.Bytes(publication.Inventory))
            || !ClosureRules.Complete(publication.Inventory, current.Checkpoint, final: true)
            || !p.PermanentlyDeletedEpochs.Order(StringComparer.Ordinal).SequenceEqual(cycle.AlternateEpochs.Concat(cycle.TaintedEpochs).Append(cycle.SourceEpoch).Distinct().Order(StringComparer.Ordinal)))
            throw new AuthorityUnavailableException("Independent final deletion, holder, survivor or restore evidence missing.");
        var original = CustodyJson.Read<CustodyBindingOriginalV1>(exactOriginal); var manifest = cycle.Manifest.Manifest;
        if (original.Result.State != Hexalith.EventStore.Contracts.Security.DeletionManifestProviderState.Consumed
            || original.Result.TenantId != tenant || original.Result.BatchId != manifest.Original.Capability.BatchId
            || original.Result.ReservationReceiptId != manifest.ProtectionReservationReceiptId
            || original.RequestDigest != cycle.Manifest.Envelope.RequestDigest || original.ManifestDigest != p.ManifestDigest
            || original.RestoreBarrierReceiptId != p.RestoreBarrierReceiptId || original.Signature.Length == 0
            || original.Result.TargetReceipts.Count != manifest.Original.Targets.Count
            || !original.Result.TargetReceipts.Select(r => r.Target).SequenceEqual(manifest.Original.Targets)
            || original.Result.TargetReceipts.Any(r => r.OriginalBatchId != manifest.Original.Capability.BatchId || !CustodyBindingContract.Text(r.ReceiptId))
            || original.Result.TargetReceipts.Select(r => r.ReceiptId).Distinct().Count() != manifest.Original.Targets.Count)
            throw new AuthorityUnavailableException("Complete exact original vector required.");
        // The independent statement signs the SHA256 of the exact original bytes including its original signature.
        var retained = await store.PutImmutableAsync(exactOriginal, "original", token);
        var next = SemanticProvider.Clone(current) with { Cycle = cycle with { Phase = CyclePhase.Published } };
        next.Originals[cycle.OperationId] = next.Originals[cycle.OperationId] with { Response = retained };
        await store.CommitAsync(current, next, token); // Merge into current state, never restore the B1 checkpoint.
    }
    private static bool IsTarget(Cycle cycle, StoredRegistration registration) => cycle.Manifest.Manifest!.Original.Targets.Any(t =>
        t.AgentInteractionId == registration.Registration.Identity.ObjectId && t.TargetProtectionKeyAlias == registration.Registration.Identity.KeyAlias);
}
