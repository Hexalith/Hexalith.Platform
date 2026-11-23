using Hexalith.Builds.Tooling.ReleaseRecords;

namespace Hexalith.Platform.AttemptStore;

/// <summary>Serializes signed operational changes through one object-store head per environment.</summary>
public sealed partial class AttemptStore
{
    public const string Staging = "staging";
    public const string Production = "production";

    private const int MaximumCommitAttempts = 16;

    private readonly IAttemptObjectStore _objects;
    private readonly RecordTrustPolicy _trust;

    public AttemptStore(IAttemptObjectStore objects, RecordTrustPolicy trust)
    {
        _objects = objects ?? throw new ArgumentNullException(nameof(objects));
        _trust = trust ?? throw new ArgumentNullException(nameof(trust));
    }

    /// <summary>Reads current verified state. A missing head returns null.</summary>
    public async Task<AttemptStoreState?> ReadAsync(string environment, CancellationToken cancellationToken = default)
    {
        CurrentHead? current = await ReadHeadAsync(environment, cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            return null;
        }

        AttemptStoreHead head = current.Head;
        VerifiedRecord<AttemptRecord>? holder = await ReadHolderAsync(head, cancellationToken).ConfigureAwait(false);
        VerifiedRecord<LatestWorkingPointerRecord>? pointer = await ReadWorkingPointerAsync(head, cancellationToken).ConfigureAwait(false);
        VerifiedRecord<PromotionStopRecord>? stop = await ReadLastStopAsync(head, cancellationToken).ConfigureAwait(false);
        return new(environment, head.Revision, head.Epoch, holder, pointer, ActiveStop(head, stop), head.Stop?.Revision ?? 0);
    }

    /// <summary>Reads the authoritative signed revision of an attempt by identity.</summary>
    public async Task<VerifiedRecord<AttemptRecord>?> ReadAttemptAsync(
        string environment, string attemptId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(attemptId);
        CurrentHead? current = await ReadHeadAsync(environment, cancellationToken).ConfigureAwait(false);
        if (current is null || !current.Head.Attempts.ContainsKey(attemptId))
        {
            return null;
        }

        return await ReadAttemptSlotAsync(current.Head, attemptId, cancellationToken).ConfigureAwait(false);
    }

    private static string HeadKey(string environment)
    {
        Require(environment is Staging or Production, "invalid-environment");
        return $"environments/{environment}/head.json";
    }

