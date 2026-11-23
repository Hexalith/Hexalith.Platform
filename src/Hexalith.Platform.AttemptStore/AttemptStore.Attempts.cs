using Hexalith.Builds.Tooling.ReleaseRecords;

namespace Hexalith.Platform.AttemptStore;

public sealed partial class AttemptStore
{
    /// <summary>Acquires a free environment lock at the signed next epoch.</summary>
    public Task<AttemptStoreCommit> AcquireAsync(
        ReadOnlyMemory<byte> attempt, ReadOnlyMemory<byte> release,
        ReleaseRecordVersion version = ReleaseRecordVersion.V1, CancellationToken cancellationToken = default)
    {
        Candidate<AttemptRecord> candidate = VerifyCandidate<AttemptRecord>(attempt, version);
        Candidate<ReleaseRecord> retained = VerifyCandidate<ReleaseRecord>(release, ReleaseRecordVersion.V1);
        SignedRecord<AttemptRecord> record = candidate.Verified.Record;
        AttemptRecord payload = record.Payload;
        Require(payload is { Outcome: "in-progress", Supersedes: null, FirstMutationUtc: null });
        Require(payload.Release == retained.Verified.Reference);
        return CommitAsync(record.Environment!, "acquire", candidate.Envelope, async (head, token) =>
        {
            Require(await ReadHolderAsync(head, token).ConfigureAwait(false) is null, "lock-held");
            Require(!head.Attempts.ContainsKey(record.RecordId), "record-id-conflict");
            ValidateNextEpoch(head, payload.LockEpoch);
            await ValidateObservedStopAsync(head, payload.ObservedStop, token).ConfigureAwait(false);
            RecordTransitionValidator.ValidateAttempt(candidate.Verified, retained.Verified, payload.LockEpoch);
            return new(await WithNewHolderAsync(head, candidate, token).ConfigureAwait(false));
        }, cancellationToken, retainedRelease: retained.Envelope, activeReplayEpoch: payload.LockEpoch);
    }

    /// <summary>Revises the currently held attempt. A terminal revision releases the lock.</summary>
    public Task<AttemptStoreCommit> ReviseAsync(
        ReadOnlyMemory<byte> attempt, ReleaseRecordVersion version = ReleaseRecordVersion.V1,
        CancellationToken cancellationToken = default)
    {
        Candidate<AttemptRecord> candidate = VerifyCandidate<AttemptRecord>(attempt, version);
        SignedRecord<AttemptRecord> record = candidate.Verified.Record;
        AttemptRecord payload = record.Payload;
        Require(payload.Outcome != "superseded");
        bool terminal = payload.Outcome != "in-progress";
        return CommitAsync(record.Environment!, "revise", candidate.Envelope, async (head, token) =>
        {
            ValidateCurrentEpoch(head, payload.LockEpoch);
            VerifiedRecord<AttemptRecord> previous = await ReadAttemptSlotAsync(head, record.RecordId, token).ConfigureAwait(false);
            Require(previous.Record.Payload.Outcome == "in-progress", "terminal-attempt");
            Require(head.Holder == record.RecordId, "lock-not-held");
            VerifiedRecord<ReleaseRecord> release = await ReadRecordAsync<ReleaseRecord>(
                head.Environment, payload.Release, ReleaseRecordVersion.V1, token).ConfigureAwait(false);
            RecordTransitionValidator.ValidateAttempt(candidate.Verified, release, head.Epoch, previous);
            return new(head with
            {
                Holder = terminal ? null : head.Holder,
                Attempts = head.Attempts.SetItem(record.RecordId, new(candidate.Envelope.Record, terminal)),
            });
        }, cancellationToken, activeReplayEpoch: terminal ? null : payload.LockEpoch);
    }

    /// <summary>Atomically installs a linked successor and the successor-signed final revision of its predecessor.</summary>
    public Task<AttemptStoreCommit> TakeoverAsync(
        ReadOnlyMemory<byte> successor, ReadOnlyMemory<byte> predecessorFinal, ReadOnlyMemory<byte> successorRelease,
        ReleaseRecordVersion successorVersion = ReleaseRecordVersion.V1,
        ReleaseRecordVersion predecessorVersion = ReleaseRecordVersion.V1,
        CancellationToken cancellationToken = default)
    {
        Candidate<AttemptRecord> next = VerifyCandidate<AttemptRecord>(successor, successorVersion);
        Candidate<AttemptRecord> final = VerifyCandidate<AttemptRecord>(predecessorFinal, predecessorVersion);
        Candidate<ReleaseRecord> retained = VerifyCandidate<ReleaseRecord>(successorRelease, ReleaseRecordVersion.V1);
        SignedRecord<AttemptRecord> successorRecord = next.Verified.Record;
        SignedRecord<AttemptRecord> finalRecord = final.Verified.Record;
        AttemptRecord payload = successorRecord.Payload;
        Require(finalRecord.Environment == successorRecord.Environment, "environment-mismatch");
        Require(payload is { Outcome: "in-progress", Supersedes: not null, FirstMutationUtc: null } &&
            finalRecord.Payload.Outcome == "superseded" && retained.Verified.Reference == payload.Release);
        return CommitAsync(successorRecord.Environment!, "takeover", next.Envelope, async (head, token) =>
        {
            ValidateNextEpoch(head, payload.LockEpoch);
            VerifiedRecord<AttemptRecord>? previous = await ReadHolderAsync(head, token).ConfigureAwait(false);
            Require(previous is not null && previous.Record.RecordId == finalRecord.RecordId, "lock-not-held");
            Require(!head.Attempts.ContainsKey(successorRecord.RecordId), "record-id-conflict");
            await ValidateObservedStopAsync(head, payload.ObservedStop, token).ConfigureAwait(false);
            VerifiedRecord<ReleaseRecord> previousRelease = await ReadRecordAsync<ReleaseRecord>(
                head.Environment, previous.Record.Payload.Release, ReleaseRecordVersion.V1, token).ConfigureAwait(false);
            RecordTransitionValidator.ValidateAttempt(next.Verified, retained.Verified, payload.LockEpoch);
            RecordTransitionValidator.ValidateAttempt(final.Verified, previousRelease, payload.LockEpoch, previous, next.Verified);

            AttemptStoreHead acquired = await WithNewHolderAsync(head, next, token).ConfigureAwait(false);
            return new(acquired with
            {
                Attempts = acquired.Attempts.SetItem(finalRecord.RecordId, new(final.Envelope.Record, Terminal: true)),
            });
        }, cancellationToken, companion: final.Envelope, retainedRelease: retained.Envelope, activeReplayEpoch: payload.LockEpoch);
    }

