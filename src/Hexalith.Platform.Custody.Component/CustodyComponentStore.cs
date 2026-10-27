using System.Security.Cryptography;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Custody.Component;

/// <summary>
/// Disposable private store for envelopes, copy inventory, the outside witness, pin fence, and shared-cut destruction.
/// It is not an installed public reference, and the shared-cut model is not physical proof.
/// </summary>
internal sealed class CustodyComponentStore : ICustodyOriginalManifestSource, IDisposable
{
    private readonly object _gate = new();
    private readonly byte[] _kek;
    private readonly Dictionary<string, byte[]> _deks = new(StringComparer.Ordinal);
    private CustodyComponentVolume _volume = new();
    private bool _disposed;

    internal CustodyComponentStore(CustodyOutsideWitness? witness, ReadOnlySpan<byte> tenantKek)
    {
        if (tenantKek.Length != 32) { throw new ArgumentException("Tenant KEK must be 32 bytes.", nameof(tenantKek)); }
        Witness = witness;
        _kek = tenantKek.ToArray();
    }

    internal CustodyOutsideWitness? Witness { get; }
    internal int Mutations { get; private set; }
    internal int PhysicalCuts { get; private set; }
    internal bool LoseNextEffect { get; set; }

    public CustodyOriginalManifest? FindOriginal(string reservationId)
    {
        lock (_gate) { return Witness is { Available: true } ? Witness.FindOriginal(reservationId) : null; }
    }

    internal bool TryEnroll(CustodyKeyObjectIdentity identity, string kekVersion, ReadOnlySpan<byte> dek)
    {
        lock (_gate)
        {
            if (!Ready() || dek.Length != 32 || _volume.Find(identity) is not null) { return false; }
            CustodyKeyLifecycleActor.ValidateIdentity(identity);
            string? generation = Witness!.LiveGeneration(identity.TenantId);
            if (generation is null) { generation = Witness.AddEpoch(identity.TenantId, RandomNumberGenerator.GetBytes(32)); }
            byte[] epoch = Witness.LiveEpoch(generation)!;
            byte[] targetKey = RandomNumberGenerator.GetBytes(32);
            try
            {
                byte[] outer = CustodyEpochEnvelope.SealC0(identity, kekVersion, dek, _kek, targetKey, generation);
                byte[] wrapped = CustodyEpochEnvelope.Seal(epoch, targetKey, CustodyEpochEnvelope.KeyAad(identity, generation));
                CustodyEpochEnvelope.Split(outer, 60, out byte[] nonce, out byte[] cipher, out byte[] tag);
                CustodyEpochEnvelope.Split(wrapped, 32, out byte[] keyNonce, out byte[] keyCipher, out byte[] keyTag);
                if (!_volume.EpochGenerations.Contains(generation)) { _volume.AddEpoch(generation, (byte[])epoch.Clone()); }
                _volume.Add(new CustodyComponentEnvelope(identity, kekVersion, generation, generation, nonce, cipher, tag, keyNonce, keyCipher, keyTag));
                string dekKey = DekKey(identity);
                if (_deks.TryGetValue(dekKey, out byte[]? previous)) { CryptographicOperations.ZeroMemory(previous); }
                _deks[dekKey] = dek.ToArray();
                Mutations++;
                return true;
            }
            finally { CryptographicOperations.ZeroMemory(targetKey); }
        }
    }

    internal CustodyKeyObjectIdentity? FindEnrolled(ProtectionTarget target)
    {
        lock (_gate)
        {
            CustodyKeyObjectIdentity? found = null;
            foreach (var envelope in _volume.Envelopes)
            {
                if (envelope.IsZeroed || Witness is not null && Witness.IsDestroyed(envelope.Identity)) { continue; }
                var identity = envelope.Identity;
                if (identity.TenantId != target.TenantId || identity.ObjectId != target.AgentInteractionId || identity.KeyAlias != target.TargetProtectionKeyAlias)
                { continue; }
                if (found is not null) { return null; }
                found = identity;
            }
            return found;
        }
    }

