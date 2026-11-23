using Hexalith.Builds.Tooling.ReleaseRecords;

namespace Hexalith.Platform.AttemptStore;

public sealed partial class AttemptStore
{
    /// <summary>Sets a promotion stop in the verified record's environment.</summary>
    public Task<AttemptStoreCommit> SetStopAsync(ReadOnlyMemory<byte> stop, CancellationToken cancellationToken = default)
    {
        Candidate<PromotionStopRecord> candidate = VerifyCandidate<PromotionStopRecord>(stop, ReleaseRecordVersion.V1);
        return CommitAsync(candidate.Verified.Record.Environment!, "set-stop", candidate.Envelope, async (head, token) =>
        {
            VerifiedRecord<PromotionStopRecord>? previous = await ReadLastStopAsync(head, token).ConfigureAwait(false);
            long revision = head.Stop?.Revision ?? 0;
            RecordTransitionValidator.ValidatePromotionStop(candidate.Verified, revision, previous);
            return new(head with
            {
                Stop = new(candidate.Verified.Record.Payload.Revision, candidate.Envelope.Record,
                    head.Stop?.ClearedThrough ?? 0, head.Stop?.LastClear),
            });
        }, cancellationToken);
    }

    /// <summary>Clears the current stop with a signed Administrator action naming the working pointer's attempt.</summary>
    public Task<AttemptStoreCommit> ClearStopAsync(ReadOnlyMemory<byte> action, CancellationToken cancellationToken = default)
    {
        Candidate<AdministratorRecord> candidate = VerifyCandidate<AdministratorRecord>(action, ReleaseRecordVersion.V1);
        AdministratorRecord payload = candidate.Verified.Record.Payload;
        Require(payload.Action == "clear-promotion-stop");
        return CommitAsync(candidate.Verified.Record.Environment!, "clear-stop", candidate.Envelope, async (head, token) =>
        {
            AttemptStoreHead next = WithNewAction(head, candidate.Envelope);
            VerifiedRecord<PromotionStopRecord>? stop = ActiveStop(head, await ReadLastStopAsync(head, token).ConfigureAwait(false));
            Require(stop is not null && head.Stop is not null, "stop-not-set");
            VerifiedRecord<LatestWorkingPointerRecord>? pointer = await ReadWorkingPointerAsync(head, token).ConfigureAwait(false);
            Require(pointer is not null && payload.Attempt == pointer.Record.Payload.TerminalAttempt, "working-attempt-mismatch");
            VerifiedRecord<AttemptRecord> working = await ReadAttemptSlotAsync(head, payload.Attempt.RecordId, token).ConfigureAwait(false);

            VerifiedRecord<AdministratorRecord>? predecessor = null;
            if (payload.Predecessor is not null)
            {
                Require(head.Stop.LastClear is { } lastClear && lastClear.Reference == payload.Predecessor, "invalid-transition");
                predecessor = await ReadRecordAsync<AdministratorRecord>(head.Environment, head.Stop.LastClear, token).ConfigureAwait(false);
            }

            RecordTransitionValidator.ValidateAdministrator(candidate.Verified, stop, working, predecessor);
            await ValidateOutstandingCausesAsync(head, stop, payload, token).ConfigureAwait(false);
            return new(next with { Stop = head.Stop with { ClearedThrough = head.Stop.Revision, LastClear = candidate.Envelope.Record } });
        }, cancellationToken);
    }

    private static VerifiedRecord<PromotionStopRecord>? ActiveStop(AttemptStoreHead head, VerifiedRecord<PromotionStopRecord>? stop) =>
        head.Stop is { } state && state.Revision > state.ClearedThrough ? stop : null;

    private async Task<VerifiedRecord<PromotionStopRecord>?> ReadLastStopAsync(
        AttemptStoreHead head, CancellationToken cancellationToken)
    {
        if (head.Stop is not { } state)
        {
            return null;
        }

        VerifiedRecord<PromotionStopRecord> latest = await ReadRecordAsync<PromotionStopRecord>(
            head.Environment, state.Latest, cancellationToken).ConfigureAwait(false);
        Require(latest.Record.Payload.Revision == state.Revision, "invalid-head");
        if (state.LastClear is not null)
        {
            VerifiedRecord<AdministratorRecord> clear = await ReadRecordAsync<AdministratorRecord>(
                head.Environment, state.LastClear, cancellationToken).ConfigureAwait(false);
            AdministratorRecord payload = clear.Record.Payload;
            Require(payload is { Action: "clear-promotion-stop", ObservedStop: not null } &&
                payload.ExpectedStopRevision == state.ClearedThrough && payload.Target == head.Environment &&
                (state.ClearedThrough != state.Revision || payload.ObservedStop == latest.Reference), "invalid-head");
        }

        return latest;
    }

    private async Task ValidateObservedStopAsync(
        AttemptStoreHead head, RecordReference? observed, CancellationToken cancellationToken)
    {
        VerifiedRecord<PromotionStopRecord>? stop = ActiveStop(head, await ReadLastStopAsync(head, cancellationToken).ConfigureAwait(false));
        Require(observed == stop?.Reference, "stop-revision-mismatch");
    }

    private async Task ValidateOutstandingCausesAsync(
        AttemptStoreHead head, VerifiedRecord<PromotionStopRecord> stop, AdministratorRecord clearance, CancellationToken cancellationToken)
    {
        long clearedThrough = head.Stop!.ClearedThrough;
        while (true)
        {
            PromotionStopRecord payload = stop.Record.Payload;
            Require(clearance.CauseDispositions.TryGetValue(payload.Cause, out string? disposition) &&
                disposition is "resolved" or "accepted", "undisposed-stop-cause");
            if (payload.Revision == clearedThrough + 1)
            {
                return;
            }

            Require(payload.PreviousStop is not null, "invalid-head");
            VerifiedRecord<PromotionStopRecord> previous = await ReadRecordAsync<PromotionStopRecord>(
                head.Environment, payload.PreviousStop, ReleaseRecordVersion.V1, cancellationToken).ConfigureAwait(false);
            RecordTransitionValidator.ValidatePromotionStop(stop, payload.Revision - 1, previous);
            stop = previous;
        }
    }
}
