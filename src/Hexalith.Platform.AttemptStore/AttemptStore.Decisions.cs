using Hexalith.Builds.Tooling.ReleaseRecords;

namespace Hexalith.Platform.AttemptStore;

public sealed partial class AttemptStore
{
    /// <summary>Reads a pending, reserved or terminal decision from the verified staging state.</summary>
    public async Task<AttemptStoreDecision?> ReadDecisionAsync(string decisionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(decisionId);
        CurrentHead? current = await ReadHeadAsync(Staging, cancellationToken).ConfigureAwait(false);
        if (current is null || !current.Head.Decisions.TryGetValue(decisionId, out StoredDecision? decision))
        {
            return null;
        }

        return await VerifyDecisionAsync(current.Head, decisionId, decision, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Conditionally applies a v2 action in the shared staging decision head.</summary>
    public Task<AttemptStoreCommit> ApplyDecisionAsync(
        ReadOnlyMemory<byte> action, VerifyPrerequisiteReset? verifyPrerequisiteReset = null,
        CancellationToken cancellationToken = default)
    {
        Candidate<PromotionDecisionRecord> candidate = VerifyCandidate<PromotionDecisionRecord>(action, ReleaseRecordVersion.V2);
        PromotionDecisionRecord payload = candidate.Verified.Record.Payload;
        string decisionId = payload.StagingEvidence.DecisionId;
        return CommitAsync(Staging, "promotion-decision", candidate.Envelope, async (head, token) =>
        {
            AttemptStoreHead next = WithNewAction(head, candidate.Envelope);
            Require(head.Decisions.TryGetValue(decisionId, out StoredDecision? decision), "decision-missing");
            AttemptStoreDecision current = await VerifyDecisionAsync(head, decisionId, decision, token).ConfigureAwait(false);
            Require(current.Terminal is null, "decision-terminal");
            VerifiedRecord<ReleaseRecord> release = await ReadRecordAsync<ReleaseRecord>(
                Staging, current.Release, ReleaseRecordVersion.V1, token).ConfigureAwait(false);

            StoredRecord stored = candidate.Envelope.Record;
            StoredDecision updated;
            string? productionETag = null;
            switch (payload.Action)
            {
                case "reserve-administrator":
                    Require(current.AdministratorReservation is null, "decision-reserved");
                    RecordTransitionValidator.ValidatePromotionDecision(candidate.Verified, current.Binding, release);
                    updated = decision with { AdministratorReservation = stored };
                    break;
                case "supersede":
                    Require(current.Reservation is null && current.AdministratorReservation is null, "decision-reserved");
                    await ValidateSupersessionAsync(head, candidate.Verified, current, release, verifyPrerequisiteReset, token).ConfigureAwait(false);
                    updated = decision with { Terminal = stored };
                    break;
                case "reserve-admission":
                case "complete":
                case "release-reservation":
                    CurrentHead? production = await ReadHeadAsync(Production, token).ConfigureAwait(false);
                    Require(production is not null, "head-missing");
                    updated = await ValidateProductionDecisionAsync(
                        production.Head, candidate, decision, current, release, token).ConfigureAwait(false);
                    productionETag = production.ETag;
                    break;
                default:
                    throw new RecordValidationException("invalid-transition");
            }

            return new(next with { Decisions = next.Decisions.SetItem(decisionId, updated) }, productionETag);
        }, cancellationToken);
    }

    private async Task ValidateSupersessionAsync(
        AttemptStoreHead head, VerifiedRecord<PromotionDecisionRecord> action, AttemptStoreDecision decision,
        VerifiedRecord<ReleaseRecord> release, VerifyPrerequisiteReset? verifyPrerequisiteReset, CancellationToken cancellationToken)
    {
        PromotionDecisionRecord payload = action.Record.Payload;
        ValidateCurrentEpoch(head, payload.LockEpoch!.Value);
        VerifiedRecord<AttemptRecord>? holder = await ReadHolderAsync(head, cancellationToken).ConfigureAwait(false);
        Require(holder?.Reference == payload.Attempt && holder is not null, "lock-not-held");
        RecordTransitionValidator.ValidatePromotionDecision(action, decision.Binding, release, holder, head.Epoch);

        AttemptRecord initiating = holder.Record.Payload;
        StagingEvidenceBinding? binding = initiating.StagingEvidence;
        Require(binding is not null, "decision-binding-conflict");
        if (binding.DecisionId != decision.Binding.DecisionId)
        {
            Require(head.Decisions.TryGetValue(binding.DecisionId, out StoredDecision? initiatingSlot), "decision-binding-conflict");
            AttemptStoreDecision initiatingDecision = await VerifyDecisionAsync(
                head, binding.DecisionId, initiatingSlot, cancellationToken).ConfigureAwait(false);
            Require(initiatingDecision.Binding == binding && initiatingDecision.Release == initiating.Release &&
                initiatingDecision.Terminal is null, "decision-binding-conflict");
        }

        if (initiating.AttemptKind == "in-place-restore")
        {
            VerifiedRecord<ReleaseRecord> initiatingRelease = await ReadRecordAsync<ReleaseRecord>(
                Staging, initiating.Release, ReleaseRecordVersion.V1, cancellationToken).ConfigureAwait(false);
            Require(verifyPrerequisiteReset is not null && await verifyPrerequisiteReset(
                holder, initiatingRelease, release, decision.Binding, cancellationToken).ConfigureAwait(false), "unverified-prerequisite-reset");
        }
    }

    private async Task<StoredDecision> ValidateProductionDecisionAsync(
        AttemptStoreHead production, Candidate<PromotionDecisionRecord> action, StoredDecision decision,
        AttemptStoreDecision current, VerifiedRecord<ReleaseRecord> release, CancellationToken cancellationToken)
    {
        PromotionDecisionRecord payload = action.Verified.Record.Payload;
        ValidateCurrentEpoch(production, payload.LockEpoch!.Value);
        if (payload.Action == "reserve-admission")
        {
            Require(current.Reservation is null, "decision-reserved");
            VerifiedRecord<AttemptRecord>? holder = await ReadHolderAsync(production, cancellationToken).ConfigureAwait(false);
            Require(holder?.Reference == payload.Attempt && holder is not null, "lock-not-held");
            RecordTransitionValidator.ValidatePromotionDecision(action.Verified, current.Binding, release, holder, production.Epoch);
            return decision with { Reservation = action.Envelope.Record };
        }

        VerifiedRecord<PromotionDecisionRecord>? reservation = current.Reservation;
        Require(reservation is not null && payload.Reservation == reservation.Reference, "reservation-mismatch");
        VerifiedRecord<AttemptRecord> terminal = await ReadAttemptSlotAsync(
            production, payload.Attempt!.RecordId, cancellationToken).ConfigureAwait(false);
        VerifiedRecord<AttemptRecord> admitted = await ReadRecordAsync<AttemptRecord>(
            Production, reservation.Record.Payload.Attempt!, ReleaseRecordVersion.V2, cancellationToken).ConfigureAwait(false);
        VerifiedRecord<LatestWorkingPointerRecord>? pointer = payload.Action == "complete"
            ? await ReadWorkingPointerAsync(production, cancellationToken).ConfigureAwait(false)
            : null;

        // An interrupted or superseded attempt cannot close the reservation under the Builds contract.
        RecordTransitionValidator.ValidatePromotionDecision(
            action.Verified, current.Binding, release, terminal, production.Epoch, reservation, admitted, pointer);
        return payload.Action == "complete"
            ? decision with { Terminal = action.Envelope.Record }
            : decision with { Reservation = null };
    }

    private async Task<AttemptStoreDecision> VerifyDecisionAsync(
        AttemptStoreHead head, string decisionId, StoredDecision decision, CancellationToken cancellationToken)
    {
        Require(decision is not null && decision.Binding?.DecisionId == decisionId && decision.Origin is not null, "invalid-head");
        VerifiedRecord<AttemptRecord> origin = await ReadRecordAsync<AttemptRecord>(
            Staging, decision.Origin, cancellationToken).ConfigureAwait(false);
        VerifiedRecord<ReleaseRecord> release = await ReadRecordAsync<ReleaseRecord>(
            Staging, decision.Release, ReleaseRecordVersion.V1, cancellationToken).ConfigureAwait(false);
        string originKey = StoredRecord.Key(Staging, "attempt", origin.Reference);
        Require(origin.Record.Payload.StagingEvidence == decision.Binding && origin.Record.Payload.Release == release.Reference &&
            origin.Record.Subject == release.Record.Payload.ReleaseId && head.Commits.TryGetValue(originKey, out CommittedTransition? originCommit) && originCommit is not null &&
            originCommit.Primary == decision.Origin && originCommit.Operation is "acquire" or "takeover", "invalid-head");

        VerifiedRecord<PromotionDecisionRecord>? reservation = await ReadDecisionActionAsync(head, decision.Reservation, release, decision.Binding, cancellationToken).ConfigureAwait(false);
        VerifiedRecord<PromotionDecisionRecord>? administrator = await ReadDecisionActionAsync(head, decision.AdministratorReservation, release, decision.Binding, cancellationToken).ConfigureAwait(false);
        VerifiedRecord<PromotionDecisionRecord>? terminal = await ReadDecisionActionAsync(head, decision.Terminal, release, decision.Binding, cancellationToken).ConfigureAwait(false);
        Require((reservation is null || reservation.Record.Payload.Action == "reserve-admission") &&
            (administrator is null || administrator.Record.Payload.Action == "reserve-administrator") &&
            (terminal is null || terminal.Record.Payload.Action switch
            {
                "complete" => reservation is not null && terminal.Record.Payload.Reservation == reservation.Reference,
                "supersede" => reservation is null && administrator is null,
                _ => false,
            }), "invalid-head");

        return new(decision.Binding, release.Reference, reservation, administrator, terminal);
    }

    private async Task<VerifiedRecord<PromotionDecisionRecord>?> ReadDecisionActionAsync(
        AttemptStoreHead head, StoredRecord? action, VerifiedRecord<ReleaseRecord> release,
        StagingEvidenceBinding binding, CancellationToken cancellationToken)
    {
        if (action is null)
        {
            return null;
        }

        VerifiedRecord<PromotionDecisionRecord> verified = await ReadRecordAsync<PromotionDecisionRecord>(
            Staging, action, cancellationToken).ConfigureAwait(false);
        string key = StoredRecord.Key(Staging, "promotion-decision", verified.Reference);
        Require(verified.Record.Payload.Release == release.Reference && verified.Record.Payload.StagingEvidence == binding &&
            verified.Record.Subject == release.Record.Payload.ReleaseId && head.Commits.TryGetValue(key, out CommittedTransition? commit) && commit is not null &&
            commit.Primary == action && commit.Operation == "promotion-decision", "invalid-head");
        return verified;
    }
}
