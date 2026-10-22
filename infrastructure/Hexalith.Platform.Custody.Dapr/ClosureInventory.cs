using Hexalith.Platform.Secrets.Runtime;
namespace Hexalith.Platform.Custody.Dapr;

internal enum CopyClass { Source, Target, Snapshot, Cache, Workflow, Broker, Export, Partial, Backup, DatabaseLog,
    ObjectVersion, KmsGeneration, SdkBuffer, SidecarBuffer, UdsBuffer, GrpcBuffer, TlsBuffer, ComponentBuffer, HostMemory, Diagnostics }
internal enum ClosureDisposition { VerifiedZeroed, IndependentlyTerminated, GatedCiphertext, PermanentlyDeleted }
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
        if (state.NativeIntents.Values.Any(i => i.Phase != CustodyBindingIntentPhase.Closed)) return false;
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
            if (final && copy.Epoch == cycle.SourceEpoch && copy.Disposition == ClosureDisposition.GatedCiphertext) return false;
            if (cycle.AlternateEpochs.Contains(copy.Epoch) || cycle.TaintedEpochs.Contains(copy.Epoch))
                if (copy.Disposition is not (ClosureDisposition.PermanentlyDeleted or ClosureDisposition.VerifiedZeroed or ClosureDisposition.IndependentlyTerminated)) return false;
        }
        return inventory.ProducerIncarnations.All(p => inventory.Queues.Any(q => q.ProducerIncarnation == p))
            && inventory.Queues.All(q => q.LastAdmittedSequence >= 0 && q.LastCompletedSequence == q.LastAdmittedSequence
                && CustodyBindingContract.Digest(q.SealEvidenceDigest) && CustodyBindingContract.Digest(q.TerminationEvidenceDigest));
    }
}