    /// <summary>Moves the environment's working pointer to its exact current-epoch working attempt.</summary>
    public Task<AttemptStoreCommit> MoveLatestWorkingAsync(ReadOnlyMemory<byte> pointer, CancellationToken cancellationToken = default)
    {
        Candidate<LatestWorkingPointerRecord> candidate = VerifyCandidate<LatestWorkingPointerRecord>(pointer, ReleaseRecordVersion.V1);
        LatestWorkingPointerRecord payload = candidate.Verified.Record.Payload;
        return CommitAsync(candidate.Verified.Record.Environment!, "move-latest-working", candidate.Envelope, async (head, token) =>
        {
            ValidateCurrentEpoch(head, payload.Epoch);
            VerifiedRecord<LatestWorkingPointerRecord>? previous = await ReadWorkingPointerAsync(head, token).ConfigureAwait(false);
            VerifiedRecord<AttemptRecord> attempt = await ReadAttemptSlotAsync(head, payload.TerminalAttempt.RecordId, token).ConfigureAwait(false);
            VerifiedRecord<ReleaseRecord> release = await ReadRecordAsync<ReleaseRecord>(
                head.Environment, payload.Release, ReleaseRecordVersion.V1, token).ConfigureAwait(false);
            RecordTransitionValidator.ValidateLatestWorking(candidate.Verified, release, attempt, head.Epoch, previous);
            return new(head with { LatestWorking = candidate.Envelope.Record });
        }, cancellationToken);
    }

    private async Task<AttemptStoreHead> WithNewHolderAsync(
        AttemptStoreHead head, Candidate<AttemptRecord> candidate, CancellationToken cancellationToken)
    {
        SignedRecord<AttemptRecord> record = candidate.Verified.Record;
        AttemptRecord payload = record.Payload;
        AttemptStoreHead next = head with
        {
            Epoch = payload.LockEpoch,
            Holder = record.RecordId,
            Attempts = head.Attempts.Add(record.RecordId, new(candidate.Envelope.Record, Terminal: false)),
        };
        if (head.Environment != Staging || payload.StagingEvidence is not { } binding)
        {
            return next;
        }

        if (head.Decisions.TryGetValue(binding.DecisionId, out StoredDecision? decision))
        {
            AttemptStoreDecision current = await VerifyDecisionAsync(head, binding.DecisionId, decision, cancellationToken).ConfigureAwait(false);
            Require(current.Binding == binding && current.Release == payload.Release, "decision-binding-conflict");
            Require(current.Terminal is null, "decision-terminal");
            return next;
        }

        // The staging intake caller supplies the pending binding on the first signed attempt.
        foreach ((string decisionId, StoredDecision previous) in head.Decisions)
        {
            if (previous.Release.RecordId == payload.Release.RecordId)
            {
                AttemptStoreDecision prior = await VerifyDecisionAsync(head, decisionId, previous, cancellationToken).ConfigureAwait(false);
                Require(prior.Release == payload.Release && prior.Terminal is not null, "decision-binding-conflict");
            }
        }

        return next with
        {
            Decisions = head.Decisions.Add(binding.DecisionId, new(candidate.Envelope.Record, binding, payload.Release, null, null, null)),
        };
    }

    private async Task<VerifiedRecord<LatestWorkingPointerRecord>?> ReadWorkingPointerAsync(
        AttemptStoreHead head, CancellationToken cancellationToken)
    {
        if (head.LatestWorking is null)
        {
            return null;
        }

        VerifiedRecord<LatestWorkingPointerRecord> pointer = await ReadRecordAsync<LatestWorkingPointerRecord>(
            head.Environment, head.LatestWorking, cancellationToken).ConfigureAwait(false);
        LatestWorkingPointerRecord payload = pointer.Record.Payload;
        Require(payload.Epoch <= head.Epoch, "invalid-head");
        VerifiedRecord<AttemptRecord> attempt = await ReadAttemptSlotAsync(head, payload.TerminalAttempt.RecordId, cancellationToken).ConfigureAwait(false);
        VerifiedRecord<ReleaseRecord> release = await ReadRecordAsync<ReleaseRecord>(
            head.Environment, payload.Release, ReleaseRecordVersion.V1, cancellationToken).ConfigureAwait(false);
        RecordTransitionValidator.ValidateLatestWorking(pointer, release, attempt, payload.Epoch);
        return pointer;
    }

    private static void ValidateNextEpoch(AttemptStoreHead head, long epoch)
    {
        Require(epoch > head.Epoch, "stale-epoch");
        Require(head.Epoch < long.MaxValue && epoch == head.Epoch + 1, "invalid-epoch");
    }

    private static void ValidateCurrentEpoch(AttemptStoreHead head, long epoch)
    {
        Require(epoch >= head.Epoch, "stale-epoch");
        Require(epoch == head.Epoch, "invalid-epoch");
    }
}