    internal bool RetainsPlainDek(CustodyKeyObjectIdentity identity)
    {
        lock (_gate) { return _deks.TryGetValue(DekKey(identity), out byte[]? dek) && dek.Any(b => b != 0); }
    }

    internal bool TryDecrypt(CustodyKeyObjectIdentity identity, string kekVersion, Span<byte> destination)
    {
        lock (_gate)
        {
            bool opened = false;
            try { opened = TryDecryptCore(identity, kekVersion, destination); }
            finally { if (!opened) { destination.Clear(); } }
            return opened;
        }
    }

    private bool TryDecryptCore(CustodyKeyObjectIdentity identity, string kekVersion, Span<byte> destination)
    {
        if (_disposed || Witness is not { Available: true } || destination.Length != 32 || Witness.IsDestroyed(identity)) { return false; }
        var envelope = _volume.Find(identity);
        if (envelope is null || envelope.IsZeroed) { return false; }
        byte[]? targetKey = null;
        try
        {
            if (Witness.TrySuccessor(identity, out var successor))
            {
                byte[]? epoch = Witness.LiveEpoch(successor.Generation);
                if (epoch is null || !Witness.IsLive(successor.Generation)) { return false; }
                targetKey = CustodyEpochEnvelope.Open(epoch, successor.Nonce, successor.Ciphertext, successor.Tag, CustodyEpochEnvelope.KeyAad(identity, successor.Generation));
            }
            else
            {
                if (!Witness.IsLive(envelope.WrapGeneration)) { return false; }
                byte[]? epoch = Witness.LiveEpoch(envelope.WrapGeneration);
                if (epoch is null) { return false; }
                targetKey = CustodyEpochEnvelope.Open(epoch, envelope.WrappedKeyNonce, envelope.WrappedKeyCiphertext, envelope.WrappedKeyTag,
                    CustodyEpochEnvelope.KeyAad(identity, envelope.WrapGeneration));
            }
            if (targetKey.Length != 32) { return false; }
            byte[] dek = CustodyEpochEnvelope.OpenDek(identity, kekVersion, envelope.SealedGeneration, envelope.OuterNonce, envelope.OuterCiphertext, envelope.OuterTag, targetKey, _kek);
            try { dek.CopyTo(destination); return true; }
            finally { CryptographicOperations.ZeroMemory(dek); }
        }
        catch (CryptographicException) { return false; }
        finally { if (targetKey is not null) { CryptographicOperations.ZeroMemory(targetKey); } }
    }

    internal bool VolumeHoldsEpochBytes(string generation)
    {
        lock (_gate) { return !_disposed && _volume.HoldsEpochBytes(generation); }
    }

    internal bool EpochIsLive(string generation)
    {
        lock (_gate) { return !_disposed && Witness is not null && Witness.IsLive(generation); }
    }

    internal bool TryRepromoteEpoch(string generation)
    {
        lock (_gate) { return Witness is not null && CustodyOutsideWitness.TryRepromote(generation); }
    }

    internal byte[] CaptureVolume()
    {
        lock (_gate) { return _disposed ? [] : _volume.Capture(); }
    }

    internal void RestoreVolume(byte[] snapshot)
    {
        lock (_gate)
        {
            if (_disposed) { return; }
            var restored = CustodyComponentVolume.FromSnapshot(snapshot);
            _volume.ZeroSecrets();
            _volume = restored;
        }
    }

    internal CustodyComponentOutcome Pin(CustodyKeyLifecycleRequest request, string requestDigest)
        => Hold(request, requestDigest, pin: true);

    internal CustodyComponentOutcome Unpin(CustodyKeyLifecycleRequest request, string requestDigest)
        => Hold(request, requestDigest, pin: false);

