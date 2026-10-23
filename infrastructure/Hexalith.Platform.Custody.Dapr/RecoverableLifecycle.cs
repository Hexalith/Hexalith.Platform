using Hexalith.Platform.Secrets.Runtime;
namespace Hexalith.Platform.Custody.Dapr;

internal sealed record WorkerOperation(string Operation, string OriginalOperationId, string IntentId, string SourceEpoch, string DestinationEpoch, string TargetReference);
internal sealed record NativeCompletion(string IntentId, string ProducerIncarnation, string ResultSha256,
    string IoCompletionEvidenceDigest, string BufferCompletionEvidenceDigest, string HolderClosureEvidenceDigest);
internal sealed record PreparationEvidence(PreparationClaim Claim, ClosureInventory Inventory);
internal sealed record PublicationEvidence(FinalClaim Claim, ClosureInventory Inventory);
internal sealed record CopyResult(string OpaqueReference, string DestinationEpoch, byte[] EncryptedOuterKey);
internal sealed record RecoveredNativeResult(string IntentId, string RequestDigest, string Kind, byte[] ExactResult,
    string NativeEvidenceDigest, WorkerOperation OriginalOperation);
internal sealed record AbandonedSuccessor(string Generation, string[] IntentIds, string FinalDeletionEvidenceDigest,
    string ProducerTerminationDigest, string BufferClosureDigest);

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
        if (cycle.TaintedEpochs.Contains(operation.DestinationEpoch)) throw new AuthorityUnavailableException("Tainted generations are permanently excluded from adoption.");
        var next = SemanticProvider.Clone(current) with { Cycle = cycle with { SuccessorEpoch = operation.DestinationEpoch,
            AlternateEpochs = cycle.AlternateEpochs.Where(e => e != operation.DestinationEpoch).ToArray() } };
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
        next.NativeResults.Add(intent.IntentId + ":source", await store.PutImmutableAsync(CustodyJson.Bytes(registration), "journal", token));
        var sent = await store.CommitAsync(current, next, token);
        // Any exception/cancellation after this point leaves a permanent send-unknown. No automatic resend.
        byte[] ciphertext = await epochs.ReencryptOuterKeyAsync(cycle.SourceEpoch, cycle.SuccessorEpoch!, tenant, registration.EncryptedOuterKey, token);
        var result = new CopyResult(operation.TargetReference, cycle.SuccessorEpoch!, ciphertext);
        var retained = await store.PutIntentResultAsync(CustodyJson.Bytes(result), CustodyJson.IntentLocator(tenant, intent.IntentId), token);
        var durable = SemanticProvider.Clone(sent);
        durable.NativeResults.Add(intent.IntentId, retained);
        durable.NativeIntents[intent.IntentId] = intent with { Phase = CustodyBindingIntentPhase.ResultDurable };
        durable.Registrations[operation.TargetReference] = registration with { EncryptedOuterKey = ciphertext, Epoch = cycle.SuccessorEpoch! };
        await store.CommitAsync(sent, durable, token); // Stale workers cannot overwrite newer holds or native results.
    }
    internal async Task RetireAlternateAsync(string tenant, WorkerOperation operation, SignedStatement permission, CancellationToken token)
    {
        var current = await store.ReadAsync(tenant, token); Authorize(current, operation, permission); var cycle = current.Checkpoint.Cycle!;
        if (current.Checkpoint.NativeIntents.ContainsKey(operation.IntentId)) return;
        if (operation.Operation != "retire-alternate" || cycle.Phase != CyclePhase.Reserved
            || operation.SourceEpoch == cycle.SourceEpoch || operation.SourceEpoch == cycle.SuccessorEpoch
            || !cycle.AlternateEpochs.Concat(cycle.TaintedEpochs).Contains(operation.SourceEpoch))
            throw new AuthorityUnavailableException("Only a known noncurrent alternate generation may retire.");
        var intent = new NativeIntentV1(tenant, cycle.OperationId, operation.IntentId, CustodyJson.Hash(CustodyJson.Bytes(operation)),
            permission.SignerId, operation.SourceEpoch, "retired", cycle.Manifest.Envelope.Proof.TargetVectorDigest, current.Head.Revision, CustodyBindingIntentPhase.SendCommitted);
        var next = SemanticProvider.Clone(current); next.NativeIntents.Add(intent.IntentId, intent); var sent = await store.CommitAsync(current, next, token);
        await epochs.ScheduleSourceCutAsync(operation.SourceEpoch, tenant, token);
        var retained = await store.PutIntentResultAsync(CustodyJson.Bytes(new ScheduleResult(intent.IntentId, "ScheduleAcknowledgedNotErased")), CustodyJson.IntentLocator(tenant, intent.IntentId), token);
        var durable = SemanticProvider.Clone(sent); durable.NativeResults.Add(intent.IntentId, retained);
        durable.NativeIntents[intent.IntentId] = intent with { Phase = CustodyBindingIntentPhase.ResultDurable };
        await store.CommitAsync(sent, durable, token);
    }
    internal async Task AbandonSuccessorAsync(string tenant, SignedStatement evidence, CancellationToken token)
    {
        var current = await store.ReadAsync(tenant, token); var cycle = current.Checkpoint.Cycle;
        var abandoned = CustodyJson.Read<AbandonedSuccessor>(CustodyJson.Bytes(evidence.Payload));
        if (!verifier.Qualified(current) || !verifier.Verify(evidence, "independent-observer", current,
                CustodyJson.Hash(CustodyJson.Bytes(abandoned)), TimeSpan.FromSeconds(30))
            || cycle is null || cycle.Phase != CyclePhase.Reserved || cycle.SuccessorEpoch != abandoned.Generation
            || !new[] { abandoned.FinalDeletionEvidenceDigest, abandoned.ProducerTerminationDigest, abandoned.BufferClosureDigest }.All(CustodyBindingContract.Digest))
            throw new AuthorityUnavailableException("Independent permanent retirement and termination required before abandoning a successor.");
        var intents = current.Checkpoint.NativeIntents.Values.Where(i => i.OperationId == cycle.OperationId && i.DestinationGeneration == abandoned.Generation).ToArray();
        if (!abandoned.IntentIds.Order(StringComparer.Ordinal).SequenceEqual(intents.Select(i => i.IntentId).Order(StringComparer.Ordinal)))
            throw new AuthorityUnavailableException("Incomplete tainted producer inventory.");
        var next = SemanticProvider.Clone(current);
        var original = await store.PutImmutableAsync(CustodyJson.Bytes(evidence), "journal", token);
        foreach (var intent in intents)
        {
            if (!next.NativeResults.TryGetValue(intent.IntentId + ":source", out var source)) throw new AuthorityUnavailableException("Original survivor source missing.");
            var registration = CustodyJson.Read<StoredRegistration>(await store.ReadImmutableAsync(source, token));
            if (registration.Epoch != cycle.SourceEpoch || IsTarget(cycle, registration)) throw new AuthorityUnavailableException("Original source substitution.");
            next.Registrations[registration.Registration.OpaqueWrappedKeyReference] = registration;
            next.NativeResults[intent.IntentId + ":abandoned"] = original;
            next.NativeIntents[intent.IntentId] = intent with { Phase = CustodyBindingIntentPhase.Closed };
        }
        next = next with { Cycle = cycle with { SuccessorEpoch = null, TaintedEpochs = cycle.TaintedEpochs.Append(abandoned.Generation).Distinct().ToArray() } };
        await store.CommitAsync(current, next, token);
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
            || !await ClosureRules.KnownHoldersAsync(store, preparation.Inventory, current.Checkpoint, token)
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
        var retained = await store.PutIntentResultAsync(CustodyJson.Bytes(new ScheduleResult(intent.IntentId, "ScheduleAcknowledgedNotErased")), CustodyJson.IntentLocator(tenant, intent.IntentId), token);
        var durable = SemanticProvider.Clone(sent) with { Cycle = next.Cycle! with { Phase = CyclePhase.AwaitingFinalEvidence } };
        durable.NativeResults.Add(intent.IntentId, retained);
        durable.NativeIntents[intent.IntentId] = intent with { Phase = CustodyBindingIntentPhase.ResultDurable };
        await store.CommitAsync(sent, durable, token);
    }
    internal async Task RecoverNativeResultAsync(string tenant, SignedStatement evidence, CancellationToken token)
    {
        var current = await store.ReadAsync(tenant, token); var recovered = CustodyJson.Read<RecoveredNativeResult>(CustodyJson.Bytes(evidence.Payload));
        var cycle = current.Checkpoint.Cycle;
        if (!verifier.Qualified(current) || !verifier.Verify(evidence, "independent-observer", current,
                CustodyJson.Hash(CustodyJson.Bytes(recovered)), TimeSpan.FromSeconds(30))
            || cycle is null || !current.Checkpoint.NativeIntents.TryGetValue(recovered.IntentId, out var intent)
            || intent.OperationId != cycle.OperationId || intent.Phase != CustodyBindingIntentPhase.SendCommitted
            || intent.RequestDigest != recovered.RequestDigest || CustodyJson.Hash(CustodyJson.Bytes(recovered.OriginalOperation)) != intent.RequestDigest
            || recovered.OriginalOperation.IntentId != intent.IntentId || recovered.OriginalOperation.OriginalOperationId != cycle.OperationId
            || recovered.OriginalOperation.Operation != recovered.Kind || !CustodyBindingContract.Digest(recovered.NativeEvidenceDigest))
            throw new AuthorityUnavailableException("Original independent native-result evidence missing.");
        var next = SemanticProvider.Clone(current);
        if (recovered.Kind == "copy-survivor")
        {
            var copy = CustodyJson.Read<CopyResult>(recovered.ExactResult);
            if (cycle.Phase != CyclePhase.Reserved || copy.DestinationEpoch != cycle.SuccessorEpoch
                || copy.EncryptedOuterKey.Length is 0 or > 6144
                || copy.OpaqueReference != recovered.OriginalOperation.TargetReference
                || !next.Registrations.TryGetValue(copy.OpaqueReference, out var registration) || IsTarget(cycle, registration)
                || registration.Epoch != cycle.SourceEpoch || intent.DestinationGeneration != copy.DestinationEpoch)
                throw new AuthorityUnavailableException("Recovered survivor result substitution.");
            next.Registrations[copy.OpaqueReference] = registration with { Epoch = copy.DestinationEpoch, EncryptedOuterKey = copy.EncryptedOuterKey };
        }
        else if (recovered.Kind is "schedule-cut" or "retire-alternate")
        {
            var scheduled = CustodyJson.Read<ScheduleResult>(recovered.ExactResult);
            if ((recovered.Kind == "schedule-cut" && (cycle.CutIntentId != intent.IntentId || cycle.Phase != CyclePhase.SendCommitted)
                    || recovered.Kind == "retire-alternate" && (cycle.Phase != CyclePhase.Reserved || intent.DestinationGeneration != "retired")) || scheduled.IntentId != intent.IntentId
                || scheduled.Status is not ("ScheduleAcknowledgedNotErased" or "ScheduleObservedNotErased"))
                throw new AuthorityUnavailableException("Recovered cut result substitution.");
            if (recovered.Kind == "schedule-cut") next = next with { Cycle = cycle with { Phase = CyclePhase.AwaitingFinalEvidence } };
        }
        else throw new AuthorityUnavailableException("Recovery kind is not a value-free original result.");
        string locator = CustodyJson.IntentLocator(tenant, intent.IntentId);
        var retained = await store.FindIntentResultAsync(locator, token);
        if (retained is not null && !(await store.ReadImmutableAsync(retained, token)).AsSpan().SequenceEqual(recovered.ExactResult))
            throw new AuthorityUnavailableException("Cannot replace original native result.");
        retained ??= await store.PutIntentResultAsync(recovered.ExactResult, locator, token);
        next.NativeResults[intent.IntentId] = retained;
        next.NativeIntents[intent.IntentId] = intent with { Phase = CustodyBindingIntentPhase.ResultDurable };
        await store.CommitAsync(current, next, token);
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
            || !await ClosureRules.KnownHoldersAsync(store, publication.Inventory, current.Checkpoint, token)
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
        using (var signer = System.Security.Cryptography.ECDsa.Create())
        {
            signer.ImportFromPem(profile.ComponentPublicKeyPem);
            if (!signer.VerifyData(CustodyJson.Bytes(original with { Signature = [] }), original.Signature,
                System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.DSASignatureFormat.Rfc3279DerSequence))
                throw new AuthorityUnavailableException("Original signature does not match the independently enrolled public verifier.");
        }
        // The independent statement signs the SHA256 of the exact original bytes including its original signature.
        var retained = await store.PutImmutableAsync(exactOriginal, "original", token);
        var next = SemanticProvider.Clone(current) with { Cycle = cycle with { Phase = CyclePhase.Published } };
        next.Originals[cycle.OperationId] = next.Originals[cycle.OperationId] with { Response = retained };
        await store.CommitAsync(current, next, token); // Merge into current state, never restore the B1 checkpoint.
    }
    private static bool IsTarget(Cycle cycle, StoredRegistration registration) => cycle.Manifest.Manifest!.Original.Targets.Any(t =>
        t.AgentInteractionId == registration.Registration.Identity.ObjectId && t.TargetProtectionKeyAlias == registration.Registration.Identity.KeyAlias);
}
internal sealed record ScheduleResult(string IntentId, string Status);
