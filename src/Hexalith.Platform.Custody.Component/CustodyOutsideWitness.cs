using System.Security.Cryptography;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Custody.Component;

/// <summary>Forward-only witness kept outside the volume. Restoring the volume does not replace this object.</summary>
internal sealed class CustodyOutsideWitness : ICustodyOutsideWitness, ICustodyOriginalManifestSource
{
    private readonly Dictionary<string, byte[]> _liveEpochs = new(StringComparer.Ordinal);
    private readonly HashSet<string> _destroyedEpochs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CustodySuccessorWrap> _successors = new(StringComparer.Ordinal);
    private readonly HashSet<string> _destroyedKeys = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CustodyWitnessFact> _facts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CustodyManifestRecord> _manifests = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CustodyOriginalManifest> _admitted = new(StringComparer.Ordinal);
    private readonly List<CustodyWitnessHold> _pins = [];
    private readonly Dictionary<string, string> _tenantLive = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _epochTenant = new(StringComparer.Ordinal);
    private int _generation;

    /// <inheritdoc/>
    public bool Available { get; set; } = true;

    internal int NextGeneration => _generation;

    internal bool IsLive(string generation) => Available && _liveEpochs.ContainsKey(generation) && !_destroyedEpochs.Contains(generation);

    internal bool IsDestroyedEpoch(string generation) => _destroyedEpochs.Contains(generation);

    internal bool IsDestroyed(CustodyKeyObjectIdentity identity) => _destroyedKeys.Contains(Key(identity));

    internal bool HasPin(CustodyKeyObjectIdentity identity, string holdId)
        => _pins.Any(p => p.Identity == identity && p.HoldId == holdId);

    internal bool HasAnyPin(CustodyKeyObjectIdentity identity) => _pins.Any(p => p.Identity == identity);

    internal IReadOnlyList<CustodyWitnessHold> Pins => _pins.ToArray();

    /// <inheritdoc/>
    public CustodyOriginalManifest? FindOriginal(string reservationId)
        => Available && _admitted.TryGetValue(reservationId, out var manifest) ? manifest : null;

    /// <inheritdoc/>
    public bool Confirms(CustodyKeyLifecycleRequest request, CustodyKeyLifecycleOutcome outcome)
    {
        if (!Available || request is null || outcome is null || outcome.Identity != request.Identity || outcome.OperationId != request.OperationId) { return false; }
        if (!_facts.TryGetValue(request.OperationId, out var fact) || fact.Identity != outcome.Identity) { return false; }
        if (outcome.Status == CustodyKeyLifecycleStatus.Conflict)
        {
            if (fact.RequestDigest != outcome.RequestDigest)
            { return outcome.ReceiptId is null && outcome.RestoreBarrierReceiptId is null; }
            return fact.Status == CustodyComponentStatus.Conflict && fact.ReceiptId == outcome.ReceiptId
                && fact.RestoreBarrierReceiptId == outcome.RestoreBarrierReceiptId && fact.HoldId == request.HoldId;
        }
        if (fact.RequestDigest != outcome.RequestDigest || fact.ReceiptId != outcome.ReceiptId || fact.RestoreBarrierReceiptId != outcome.RestoreBarrierReceiptId
            || !Matches(fact.Status, outcome.Status)) { return false; }
        if (fact.HoldId == request.HoldId) { return true; }
        return fact.Status == CustodyComponentStatus.BlockedByPin && request.Action == CustodyKeyLifecycleAction.Destroy
            && request.HoldId is null && fact.HoldId is not null;
    }

    internal bool TryAdmit(CustodyOriginalManifest manifest)
    {
        if (_admitted.TryGetValue(manifest.ReservationId, out var prior))
        {
            return prior.BatchId == manifest.BatchId && prior.ManifestDigest == manifest.ManifestDigest && prior.Targets.SequenceEqual(manifest.Targets);
        }
        _admitted.Add(manifest.ReservationId, manifest);
        return true;
    }

    internal CustodyManifestRecord? FindManifest(string reservationId)
        => _manifests.TryGetValue(reservationId, out var record) ? record : null;

    internal CustodyWitnessFact? FindFact(string operationId)
        => _facts.TryGetValue(operationId, out var fact) ? fact : null;

    internal void Remember(CustodyWitnessFact fact) => _facts[fact.OperationId] = fact;

    internal void RememberManifest(string reservationId, CustodyManifestRecord record) => _manifests[reservationId] = record;

    internal void AddPin(string holdId, CustodyKeyObjectIdentity identity) => _pins.Add(new CustodyWitnessHold(holdId, identity));

    internal bool RemovePin(string holdId, CustodyKeyObjectIdentity identity)
    {
        int index = _pins.FindIndex(p => p.HoldId == holdId && p.Identity == identity);
        if (index < 0) { return false; }
        _pins.RemoveAt(index);
        return true;
    }

    internal string? LiveGeneration(string tenantId)
        => _tenantLive.TryGetValue(tenantId, out string? generation) ? generation : null;

    internal string AddEpoch(string tenantId, byte[] key)
    {
        string generation = "g" + _generation.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _generation++;
        _liveEpochs.Add(generation, key);
        _epochTenant.Add(generation, tenantId);
        _tenantLive[tenantId] = generation;
        return generation;
    }

    internal byte[]? LiveEpoch(string generation) => _liveEpochs.TryGetValue(generation, out byte[]? key) ? key : null;

    internal void AddSuccessor(CustodyKeyObjectIdentity identity, string generation, byte[] nonce, byte[] ciphertext, byte[] tag)
    {
        string key = Key(identity);
        if (_successors.TryGetValue(key, out var prior)) { ZeroWrap(prior); }
        _successors[key] = new(generation, nonce, ciphertext, tag);
    }