    internal CustodyComponentOutcome Consume(string operationId, CustodyOriginalManifest manifest, string requestDigest, CustodyKeyObjectIdentity? requested = null)
    {
        lock (_gate)
        {
            CustodyKeyObjectIdentity? named = requested ?? manifest.Targets.FirstOrDefault();
            var missing = Result(operationId, requestDigest, CustodyComponentStatus.Unavailable, named);
            if (_disposed || Witness is null) { return missing; }
            if (!Witness.Available) { return missing with { Status = CustodyComponentStatus.Unknown }; }
            if (Witness.FindManifest(manifest.ReservationId) is { } prior)
            {
                string? receipt = requested is null ? prior.Receipts.FirstOrDefault()?.ReceiptId : ReceiptFor(prior.Receipts, requested);
                return prior.RequestDigest == requestDigest
                    ? Result(operationId, requestDigest, prior.Status, named, receipt, prior.RestoreBarrierReceiptId, prior.Receipts)
                    : Result(operationId, requestDigest, CustodyComponentStatus.Conflict, named);
            }
            if (Witness.FindFact(operationId) is { } existing && !ReleasedBlock(existing, requestDigest))
            {
                return existing.RequestDigest == requestDigest
                    ? FromFact(existing)
                    : Result(operationId, requestDigest, CustodyComponentStatus.Conflict, named);
            }
            if (!ValidManifest(manifest) || (requested is not null && !manifest.Targets.Contains(requested)))
            {
                return Result(operationId, requestDigest, CustodyComponentStatus.Conflict, named);
            }
            string tenant = manifest.Targets[0].TenantId;
            var survivors = _volume.Envelopes.Where(e => IsLiveSurvivor(e, tenant, manifest)).Select(e => e.Identity).ToArray();
            var pins = Witness.Pins.Where(p => manifest.Targets.Contains(p.Identity) || survivors.Contains(p.Identity)).ToArray();
            if (LoseNextEffect)
            {
                LoseNextEffect = false;
                Witness.RememberManifest(manifest.ReservationId, new CustodyManifestRecord(requestDigest, CustodyComponentStatus.Unknown, null, []));
                return Remember(Result(operationId, requestDigest, CustodyComponentStatus.Unknown, named));
            }
            string gate = CustodySharedCutGate.Run(manifest, requestDigest, survivors, pins);
            if (gate == CustodySharedCutGate.Blocked)
            {
                string? hold = pins.FirstOrDefault(p => manifest.Targets.Contains(p.Identity)).HoldId;
                if (hold is null) { return missing with { Status = CustodyComponentStatus.Unknown }; }
                var blocked = Remember(Result(operationId, requestDigest, CustodyComponentStatus.BlockedByPin, named, holdId: hold));
                if (operationId != manifest.ReservationId)
                {
                    Witness.Remember(new CustodyWitnessFact(manifest.ReservationId, requestDigest, CustodyComponentStatus.BlockedByPin, null, null, named, hold));
                }
                return blocked;
            }
            if (gate != CustodySharedCutGate.Published)
            {
                return Result(operationId, requestDigest, CustodyComponentStatus.Conflict, named);
            }
            var draftVolume = _volume.Clone();
            var draftWitness = Witness.Clone();
            if (!draftWitness.TryAdmit(manifest) || !TryCut(draftVolume, draftWitness, operationId, manifest, requestDigest, survivors, named, out var outcome))
            {
                draftVolume.ZeroSecrets();
                draftWitness.ZeroAbandoned();
                return Remember(Result(operationId, requestDigest, CustodyComponentStatus.Unknown, named));
            }
            _volume.ZeroSecrets();
            Witness.CommitFrom(draftWitness);
            _volume = draftVolume;
            foreach (var target in manifest.Targets) { DropDek(target); }
            PhysicalCuts++;
            Mutations++;
            return outcome;
        }
    }

    internal CustodyComponentOutcome Lookup(string operationId, string requestDigest)
    {
        lock (_gate)
        {
            if (_disposed || Witness is null) { return Result(operationId, requestDigest, CustodyComponentStatus.Unavailable, null); }
            if (!Witness.Available) { return Result(operationId, requestDigest, CustodyComponentStatus.Unknown, null); }
            if (Witness.FindFact(operationId) is not { } fact) { return Result(operationId, requestDigest, CustodyComponentStatus.NotStarted, null); }
            return fact.RequestDigest == requestDigest ? FromFact(fact) : Result(operationId, requestDigest, CustodyComponentStatus.Conflict, fact.Identity);
        }
    }

