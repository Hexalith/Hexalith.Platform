using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Protection;

/// <summary>Exact bounded-depth lifetime indexes, committed only by the same tenant CAS as native destruction and owner ordering.</summary>
public sealed partial class ErasureRootCustody
{
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<ErasureRootHead, ErasureRootOccurrenceIndex> _retainedIndexes = new();
    internal ErasureRootOccurrenceIndex RetainedIndex(ErasureRootHead head)
        => _retainedIndexes.GetValue(head, value => new(store, installation, value.TenantId));
    internal async Task<T?> ReadRetainedAsync<T>(ErasureRootHead head, string collection, SortedDictionary<string, T> values, string id, CancellationToken token) where T : class
    {
        if (values.TryGetValue(id, out var legacy)) { return legacy; }
        var value = await RetainedIndex(head).ReadAsync<T>(Root(head, collection), collection + ":" + id, token).ConfigureAwait(false);
        if (value is not null) { values.Add(id, value); }
        return value;
    }
    private static bool CustodyHistory(string collection) => collection is "Erased" or "ManifestArchives" or "LifecycleOutcomes";
    private static string? Root(ErasureRootHead head, string collection) => collection == "Anchors" ? head.AnchorIndexRoot : CustodyHistory(collection) ? head.CustodyHistoryIndexRoot
        : collection.StartsWith("Qualification", StringComparison.Ordinal) ? head.QualificationIndexRoot : head.LifetimeIndexRoot;
    internal Task<ErasureRootErasure?> ReadErasedAsync(ErasureRootHead head, string id, CancellationToken token)
        => ReadRetainedAsync(head, "Erased", head.Erased, id, token);
    internal Task<CustodyKeyLifecycleOutcome?> ReadLifecycleAsync(ErasureRootHead head, string id, CancellationToken token)
        => ReadRetainedAsync(head, "LifecycleOutcomes", head.LifecycleOutcomes, id, token);
    internal Task<string?> ReadSigningIntentAsync(ErasureRootHead head, string id, CancellationToken token)
        => ReadRetainedAsync(head, "GuardSigningIntents", head.GuardSigningIntents, id, token);
    internal async Task LoadQualificationAsync(ErasureRootHead head, string? binding, CancellationToken token, string? operation = null)
    {
        if (binding is not null)
        {
            _ = await ReadRetainedAsync(head, "QualificationAttempts", head.QualificationAttempts, binding, token).ConfigureAwait(false);
            var index = RetainedIndex(head);
            string? group = await index.ReadAsync<string>(head.QualificationIndexRoot, "qualification-group:" + binding, token).ConfigureAwait(false);
            if (group is not null)
            {
                foreach (var pair in await index.ReadAllAsync<ProtectionQualificationOperation>(group, "operation:", token).ConfigureAwait(false))
                { head.QualificationOperations.TryAdd(pair.Key, pair.Value); }
                foreach (var pair in await index.ReadAllAsync<ProtectionQualificationProducerOriginal>(group, "producer:", token).ConfigureAwait(false))
                { head.QualificationProducerOriginals.TryAdd(pair.Key, pair.Value); }
            }
        }
        if (operation is not null)
        {
            _ = await ReadRetainedAsync(head, "QualificationOperations", head.QualificationOperations, ProtectionQualificationOperationJournal.Id("executor", operation), token).ConfigureAwait(false);
            _ = await ReadRetainedAsync(head, "QualificationOperations", head.QualificationOperations, ProtectionQualificationOperationJournal.Id("enrollment", operation), token).ConfigureAwait(false);
        }
    }
    internal async Task HydrateLifetimeAsync(ErasureRootHead head, CancellationToken token)
    {
        if (head.LifetimeIndexRoot is null && head.QualificationIndexRoot is null && head.CustodyHistoryIndexRoot is null && head.AnchorIndexRoot is null) { return; }
        var index = RetainedIndex(head);
        foreach (var pair in await index.ReadAllAsync<ErasureRootErasure>(head.CustodyHistoryIndexRoot, "Erased:", token).ConfigureAwait(false))
        { if (!head.Erased.TryAdd(pair.Key, pair.Value) && Hash(head.Erased[pair.Key]) != Hash(pair.Value)) { throw new InvalidOperationException("Legacy retained original conflict."); } }
        foreach (var pair in await index.ReadAllAsync<CustodyKeyLifecycleOutcome>(head.CustodyHistoryIndexRoot, "LifecycleOutcomes:", token).ConfigureAwait(false))
        { if (!head.LifecycleOutcomes.TryAdd(pair.Key, pair.Value) && Hash(head.LifecycleOutcomes[pair.Key]) != Hash(pair.Value)) { throw new InvalidOperationException("Legacy retained original conflict."); } }
        foreach (var pair in await index.ReadAllAsync<ErasureRootAnchor>(head.AnchorIndexRoot, "Anchors:", token).ConfigureAwait(false))
        { if (!head.Anchors.TryAdd(pair.Key, pair.Value) && Hash(head.Anchors[pair.Key]) != Hash(pair.Value)) { throw new InvalidOperationException("Legacy retained original conflict."); } }
        foreach (var pair in await index.ReadAllAsync<string>(head.CustodyHistoryIndexRoot, "ManifestArchives:", token).ConfigureAwait(false))
        { if (!head.ManifestArchives.TryAdd(pair.Key, pair.Value) && Hash(head.ManifestArchives[pair.Key]) != Hash(pair.Value)) { throw new InvalidOperationException("Legacy retained original conflict."); } }
        foreach (var pair in await index.ReadAllAsync<string>(head.LifetimeIndexRoot, "GuardAdmissionArchives:", token).ConfigureAwait(false))
        { if (!head.GuardAdmissionArchives.TryAdd(pair.Key, pair.Value) && Hash(head.GuardAdmissionArchives[pair.Key]) != Hash(pair.Value)) { throw new InvalidOperationException("Legacy retained original conflict."); } }
        foreach (var pair in await index.ReadAllAsync<string>(head.LifetimeIndexRoot, "GuardAttemptArchives:", token).ConfigureAwait(false))
        { if (!head.GuardAttemptArchives.TryAdd(pair.Key, pair.Value) && Hash(head.GuardAttemptArchives[pair.Key]) != Hash(pair.Value)) { throw new InvalidOperationException("Legacy retained original conflict."); } }
        foreach (var pair in await index.ReadAllAsync<string>(head.LifetimeIndexRoot, "GuardSigningIntents:", token).ConfigureAwait(false))
        { if (!head.GuardSigningIntents.TryAdd(pair.Key, pair.Value) && Hash(head.GuardSigningIntents[pair.Key]) != Hash(pair.Value)) { throw new InvalidOperationException("Legacy retained original conflict."); } }
        foreach (var pair in await index.ReadAllAsync<string>(head.LifetimeIndexRoot, "ConditionalHoldOriginalArchives:", token).ConfigureAwait(false))
        { if (!head.ConditionalHoldOriginalArchives.TryAdd(pair.Key, pair.Value) && Hash(head.ConditionalHoldOriginalArchives[pair.Key]) != Hash(pair.Value)) { throw new InvalidOperationException("Legacy retained original conflict."); } }
        foreach (var pair in await index.ReadAllAsync<ErasureRootRevocationOriginal>(head.LifetimeIndexRoot, "RevocationOriginals:", token).ConfigureAwait(false))
        { if (!head.RevocationOriginals.TryAdd(pair.Key, pair.Value) && Hash(head.RevocationOriginals[pair.Key]) != Hash(pair.Value)) { throw new InvalidOperationException("Legacy retained original conflict."); } }
        foreach (var pair in await index.ReadAllAsync<DeletionReattestationActivation>(head.LifetimeIndexRoot, "ActivationEvidence:", token).ConfigureAwait(false))
        { if (!head.ActivationEvidence.TryAdd(pair.Key, pair.Value) && Hash(head.ActivationEvidence[pair.Key]) != Hash(pair.Value)) { throw new InvalidOperationException("Legacy retained original conflict."); } }
        foreach (var pair in await index.ReadAllAsync<DeletionBlockedReplacementReconciliation>(head.LifetimeIndexRoot, "BlockedReplacementEvidence:", token).ConfigureAwait(false))
        { if (!head.BlockedReplacementEvidence.TryAdd(pair.Key, pair.Value) && Hash(head.BlockedReplacementEvidence[pair.Key]) != Hash(pair.Value)) { throw new InvalidOperationException("Legacy retained original conflict."); } }
        foreach (var pair in await index.ReadAllAsync<GovernanceSigningRecovery>(head.LifetimeIndexRoot, "SigningRecoveries:", token).ConfigureAwait(false))
        { if (!head.SigningRecoveries.TryAdd(pair.Key, pair.Value) && Hash(head.SigningRecoveries[pair.Key]) != Hash(pair.Value)) { throw new InvalidOperationException("Legacy retained original conflict."); } }
        foreach (var pair in await index.ReadAllAsync<ErasureRootPinRefusalOriginal>(head.LifetimeIndexRoot, "PinRefusals:", token).ConfigureAwait(false))
        { if (!head.PinRefusals.TryAdd(pair.Key, pair.Value) && Hash(head.PinRefusals[pair.Key]) != Hash(pair.Value)) { throw new InvalidOperationException("Legacy retained original conflict."); } }
        foreach (var pair in await index.ReadAllAsync<ProtectionQualificationProducerOriginal>(head.QualificationIndexRoot, "QualificationProducerOriginals:", token).ConfigureAwait(false))
        { if (!head.QualificationProducerOriginals.TryAdd(pair.Key, pair.Value) && Hash(head.QualificationProducerOriginals[pair.Key]) != Hash(pair.Value)) { throw new InvalidOperationException("Legacy retained original conflict."); } }
        foreach (var pair in await index.ReadAllAsync<string>(head.QualificationIndexRoot, "QualificationAttempts:", token).ConfigureAwait(false))
        { if (!head.QualificationAttempts.TryAdd(pair.Key, pair.Value) && Hash(head.QualificationAttempts[pair.Key]) != Hash(pair.Value)) { throw new InvalidOperationException("Legacy retained original conflict."); } }
        foreach (var pair in await index.ReadAllAsync<ProtectionQualificationOperation>(head.QualificationIndexRoot, "QualificationOperations:", token).ConfigureAwait(false))
        { if (!head.QualificationOperations.TryAdd(pair.Key, pair.Value) && Hash(head.QualificationOperations[pair.Key]) != Hash(pair.Value)) { throw new InvalidOperationException("Legacy retained original conflict."); } }
        foreach (var pair in await index.ReadAllAsync<ProtectionQualificationSeedOriginal>(head.QualificationIndexRoot, "QualificationSeedOriginals:", token).ConfigureAwait(false))
        { if (!head.QualificationSeedOriginals.TryAdd(pair.Key, pair.Value) && Hash(head.QualificationSeedOriginals[pair.Key]) != Hash(pair.Value)) { throw new InvalidOperationException("Legacy retained original conflict."); } }
    }
    private async Task ValidateLegacyLifetimeAsync(ErasureRootHead head, CancellationToken token)
    {
        async Task Check<T>(string kind, SortedDictionary<string, T> values) where T : class
        {
            string? root = Root(head, kind); if (root is null) { return; }
            foreach (var pair in values)
            {
                var retained = await RetainedIndex(head).ReadAsync<T>(root, kind + ":" + pair.Key, token).ConfigureAwait(false);
                if (retained is not null && Hash(retained) != Hash(pair.Value))
                { throw new InvalidOperationException("Conflicting legacy/index authority requires independent exact migration."); }
            }
        }
        await Check("Erased", head.Erased).ConfigureAwait(false);
        await Check("LifecycleOutcomes", head.LifecycleOutcomes).ConfigureAwait(false);
        await Check("Anchors", head.Anchors).ConfigureAwait(false);
        await Check("ManifestArchives", head.ManifestArchives).ConfigureAwait(false);
        await Check("GuardAdmissionArchives", head.GuardAdmissionArchives).ConfigureAwait(false);
        await Check("GuardAttemptArchives", head.GuardAttemptArchives).ConfigureAwait(false);
        await Check("GuardSigningIntents", head.GuardSigningIntents).ConfigureAwait(false);
        await Check("ConditionalHoldOriginalArchives", head.ConditionalHoldOriginalArchives).ConfigureAwait(false);
        await Check("RevocationOriginals", head.RevocationOriginals).ConfigureAwait(false);
        await Check("ActivationEvidence", head.ActivationEvidence).ConfigureAwait(false);
        await Check("BlockedReplacementEvidence", head.BlockedReplacementEvidence).ConfigureAwait(false);
        await Check("SigningRecoveries", head.SigningRecoveries).ConfigureAwait(false);
        await Check("PinRefusals", head.PinRefusals).ConfigureAwait(false);
        await Check("QualificationProducerOriginals", head.QualificationProducerOriginals).ConfigureAwait(false);
        await Check("QualificationAttempts", head.QualificationAttempts).ConfigureAwait(false);
        await Check("QualificationOperations", head.QualificationOperations).ConfigureAwait(false);
        await Check("QualificationSeedOriginals", head.QualificationSeedOriginals).ConfigureAwait(false);
    }
    private Task<bool> ValidateLifetimeWriteSetAsync(string tenant, ErasureRootHead head, CancellationToken token)
    {
        if (head.QualificationOperations.Values.GroupBy(value => value.BindingDigest, StringComparer.Ordinal).Any(group => group.Count() > 1024))
        { return Task.FromResult(false); }
        if (head.QualificationAttempts.Any(pair => pair.Key.Length is < 1 or > 2048 || pair.Value.Length is < 1 or > 200 || pair.Value.Any(char.IsControl)))
        { return Task.FromResult(false); }
        string operation = head.LifetimeIndexRoot is null && head.AnchorIndexRoot is null && head.CustodyHistoryIndexRoot is null && head.QualificationIndexRoot is null ? "MigrateLifetimeIndex" : "RetainLifetimeIndex";
        return authorization.AuthorizeAsync(tenant, installation.EnrollmentId, operation,
            Hash(new { head.LifetimeIndexRoot, head.AnchorIndexRoot, head.CustodyHistoryIndexRoot, head.QualificationIndexRoot }), token);
    }
    private async Task<ErasureRootHead> CompactLifetimeAsync(ErasureRootHead head, CancellationToken token)
    {
        var index = RetainedIndex(head);
        string? root = head.LifetimeIndexRoot, qualificationRoot = head.QualificationIndexRoot, custodyRoot = head.CustodyHistoryIndexRoot, anchorRoot = head.AnchorIndexRoot;
        async Task Retain<T>(string kind, SortedDictionary<string, T> values, bool mutable = false) where T : class
        {
            foreach (var pair in values)
            {
                bool qualification = kind.StartsWith("Qualification", StringComparison.Ordinal);
                string? selectedRoot = kind == "Anchors" ? anchorRoot : CustodyHistory(kind) ? custodyRoot : qualification ? qualificationRoot : root;
                var prior = await index.ReadAsync<T>(selectedRoot, kind + ":" + pair.Key, token).ConfigureAwait(false);
                if (!mutable && prior is not null && Hash(prior) != Hash(pair.Value)) { throw new InvalidOperationException("Immutable retained original conflict."); }
                string updated = await index.WriteAsync(selectedRoot, kind + ":" + pair.Key, pair.Value, token).ConfigureAwait(false);
                if (kind == "Anchors") { anchorRoot = updated; } else if (CustodyHistory(kind)) { custodyRoot = updated; } else if (qualification) { qualificationRoot = updated; } else { root = updated; }
            }
        }
        await Retain("Erased", head.Erased).ConfigureAwait(false);
        await Retain("LifecycleOutcomes", head.LifecycleOutcomes).ConfigureAwait(false);
        await Retain("Anchors", head.Anchors, true).ConfigureAwait(false);
        await Retain("ManifestArchives", head.ManifestArchives).ConfigureAwait(false);
        await Retain("GuardAdmissionArchives", head.GuardAdmissionArchives).ConfigureAwait(false);
        await Retain("GuardAttemptArchives", head.GuardAttemptArchives).ConfigureAwait(false);
        await Retain("GuardSigningIntents", head.GuardSigningIntents).ConfigureAwait(false);
        await Retain("ConditionalHoldOriginalArchives", head.ConditionalHoldOriginalArchives).ConfigureAwait(false);
        await Retain("RevocationOriginals", head.RevocationOriginals).ConfigureAwait(false);
        await Retain("ActivationEvidence", head.ActivationEvidence).ConfigureAwait(false);
        await Retain("BlockedReplacementEvidence", head.BlockedReplacementEvidence).ConfigureAwait(false);
        await Retain("SigningRecoveries", head.SigningRecoveries).ConfigureAwait(false);
        await Retain("PinRefusals", head.PinRefusals).ConfigureAwait(false);
        await Retain("QualificationProducerOriginals", head.QualificationProducerOriginals).ConfigureAwait(false);
        await Retain("QualificationAttempts", head.QualificationAttempts).ConfigureAwait(false);
        await Retain("QualificationOperations", head.QualificationOperations, true).ConfigureAwait(false);
        await Retain("QualificationSeedOriginals", head.QualificationSeedOriginals).ConfigureAwait(false);
        // Each independently enrolled one-attempt profile has its own bounded working cohort.
        // Historical profiles themselves are indexed rather than accumulating a tenant pointer map.
        foreach (string binding in head.QualificationOperations.Values.Select(value => value.BindingDigest)
            .Concat(head.QualificationProducerOriginals.Values.Select(value => value.Producer.BindingDigest)).Distinct(StringComparer.Ordinal))
        {
            string? group = await index.ReadAsync<string>(qualificationRoot, "qualification-group:" + binding, token).ConfigureAwait(false);
            foreach (var pair in head.QualificationOperations.Where(pair => pair.Value.BindingDigest == binding))
            { group = await index.WriteAsync(group, "operation:" + pair.Key, pair.Value, token).ConfigureAwait(false); }
            foreach (var pair in head.QualificationProducerOriginals.Where(pair => pair.Value.Producer.BindingDigest == binding))
            { group = await index.WriteAsync(group, "producer:" + pair.Key, pair.Value, token).ConfigureAwait(false); }
            if (group is not null) { qualificationRoot = await index.WriteAsync(qualificationRoot, "qualification-group:" + binding, group, token).ConfigureAwait(false); }
        }
        foreach (var value in head.RevocationOriginals.Values)
        { root = await index.WriteAsync(root, "revocation-reference:" + Hash(new { value.Envelope.EventIdentity, value.Envelope.SignatureDigest }), value, token).ConfigureAwait(false); }
        foreach (var value in head.SigningRecoveries.Values)
        {
            string reference = "signing-recovery-reference:" + Hash(new { value.Payload, value.SigningRequestId, value.DetachedJwsDigest });
            var prior = await index.ReadAsync<GovernanceSigningRecovery>(root, reference, token).ConfigureAwait(false);
            if (prior is null || prior.ObservedAt < value.ObservedAt || prior.ObservedAt == value.ObservedAt && string.CompareOrdinal(Hash(prior), Hash(value)) < 0)
            { root = await index.WriteAsync(root, reference, value, token).ConfigureAwait(false); }
        }
        return head with { LifetimeIndexRoot = root, AnchorIndexRoot = anchorRoot, QualificationIndexRoot = qualificationRoot, CustodyHistoryIndexRoot = custodyRoot,
            Erased = new(StringComparer.Ordinal),
            LifecycleOutcomes = new(StringComparer.Ordinal),
            Anchors = new(StringComparer.Ordinal),
            ManifestArchives = new(StringComparer.Ordinal),
            GuardAdmissionArchives = new(StringComparer.Ordinal),
            GuardAttemptArchives = new(StringComparer.Ordinal),
            GuardSigningIntents = new(StringComparer.Ordinal),
            ConditionalHoldOriginalArchives = new(StringComparer.Ordinal),
            RevocationOriginals = new(StringComparer.Ordinal),
            ActivationEvidence = new(StringComparer.Ordinal),
            BlockedReplacementEvidence = new(StringComparer.Ordinal),
            SigningRecoveries = new(StringComparer.Ordinal),
            PinRefusals = new(StringComparer.Ordinal),
            QualificationProducerOriginals = new(StringComparer.Ordinal),
            QualificationAttempts = new(StringComparer.Ordinal),
            QualificationOperations = new(StringComparer.Ordinal),
            QualificationSeedOriginals = new(StringComparer.Ordinal) };
    }
    private static ErasureRootHead CompactCapacity(ErasureRootHead head) => head with
    {
        Erased = new(StringComparer.Ordinal),
        LifecycleOutcomes = new(StringComparer.Ordinal),
        Anchors = new(StringComparer.Ordinal),
        ManifestArchives = new(StringComparer.Ordinal),
        GuardAdmissionArchives = new(StringComparer.Ordinal),
        GuardAttemptArchives = new(StringComparer.Ordinal),
        GuardSigningIntents = new(StringComparer.Ordinal),
        ConditionalHoldOriginalArchives = new(StringComparer.Ordinal),
        RevocationOriginals = new(StringComparer.Ordinal),
        ActivationEvidence = new(StringComparer.Ordinal),
        BlockedReplacementEvidence = new(StringComparer.Ordinal),
        SigningRecoveries = new(StringComparer.Ordinal),
        PinRefusals = new(StringComparer.Ordinal),
        QualificationProducerOriginals = new(StringComparer.Ordinal),
        QualificationAttempts = new(StringComparer.Ordinal),
        QualificationOperations = new(StringComparer.Ordinal),
        QualificationSeedOriginals = new(StringComparer.Ordinal)
    };

}