    internal bool TrySuccessor(CustodyKeyObjectIdentity identity, out CustodySuccessorWrap wrap)
        => _successors.TryGetValue(Key(identity), out wrap!);

    internal void MarkDestroyed(CustodyKeyObjectIdentity identity) => _destroyedKeys.Add(Key(identity));

    internal void RemoveSuccessor(CustodyKeyObjectIdentity identity)
    {
        if (_successors.Remove(Key(identity), out var prior)) { ZeroWrap(prior); }
    }

    internal void DestroyEpoch(string generation)
    {
        _destroyedEpochs.Add(generation);
        if (_liveEpochs.TryGetValue(generation, out byte[]? key)) { CryptographicOperations.ZeroMemory(key); }
        _liveEpochs.Remove(generation);
        if (_epochTenant.TryGetValue(generation, out string? tenant) && _tenantLive.TryGetValue(tenant, out string? current) && current == generation)
        { _tenantLive.Remove(tenant); }
    }

    internal void ZeroAbandoned()
    {
        foreach (byte[] key in _liveEpochs.Values) { CryptographicOperations.ZeroMemory(key); }
        _liveEpochs.Clear();
        _tenantLive.Clear();
        foreach (var wrap in _successors.Values) { ZeroWrap(wrap); }
        _successors.Clear();
    }

    internal static bool TryRepromote(string generation)
    {
        // Volume bytes cannot make a retired or restored epoch current.
        _ = generation;
        return false;
    }

    internal CustodyOutsideWitness Clone()
    {
        var copy = new CustodyOutsideWitness { Available = Available, _generation = _generation };
        foreach (var pair in _liveEpochs) { copy._liveEpochs.Add(pair.Key, (byte[])pair.Value.Clone()); }
        foreach (var pair in _tenantLive) { copy._tenantLive.Add(pair.Key, pair.Value); }
        foreach (var pair in _epochTenant) { copy._epochTenant.Add(pair.Key, pair.Value); }
        foreach (string generation in _destroyedEpochs) { copy._destroyedEpochs.Add(generation); }
        foreach (var pair in _successors) { copy._successors.Add(pair.Key, pair.Value.Copy()); }
        foreach (string key in _destroyedKeys) { copy._destroyedKeys.Add(key); }
        foreach (var pair in _facts) { copy._facts.Add(pair.Key, pair.Value); }
        foreach (var pair in _manifests) { copy._manifests.Add(pair.Key, pair.Value); }
        foreach (var pair in _admitted) { copy._admitted.Add(pair.Key, pair.Value); }
        copy._pins.AddRange(_pins);
        return copy;
    }

    internal void CommitFrom(CustodyOutsideWitness draft)
    {
        foreach (byte[] key in _liveEpochs.Values) { CryptographicOperations.ZeroMemory(key); }
        _liveEpochs.Clear();
        foreach (var pair in draft._liveEpochs) { _liveEpochs.Add(pair.Key, pair.Value); }
        draft._liveEpochs.Clear();
        _destroyedEpochs.Clear();
        foreach (string generation in draft._destroyedEpochs) { _destroyedEpochs.Add(generation); }
        foreach (var wrap in _successors.Values) { ZeroWrap(wrap); }
        _successors.Clear();
        foreach (var pair in draft._successors) { _successors.Add(pair.Key, pair.Value); }
        _destroyedKeys.Clear();
        foreach (string key in draft._destroyedKeys) { _destroyedKeys.Add(key); }
        _facts.Clear();
        foreach (var pair in draft._facts) { _facts.Add(pair.Key, pair.Value); }
        _manifests.Clear();
        foreach (var pair in draft._manifests) { _manifests.Add(pair.Key, pair.Value); }
        _admitted.Clear();
        foreach (var pair in draft._admitted) { _admitted.Add(pair.Key, pair.Value); }
        _pins.Clear();
        _pins.AddRange(draft._pins);
        _tenantLive.Clear();
        foreach (var pair in draft._tenantLive) { _tenantLive.Add(pair.Key, pair.Value); }
        _epochTenant.Clear();
        foreach (var pair in draft._epochTenant) { _epochTenant.Add(pair.Key, pair.Value); }
        _generation = draft._generation;
        Available = draft.Available;
    }

    private static void ZeroWrap(CustodySuccessorWrap wrap)
    {
        CryptographicOperations.ZeroMemory(wrap.Nonce);
        CryptographicOperations.ZeroMemory(wrap.Ciphertext);
        CryptographicOperations.ZeroMemory(wrap.Tag);
    }

    private static bool Matches(CustodyComponentStatus status, CustodyKeyLifecycleStatus outcome) => status switch
    {
        CustodyComponentStatus.Pinned => outcome == CustodyKeyLifecycleStatus.Pinned,
        CustodyComponentStatus.Unpinned => outcome == CustodyKeyLifecycleStatus.Unpinned,
        CustodyComponentStatus.Destroyed => outcome == CustodyKeyLifecycleStatus.Destroyed,
        CustodyComponentStatus.BlockedByPin => outcome == CustodyKeyLifecycleStatus.BlockedByPin,
        CustodyComponentStatus.Conflict => outcome == CustodyKeyLifecycleStatus.Conflict,
        CustodyComponentStatus.Unknown => outcome == CustodyKeyLifecycleStatus.Unknown,
        CustodyComponentStatus.Unavailable => outcome == CustodyKeyLifecycleStatus.Unavailable,
        _ => false,
    };

    private static string Key(CustodyKeyObjectIdentity identity)
        => string.Join('\u001f', identity.TenantId, identity.ObjectId, identity.Purpose.ToString(), identity.KeyAlias, identity.KeyVersion,
            identity.LifecycleVersion, identity.StoreTarget, identity.ContractVersion);
}