    internal CustodyComponentOutcome LookupManifest(string reservationId, string requestDigest)
    {
        lock (_gate)
        {
            if (_disposed || Witness is null) { return Result(reservationId, requestDigest, CustodyComponentStatus.Unavailable, null); }
            if (!Witness.Available) { return Result(reservationId, requestDigest, CustodyComponentStatus.Unknown, null); }
            if (Witness.FindManifest(reservationId) is not { } record)
            {
                if (Witness.FindFact(reservationId) is { } fact)
                {
                    return fact.RequestDigest == requestDigest ? FromFact(fact) : Result(reservationId, requestDigest, CustodyComponentStatus.Conflict, fact.Identity);
                }
                return Result(reservationId, requestDigest, CustodyComponentStatus.NotStarted, null);
            }
            if (record.RequestDigest != requestDigest) { return Result(reservationId, requestDigest, CustodyComponentStatus.Conflict, null); }
            return Result(reservationId, requestDigest, record.Status, null, record.Receipts.FirstOrDefault()?.ReceiptId, record.RestoreBarrierReceiptId, record.Receipts);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) { return; }
            CryptographicOperations.ZeroMemory(_kek);
            foreach (byte[] dek in _deks.Values) { CryptographicOperations.ZeroMemory(dek); }
            _deks.Clear();
            _volume.ZeroSecrets();
            Witness?.ZeroAbandoned();
            _disposed = true;
        }
    }

    private CustodyComponentOutcome Hold(CustodyKeyLifecycleRequest request, string requestDigest, bool pin)
    {
        lock (_gate)
        {
            var missing = Result(request.OperationId, requestDigest, CustodyComponentStatus.Unavailable, request.Identity, holdId: request.HoldId);
            if (_disposed || Witness is null || request.HoldId is null) { return missing; }
            if (!Witness.Available) { return missing with { Status = CustodyComponentStatus.Unknown }; }
            if (Witness.FindFact(request.OperationId) is { } fact)
            {
                return fact.RequestDigest == requestDigest ? FromFact(fact) : missing with { Status = CustodyComponentStatus.Conflict };
            }
            var envelope = _volume.Find(request.Identity);
            if (envelope is null || !Live(request.Identity, envelope)) { return missing; }
            bool exists = Witness.HasPin(request.Identity, request.HoldId);
            if (pin == exists) { return Remember(missing with { Status = CustodyComponentStatus.Conflict }); }
            if (pin) { Witness.AddPin(request.HoldId, request.Identity); }
            else if (!Witness.RemovePin(request.HoldId, request.Identity)) { return Remember(missing with { Status = CustodyComponentStatus.Conflict }); }
            Mutations++;
            var status = pin ? CustodyComponentStatus.Pinned : CustodyComponentStatus.Unpinned;
            return Remember(Result(request.OperationId, requestDigest, status, request.Identity, (pin ? "pin-" : "unpin-") + request.HoldId, holdId: request.HoldId));
        }
    }

    private bool ReleasedBlock(CustodyWitnessFact fact, string requestDigest)
        => fact.Status == CustodyComponentStatus.BlockedByPin && fact.RequestDigest == requestDigest && fact.HoldId is { } hold
            && !Witness!.Pins.Any(p => p.HoldId == hold);

    private bool Live(CustodyKeyObjectIdentity identity, CustodyComponentEnvelope envelope)
        => Witness!.TrySuccessor(identity, out var successor) ? Witness.IsLive(successor.Generation) : Witness.IsLive(envelope.WrapGeneration);

    private static bool TryCut(CustodyComponentVolume volume, CustodyOutsideWitness witness, string operationId, CustodyOriginalManifest manifest, string requestDigest,
        IReadOnlyList<CustodyKeyObjectIdentity> survivors, CustodyKeyObjectIdentity? requested, out CustodyComponentOutcome outcome)
    {
        outcome = Result(operationId, requestDigest, CustodyComponentStatus.Unknown, requested);
        string tenant = manifest.Targets[0].TenantId;
        string? source = witness.LiveGeneration(tenant);
        if (source is null || witness.LiveEpoch(source) is not { } epoch) { return false; }
        byte[] nextEpoch = RandomNumberGenerator.GetBytes(32);
        byte[] volumeEpoch = [];
        bool completed = false;
        try
        {
            string successor = witness.AddEpoch(tenant, nextEpoch);
            volumeEpoch = (byte[])nextEpoch.Clone();
            volume.AddEpoch(successor, volumeEpoch);
            foreach (var survivor in survivors)
            {
                if (!TryUnwrap(volume, witness, epoch, source, survivor, out byte[] targetKey)) { return false; }
                try
                {
                    byte[] wrapped = CustodyEpochEnvelope.Seal(nextEpoch, targetKey, CustodyEpochEnvelope.KeyAad(survivor, successor));
                    CustodyEpochEnvelope.Split(wrapped, 32, out byte[] nonce, out byte[] cipher, out byte[] tag);
                    witness.AddSuccessor(survivor, successor, nonce, cipher, tag);
                    volume.Find(survivor)!.Rewrap(successor, (byte[])nonce.Clone(), (byte[])cipher.Clone(), (byte[])tag.Clone());
                }
                finally { CryptographicOperations.ZeroMemory(targetKey); }
            }
            string barrier = "barrier-" + manifest.ReservationId;
            var receipts = new List<DeletionTargetReceipt>(manifest.Targets.Count);
            foreach (var target in manifest.Targets)
            {
                receipts.Add(new DeletionTargetReceipt(new ProtectionTarget(target.TenantId, target.ObjectId, target.KeyAlias), manifest.BatchId, "receipt-" + target.ObjectId));
                volume.ZeroEnvelope(target);
                witness.RemoveSuccessor(target);
                witness.MarkDestroyed(target);
            }
            witness.DestroyEpoch(source);
            volume.ZeroEpoch(source);
            witness.RememberManifest(manifest.ReservationId, new CustodyManifestRecord(requestDigest, CustodyComponentStatus.Destroyed, barrier, receipts));
            var named = requested ?? manifest.Targets[0];
            string? own = ReceiptFor(receipts, named);
            outcome = Result(operationId, requestDigest, CustodyComponentStatus.Destroyed, named, own, barrier, receipts);
            witness.Remember(new CustodyWitnessFact(operationId, requestDigest, CustodyComponentStatus.Destroyed, own, barrier, named, null));
            completed = true;
            return true;
        }
        finally
        {
            if (!completed)
            {
                CryptographicOperations.ZeroMemory(nextEpoch);
                CryptographicOperations.ZeroMemory(volumeEpoch);
            }
        }
    }

    private static string? ReceiptFor(IReadOnlyList<DeletionTargetReceipt> receipts, CustodyKeyObjectIdentity identity)
        => receipts.FirstOrDefault(r => r.Target.TenantId == identity.TenantId && r.Target.AgentInteractionId == identity.ObjectId && r.Target.TargetProtectionKeyAlias == identity.KeyAlias)?.ReceiptId;

    private static bool TryUnwrap(CustodyComponentVolume volume, CustodyOutsideWitness witness, byte[] epoch, string generation, CustodyKeyObjectIdentity identity, out byte[] targetKey)
    {
        targetKey = [];
        try
        {
            if (witness.IsDestroyed(identity)) { return false; }
            if (witness.TrySuccessor(identity, out var successor))
            {
                byte[]? successorEpoch = witness.LiveEpoch(successor.Generation);
                if (successorEpoch is null || !witness.IsLive(successor.Generation)) { return false; }
                targetKey = CustodyEpochEnvelope.Open(successorEpoch, successor.Nonce, successor.Ciphertext, successor.Tag, CustodyEpochEnvelope.KeyAad(identity, successor.Generation));
            }
            else
            {
                var envelope = volume.Find(identity);
                if (envelope is null || envelope.IsZeroed || envelope.WrapGeneration != generation) { return false; }
                targetKey = CustodyEpochEnvelope.Open(epoch, envelope.WrappedKeyNonce, envelope.WrappedKeyCiphertext, envelope.WrappedKeyTag, CustodyEpochEnvelope.KeyAad(identity, generation));
            }
            if (targetKey.Length == 32) { return true; }
            CryptographicOperations.ZeroMemory(targetKey);
            targetKey = [];
            return false;
        }
        catch (CryptographicException)
        {
            if (targetKey.Length > 0) { CryptographicOperations.ZeroMemory(targetKey); }
            targetKey = [];
            return false;
        }
    }

    private bool IsLiveSurvivor(CustodyComponentEnvelope envelope, string tenant, CustodyOriginalManifest manifest)
    {
        var identity = envelope.Identity;
        return identity.TenantId == tenant && !manifest.Targets.Contains(identity) && !envelope.IsZeroed
            && Witness is not null && !Witness.IsDestroyed(identity) && Live(identity, envelope);
    }

    private bool ValidManifest(CustodyOriginalManifest manifest)
    {
        var targets = manifest.Targets;
        if (targets is not { Count: >= 1 and <= 1000 } || !Digest(manifest.ManifestDigest) || string.IsNullOrWhiteSpace(manifest.ReservationId)) { return false; }
        ProtectionTarget? previous = null;
        foreach (var identity in targets)
        {
            if (identity.Purpose != PlatformKeyPurpose.InteractionRootDek || identity.TenantId != targets[0].TenantId
                || Witness!.IsDestroyed(identity) || _volume.Find(identity) is not { } envelope || envelope.IsZeroed) { return false; }
            var target = new ProtectionTarget(identity.TenantId, identity.ObjectId, identity.KeyAlias);
            if (previous is not null && Compare(previous, target) >= 0) { return false; }
            previous = target;
        }
        var vector = targets.Select(i => new ProtectionTarget(i.TenantId, i.ObjectId, i.KeyAlias)).ToArray();
        return DeletionBatchCapabilityIdentity.TargetManifestDigest(vector) == manifest.ManifestDigest
            && targets.Select(i => i.ObjectId).Distinct(StringComparer.Ordinal).Count() == targets.Count;
    }

    private CustodyComponentOutcome Remember(CustodyComponentOutcome outcome)
    {
        Witness!.Remember(new CustodyWitnessFact(outcome.OperationId, outcome.RequestDigest, outcome.Status, outcome.ReceiptId, outcome.RestoreBarrierReceiptId, outcome.Identity, outcome.HoldId));
        return outcome;
    }

    private bool Ready() => !_disposed && Witness is { Available: true };

    private static CustodyComponentOutcome FromFact(CustodyWitnessFact fact)
        => new(fact.Status, fact.OperationId, fact.RequestDigest, fact.ReceiptId, fact.RestoreBarrierReceiptId, Identity: fact.Identity, HoldId: fact.HoldId);

    private static CustodyComponentOutcome Result(string operationId, string requestDigest, CustodyComponentStatus status, CustodyKeyObjectIdentity? identity,
        string? receiptId = null, string? barrier = null, IReadOnlyList<DeletionTargetReceipt>? receipts = null, string? holdId = null)
        => new(status, operationId, requestDigest, receiptId, barrier, receipts, identity, holdId);

    private static int Compare(ProtectionTarget left, ProtectionTarget right)
    {
        int tenant = StringComparer.Ordinal.Compare(left.TenantId, right.TenantId);
        int interaction = StringComparer.Ordinal.Compare(left.AgentInteractionId, right.AgentInteractionId);
        return tenant != 0 ? tenant : interaction != 0 ? interaction : StringComparer.Ordinal.Compare(left.TargetProtectionKeyAlias, right.TargetProtectionKeyAlias);
    }

    private static bool Digest(string value) => value is { Length: 64 } && value.All(c => c is >= 'A' and <= 'F' or >= '0' and <= '9');

    private void DropDek(CustodyKeyObjectIdentity identity)
    {
        if (_deks.Remove(DekKey(identity), out byte[]? dek)) { CryptographicOperations.ZeroMemory(dek); }
    }

    private static string DekKey(CustodyKeyObjectIdentity identity)
        => string.Join('\u001f', identity.TenantId, identity.ObjectId, identity.Purpose.ToString(), identity.KeyAlias, identity.KeyVersion,
            identity.LifecycleVersion, identity.StoreTarget, identity.ContractVersion);
}