    private static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition, string code = "invalid-transition")
    {
        if (!condition)
        {
            throw new RecordValidationException(code);
        }
    }

    private Candidate<T> VerifyCandidate<T>(ReadOnlyMemory<byte> input, ReleaseRecordVersion version)
        where T : class
    {
        Require(input.Length <= ReleaseRecordCodec.MaximumRecordBytes, "record-too-large");
        byte[] bytes = input.ToArray();
        VerifiedRecord<T> record = Verify<T>(bytes, version);
        return new(record, new Envelope(
            record.Record.Kind,
            bytes,
            new StoredRecord(record.Reference, version, StoredRecord.Digest(bytes))));
    }

    private VerifiedRecord<T> Verify<T>(byte[] bytes, ReleaseRecordVersion version)
        where T : class
    {
        RecordVerificationResult<T> result = ReleaseRecordCodec.Verify<T>(bytes, _trust, version);
        return result.Verified ?? throw new RecordValidationException(result.FailureCode ?? "invalid-record");
    }

    private async Task<CurrentHead?> ReadHeadAsync(string environment, CancellationToken cancellationToken)
    {
        AttemptObject? value = await _objects.ReadAsync(HeadKey(environment), AttemptStoreHead.MaximumBytes, cancellationToken).ConfigureAwait(false);
        if (value is null)
        {
            return null;
        }

        Require(value.Bytes.Length <= AttemptStoreHead.MaximumBytes && !string.IsNullOrWhiteSpace(value.ETag), "invalid-head");
        return new(AttemptStoreHead.Decode(value.Bytes, environment), value.ETag, StoredRecord.Digest(value.Bytes));
    }

    private async Task<AttemptStoreCommit> CommitAsync(
        string environment,
        string operation,
        Envelope primary,
        Func<AttemptStoreHead, CancellationToken, Task<ProposedHead>> prepare,
        CancellationToken cancellationToken,
        Envelope? companion = null,
        Envelope? retainedRelease = null,
        long? activeReplayEpoch = null)
    {
        string key = HeadKey(environment);
        string commitKey = StoredRecord.Key(environment, primary.Kind, primary.Record.Reference);
        CurrentHead? current = await ReadHeadAsync(environment, cancellationToken).ConfigureAwait(false);
        for (int retry = 0; ; retry++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AttemptStoreHead head = current?.Head ?? AttemptStoreHead.Empty(environment);
            if (head.Commits.TryGetValue(commitKey, out CommittedTransition? committed))
            {
                Require(activeReplayEpoch is null || activeReplayEpoch == head.Epoch, "stale-epoch");
                return await ReconcileAsync(head, operation, committed, primary, companion, retainedRelease, cancellationToken).ConfigureAwait(false);
            }

            Require(retry < MaximumCommitAttempts, "contention");
            ProposedHead prepared = await prepare(head, cancellationToken).ConfigureAwait(false);
            Require(head.Revision < long.MaxValue, "revision-exhausted");
            if (current is null)
            {
                // Create the empty head once. Every operational transition uses an observed head ETag.
                try
                {
                    await _objects.CreateAsync(key, head.Encode(), cancellationToken).ConfigureAwait(false);
                }
                catch (AttemptStoreWriteUncertainException exception)
                {
                    current = await ReadAfterUncertainWriteAsync(environment, exception, cancellationToken).ConfigureAwait(false);
                    if (current is null)
                    {
                        throw;
                    }

                    continue;
                }

                current = await ReadHeadAsync(environment, cancellationToken).ConfigureAwait(false);
                continue;
            }

            CommittedTransition receipt = new(operation, head.Revision + 1, prepared.Head.Epoch, primary.Record, companion?.Record);
            AttemptStoreHead next = prepared.Head with
            {
                Revision = receipt.Revision,
                Previous = current.Digest,
                Commits = head.Commits.Add(commitKey, receipt),
            };
            byte[] encoded = next.Encode();
            if (retainedRelease is not null)
            {
                await ArchiveAsync(environment, retainedRelease, cancellationToken).ConfigureAwait(false);
            }

            await ArchiveAsync(environment, primary, cancellationToken).ConfigureAwait(false);
            if (companion is not null)
            {
                await ArchiveAsync(environment, companion, cancellationToken).ConfigureAwait(false);
            }

            // Production readers are checked again at the decision commit boundary. The only write is to staging.
            if (prepared.ProductionETag is { } productionETag &&
                (await ReadHeadAsync(Production, cancellationToken).ConfigureAwait(false))?.ETag != productionETag)
            {
                current = await ReadHeadAsync(environment, cancellationToken).ConfigureAwait(false);
                Require(current is not null, "head-missing");
                continue;
            }

            string expectedETag = current.ETag;
            try
            {
                if (await _objects.ReplaceAsync(key, expectedETag, encoded, cancellationToken).ConfigureAwait(false))
                {
                    return ToResult(environment, receipt);
                }
            }
            catch (AttemptStoreWriteUncertainException exception)
            {
                current = await ReadAfterUncertainWriteAsync(environment, exception, cancellationToken).ConfigureAwait(false);
                if (current is null || current.ETag == expectedETag)
                {
                    throw;
                }

                if (current.Head.Commits.TryGetValue(commitKey, out committed))
                {
                    return await ReconcileAsync(current.Head, operation, committed, primary, companion, retainedRelease, cancellationToken).ConfigureAwait(false);
                }

                continue;
            }

            current = await ReadHeadAsync(environment, cancellationToken).ConfigureAwait(false);
            Require(current is not null, "head-missing");
        }
    }

    private async Task<CurrentHead?> ReadAfterUncertainWriteAsync(
        string environment, AttemptStoreWriteUncertainException original, CancellationToken cancellationToken)
    {
        try
        {
            return await ReadHeadAsync(environment, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            throw new AttemptStoreWriteUncertainException(new AggregateException(original, exception));
        }
    }

    private async Task<AttemptStoreCommit> ReconcileAsync(
        AttemptStoreHead head, string operation, CommittedTransition committed, Envelope primary, Envelope? companion,
        Envelope? retainedRelease, CancellationToken cancellationToken)
    {
        Require(committed is not null && committed.Revision > 0 && committed.Revision <= head.Revision &&
            committed.Epoch >= 0 && committed.Epoch <= head.Epoch, "invalid-head");
        Require(committed.Operation == operation && committed.Primary == primary.Record && committed.Companion == companion?.Record,
            "record-content-conflict");
        await RequireArchivedBytesAsync(head.Environment, primary, cancellationToken).ConfigureAwait(false);
        if (companion is not null)
        {
            await RequireArchivedBytesAsync(head.Environment, companion, cancellationToken).ConfigureAwait(false);
        }

        if (retainedRelease is not null)
        {
            await RequireArchivedBytesAsync(head.Environment, retainedRelease, cancellationToken).ConfigureAwait(false);
        }

        return ToResult(head.Environment, committed);
    }

    private static AttemptStoreCommit ToResult(string environment, CommittedTransition committed) =>
        new(environment, committed.Revision, committed.Epoch, committed.Primary.Reference, committed.Companion?.Reference);

    private async Task ArchiveAsync(string environment, Envelope envelope, CancellationToken cancellationToken)
    {
        string key = StoredRecord.Key(environment, envelope.Kind, envelope.Record.Reference);
        // Existing releases and replayed objects avoid an unnecessary write request.
        AttemptObject? existing = await _objects.ReadAsync(key, ReleaseRecordCodec.MaximumRecordBytes, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            try
            {
                if (await _objects.CreateAsync(key, envelope.Bytes, cancellationToken).ConfigureAwait(false))
                {
                    return;
                }
            }
            catch (AttemptStoreWriteUncertainException)
            {
                existing = await ReadArchiveProofAsync(key, cancellationToken).ConfigureAwait(false);
                if (existing is null)
                {
                    throw;
                }
            }

            existing ??= await _objects.ReadAsync(key, ReleaseRecordCodec.MaximumRecordBytes, cancellationToken).ConfigureAwait(false);
        }

        Require(existing is not null && existing.Bytes.AsSpan().SequenceEqual(envelope.Bytes), "record-content-conflict");
    }

    private async Task RequireArchivedBytesAsync(string environment, Envelope envelope, CancellationToken cancellationToken)
    {
        string key = StoredRecord.Key(environment, envelope.Kind, envelope.Record.Reference);
        AttemptObject existing = await ReadArchiveProofAsync(key, cancellationToken).ConfigureAwait(false) ??
            throw new AttemptStoreWriteUncertainException(new RecordValidationException("record-missing"));
        Require(existing.Bytes.AsSpan().SequenceEqual(envelope.Bytes), "record-content-conflict");
    }

    private async Task<AttemptObject?> ReadArchiveProofAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            return await _objects.ReadAsync(key, ReleaseRecordCodec.MaximumRecordBytes, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            throw new AttemptStoreWriteUncertainException(exception);
        }
    }

    private Task<VerifiedRecord<T>> ReadRecordAsync<T>(string environment, StoredRecord record, CancellationToken cancellationToken)
        where T : class
    {
        Require(record is not null && StoredRecord.IsDigest(record.EnvelopeDigest), "invalid-head");
        return ReadRecordAsync<T>(environment, record.Reference, record.Version, cancellationToken, record.EnvelopeDigest);
    }

    private async Task<VerifiedRecord<T>> ReadRecordAsync<T>(
        string environment, RecordReference reference, ReleaseRecordVersion version, CancellationToken cancellationToken, string? envelopeDigest = null)
        where T : class
    {
        string key = StoredRecord.Key(environment, ReleaseRecordCodec.KindFor<T>(), reference);
        AttemptObject? value = await _objects.ReadAsync(key, ReleaseRecordCodec.MaximumRecordBytes, cancellationToken).ConfigureAwait(false);
        Require(value is not null, "record-missing");
        Require(envelopeDigest is null || envelopeDigest == StoredRecord.Digest(value.Bytes), "record-content-conflict");
        VerifiedRecord<T> verified = Verify<T>(value.Bytes, version);
        Require(verified.Reference == reference && (typeof(T) == typeof(ReleaseRecord) || verified.Record.Environment == environment ||
            (typeof(T) == typeof(PromotionDecisionRecord) && environment == Staging)), "record-reference-mismatch");
        return verified;
    }

    private async Task<VerifiedRecord<AttemptRecord>?> ReadHolderAsync(AttemptStoreHead head, CancellationToken cancellationToken)
    {
        if (head.Holder is null)
        {
            return null;
        }

        VerifiedRecord<AttemptRecord> verified = await ReadAttemptSlotAsync(head, head.Holder, cancellationToken).ConfigureAwait(false);
        Require(verified.Record.Payload is { Outcome: "in-progress" } && verified.Record.Payload.LockEpoch == head.Epoch, "invalid-head");
        return verified;
    }

    private async Task<VerifiedRecord<AttemptRecord>> ReadAttemptSlotAsync(
        AttemptStoreHead head, string attemptId, CancellationToken cancellationToken)
    {
        Require(head.Attempts.TryGetValue(attemptId, out StoredAttempt? slot) && slot is not null, "attempt-missing");
        VerifiedRecord<AttemptRecord> attempt = await ReadRecordAsync<AttemptRecord>(head.Environment, slot.Record, cancellationToken).ConfigureAwait(false);
        Require(attempt.Record.RecordId == attemptId && attempt.Record.Payload.LockEpoch <= head.Epoch &&
            slot.Terminal == (attempt.Record.Payload.Outcome != "in-progress") &&
            (slot.Terminal || head.Holder == attemptId), "invalid-head");

        VerifiedRecord<ReleaseRecord> release = await ReadRecordAsync<ReleaseRecord>(
            head.Environment, attempt.Record.Payload.Release, ReleaseRecordVersion.V1, cancellationToken).ConfigureAwait(false);
        Require(attempt.Record.Subject == release.Record.Payload.ReleaseId, "record-reference-mismatch");
        return attempt;
    }

    private static AttemptStoreHead WithNewAction(AttemptStoreHead head, Envelope action)
    {
        RecordReference reference = action.Record.Reference;
        string identity = action.Kind + "/" + reference.RecordId;
        Require(!head.ActionIds.ContainsKey(identity), "record-id-conflict");
        return head with { ActionIds = head.ActionIds.Add(identity, reference) };
    }

    private sealed record Envelope(string Kind, byte[] Bytes, StoredRecord Record);
    private sealed record Candidate<T>(VerifiedRecord<T> Verified, Envelope Envelope) where T : class;
    private sealed record CurrentHead(AttemptStoreHead Head, string ETag, string Digest);
    private sealed record ProposedHead(AttemptStoreHead Head, string? ProductionETag = null);
}
