using Hexalith.Platform.Secrets.Runtime;
namespace Hexalith.Platform.Custody.Dapr;

internal enum CopyClass { Source, Target, Snapshot, Cache, Workflow, Broker, Export, Partial, Backup, DatabaseLog,
    ObjectVersion, KmsGeneration, SdkBuffer, SidecarBuffer, UdsBuffer, GrpcBuffer, TlsBuffer, ComponentBuffer, HostMemory, Diagnostics }
internal enum ClosureDisposition { VerifiedZeroed, IndependentlyTerminated, GatedCiphertext, PermanentlyDeleted, CryptographicallyErased }
internal sealed record CopyEntry(string CopyId, CopyClass Class, string OwnerId, string LocationId, string ProducerIncarnation,
    string? ParentCopyId, string Epoch, string TargetReference, string OutcomeOriginalDigest, string ClosureEvidenceDigest,
    ClosureDisposition Disposition);
internal sealed record SealedQueue(string QueueId, string ProducerIncarnation, long LastAdmittedSequence, long LastCompletedSequence,
    string SealEvidenceDigest, string TerminationEvidenceDigest);
internal sealed record ClassCoverage(CopyClass Class, string InventoryEvidenceDigest, string[] CopyIds);
internal sealed record ClosureInventory(string ManifestDigest, string SourceEpoch, string SuccessorEpoch, CopyEntry[] Copies,
    SealedQueue[] Queues, ClassCoverage[] Coverage, string[] ProducerIncarnations, string[] NativeIntentIds);

