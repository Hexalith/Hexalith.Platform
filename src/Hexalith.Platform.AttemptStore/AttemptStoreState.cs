using Hexalith.Builds.Tooling.ReleaseRecords;

namespace Hexalith.Platform.AttemptStore;

/// <summary>The original committed effect. A replay returns the same receipt even after other writes.</summary>
public sealed record AttemptStoreCommit(
    string Environment,
    long Revision,
    long Epoch,
    RecordReference Record,
    RecordReference? Companion);

/// <summary>A verified view of the current lock, working pointer and promotion stop.</summary>
public sealed record AttemptStoreState(
    string Environment,
    long Revision,
    long Epoch,
    VerifiedRecord<AttemptRecord>? Holder,
    VerifiedRecord<LatestWorkingPointerRecord>? LatestWorking,
    VerifiedRecord<PromotionStopRecord>? Stop,
    long StopRevision);

/// <summary>The verified committed actions for a promotion decision.</summary>
public sealed record AttemptStoreDecision(
    StagingEvidenceBinding Binding,
    RecordReference Release,
    VerifiedRecord<PromotionDecisionRecord>? Reservation,
    VerifiedRecord<PromotionDecisionRecord>? AdministratorReservation,
    VerifiedRecord<PromotionDecisionRecord>? Terminal)
{
    public string State => Terminal?.Record.Payload.Action switch
    {
        "complete" => "completed",
        "supersede" => "superseded",
        _ when Reservation is not null || AdministratorReservation is not null => "reserved",
        _ => "pending",
    };
}

/// <summary>Independently verified proof that the exact reset is a prerequisite of the conflicting staging start.</summary>
/// <remarks>Invoke the caller's staging preparation policy against these verified current bindings on each CAS attempt.</remarks>
public delegate Task<bool> VerifyPrerequisiteReset(
    VerifiedRecord<AttemptRecord> reset,
    VerifiedRecord<ReleaseRecord> initiatingRelease,
    VerifiedRecord<ReleaseRecord> decisionRelease,
    StagingEvidenceBinding decision,
    CancellationToken cancellationToken);