internal static class ClosureRules
{
    internal static async Task<bool> KnownHoldersAsync(IAuthorityStore store, ClosureInventory inventory, Checkpoint state, CancellationToken token)
    {
        foreach (var retained in state.NativeResults.Where(pair => pair.Key.EndsWith(":admission", StringComparison.Ordinal)))
        {
            var statement = CustodyJson.Read<SignedStatement>(await store.ReadImmutableAsync(retained.Value, token));
            var admission = CustodyJson.Read<KeyAdmission>(CustodyJson.Bytes(statement.Payload));
            foreach (var holder in admission.Holders)
            {
                CopyClass kind = holder.Role switch { "caller" => CopyClass.HostMemory, "sdk" => CopyClass.SdkBuffer,
                    "sidecar" => CopyClass.SidecarBuffer, "uds" => CopyClass.UdsBuffer, "grpc" => CopyClass.GrpcBuffer,
                    "tls" => CopyClass.TlsBuffer, _ => CopyClass.ComponentBuffer };
                if (!inventory.Copies.Any(copy => copy.CopyId == holder.CopyId && copy.Class == kind && copy.OwnerId == holder.OwnerId
                    && copy.ProducerIncarnation == holder.ProcessIncarnation && copy.TargetReference == admission.KeyUse.OriginalRootReference
                    && copy.OutcomeOriginalDigest == holder.AdmissionOriginalDigest
                    && copy.Disposition is ClosureDisposition.VerifiedZeroed or ClosureDisposition.IndependentlyTerminated)) return false;
            }
        }
        return true;
    }
    internal static bool Complete(ClosureInventory inventory, Checkpoint state, bool final)
    {
        var cycle = state.Cycle;
        if (cycle is null || inventory.ManifestDigest != cycle.Manifest.Manifest!.Original.Capability.ManifestDigest
            || inventory.SourceEpoch != cycle.SourceEpoch || inventory.SuccessorEpoch != cycle.SuccessorEpoch
            || inventory.Coverage.Length != Enum.GetValues<CopyClass>().Length
            || inventory.Coverage.Select(c => c.Class).Distinct().Count() != inventory.Coverage.Length
            || inventory.Copies.Select(c => c.CopyId).Distinct(StringComparer.Ordinal).Count() != inventory.Copies.Length
            || inventory.ProducerIncarnations.Length == 0 || inventory.Queues.Length == 0
            || !inventory.NativeIntentIds.Order(StringComparer.Ordinal).SequenceEqual(state.NativeIntents.Keys.Order(StringComparer.Ordinal))) return false;
        if (state.Deliveries.Values.Any(d => d.Phase != CustodyBindingIntentPhase.Closed)
            || state.NativeIntents.Values.Any(i => i.Phase != CustodyBindingIntentPhase.Closed)) return false;
        if (state.NativeIntents.Values.Any(i => !CustodyBindingContract.Text(i.ProducerIncarnation)
            || !inventory.ProducerIncarnations.Contains(i.ProducerIncarnation, StringComparer.Ordinal))) return false;
        // An independently signed inventory cannot contradict material already known to this checkpoint.
        foreach (var registration in state.Registrations.Values)
        {
            if (!state.Originals.TryGetValue(registration.Registration.OperationId, out var original) || original.Response is null
                || !inventory.Copies.Any(c => c.Class == CopyClass.ObjectVersion && c.TargetReference == registration.Registration.OpaqueWrappedKeyReference
                    && c.Epoch == registration.Epoch && c.OutcomeOriginalDigest == original.Response.Sha256
                    && c.Disposition == (final && registration.Epoch == cycle.SourceEpoch ? ClosureDisposition.CryptographicallyErased : ClosureDisposition.GatedCiphertext))) return false;
        }
        foreach (string epoch in cycle.AlternateEpochs.Concat(cycle.TaintedEpochs).Append(cycle.SourceEpoch).Append(cycle.SuccessorEpoch!).Distinct())
            if (!inventory.Copies.Any(c => c.Class == CopyClass.KmsGeneration && c.Epoch == epoch && c.TargetReference == epoch
                && c.Disposition == (epoch == cycle.SuccessorEpoch || !final && epoch == cycle.SourceEpoch
                    ? ClosureDisposition.GatedCiphertext : ClosureDisposition.PermanentlyDeleted))) return false;
        var ids = inventory.Copies.Select(c => c.CopyId).ToHashSet(StringComparer.Ordinal);
        foreach (var coverage in inventory.Coverage)
            if (!CustodyBindingContract.Digest(coverage.InventoryEvidenceDigest)
                || !coverage.CopyIds.Order(StringComparer.Ordinal).SequenceEqual(inventory.Copies.Where(c => c.Class == coverage.Class).Select(c => c.CopyId).Order(StringComparer.Ordinal))) return false;
        foreach (var copy in inventory.Copies)
        {
            if (!Enum.IsDefined(copy.Class) || !Enum.IsDefined(copy.Disposition)
                || !new[] { copy.CopyId, copy.OwnerId, copy.LocationId, copy.ProducerIncarnation, copy.Epoch, copy.TargetReference }.All(x => CustodyBindingContract.Text(x))
                || !CustodyBindingContract.Digest(copy.OutcomeOriginalDigest) || !CustodyBindingContract.Digest(copy.ClosureEvidenceDigest)
                || !inventory.ProducerIncarnations.Contains(copy.ProducerIncarnation, StringComparer.Ordinal)
                || copy.ParentCopyId is { } parent && (!ids.Contains(parent) || parent == copy.CopyId)) return false;
            var visited = new HashSet<string>(StringComparer.Ordinal) { copy.CopyId }; var ancestor = copy.ParentCopyId;
            while (ancestor is not null)
            {
                if (!visited.Add(ancestor)) return false;
                ancestor = inventory.Copies.Single(c => c.CopyId == ancestor).ParentCopyId;
            }
            bool memory = copy.Class is CopyClass.SdkBuffer or CopyClass.SidecarBuffer or CopyClass.UdsBuffer or CopyClass.GrpcBuffer
                or CopyClass.TlsBuffer or CopyClass.ComponentBuffer or CopyClass.HostMemory or CopyClass.Diagnostics;
            if (memory && copy.Disposition is not (ClosureDisposition.VerifiedZeroed or ClosureDisposition.IndependentlyTerminated)) return false;
            if (copy.Disposition == ClosureDisposition.CryptographicallyErased && (!final || copy.Epoch != cycle.SourceEpoch)) return false;
            if (final && copy.Epoch == cycle.SourceEpoch && copy.Disposition == ClosureDisposition.GatedCiphertext) return false;
            if (cycle.AlternateEpochs.Contains(copy.Epoch) || cycle.TaintedEpochs.Contains(copy.Epoch))
                if (copy.Disposition is not (ClosureDisposition.PermanentlyDeleted or ClosureDisposition.VerifiedZeroed or ClosureDisposition.IndependentlyTerminated)) return false;
        }
        return inventory.ProducerIncarnations.All(p => inventory.Queues.Any(q => q.ProducerIncarnation == p))
            && inventory.Queues.All(q => q.LastAdmittedSequence >= 0 && q.LastCompletedSequence == q.LastAdmittedSequence
                && CustodyBindingContract.Digest(q.SealEvidenceDigest) && CustodyBindingContract.Digest(q.TerminationEvidenceDigest));
    }
}
