using Hexalith.Builds.Tooling.ReleaseRecords;

namespace Hexalith.Platform.AttemptStore.Tests;

public sealed class PromotionDecisionTests
{
    private const string Staging = AttemptStore.Staging;
    private const string Production = AttemptStore.Production;
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ProductionReservationAndStagingSupersessionRaceOnOneHead()
    {
        using RecordFixture fixture = new();
        InMemoryAttemptObjectStore backend = new();
        AttemptStore staging = new(backend, fixture.Trust);
        AttemptStore production = new(backend, fixture.Trust);
        DecisionScenario scenario = await PrepareAsync(fixture, staging);
        SignedBytes<PromotionDecisionRecord> wrongWriter = fixture.Decision(
            "wrong-writer", "supersede", scenario.Binding, scenario.Release, scenario.StagingInitiator, writer: "staging-successor");
        SignedBytes<PromotionDecisionRecord> stale = fixture.Decision(
            "stale", "supersede", scenario.Binding, scenario.Release, scenario.StagingInitiator, epoch: 1);
        int writes = backend.Writes;
        await AttemptStoreTests.RejectAsync("invalid-transition", () => staging.ApplyDecisionAsync(wrongWriter.Bytes, cancellationToken: Token));
        await AttemptStoreTests.RejectAsync("stale-epoch", () => staging.ApplyDecisionAsync(stale.Bytes, cancellationToken: Token));
        Assert.Equal(writes, backend.Writes);

        WaitForTwoWriters race = new(AttemptStoreTests.StagingHead);
        backend.BeforeReplace = race.WaitAsync;
        int stagingVersions = backend.History(AttemptStoreTests.StagingHead).Count;
        int productionVersions = backend.History("environments/production/head.json").Count;
        string?[] results = await Task.WhenAll(
            AttemptStoreTests.RefusalAsync(() => production.ApplyDecisionAsync(scenario.Reservation.Bytes, cancellationToken: Token)),
            AttemptStoreTests.RefusalAsync(() => staging.ApplyDecisionAsync(scenario.Supersession.Bytes, cancellationToken: Token)));

        AttemptStoreDecision? decision = await staging.ReadDecisionAsync(scenario.Binding.DecisionId, Token);
        Assert.NotNull(decision);
        if (results[0] is null)
        {
            Assert.Equal("decision-reserved", results[1]);
            Assert.Equal(scenario.Reservation.Reference, decision.Reservation?.Reference);
            Assert.Equal("reserved", decision.State);
        }
        else
        {
            Assert.Null(results[1]);
            Assert.Equal("decision-terminal", results[0]);
            Assert.Equal(scenario.Supersession.Reference, decision.Terminal?.Reference);
            Assert.Equal("superseded", decision.State);
        }

        Assert.Equal(stagingVersions + 1, backend.History(AttemptStoreTests.StagingHead).Count);
        Assert.Equal(productionVersions, backend.History("environments/production/head.json").Count);
    }

    [Theory]
    [InlineData("working", "complete", "completed")]
    [InlineData("non-working", "release-reservation", "pending")]
    public async Task ClosureBindsTheAdmittedRevisionAndAuthoritativeTerminalResult(
        string outcome, string action, string state)
    {
        using RecordFixture fixture = new();
        InMemoryAttemptObjectStore backend = new();
        AttemptStore store = new(backend, fixture.Trust);
        DecisionScenario scenario = await PrepareAsync(fixture, store);
        AttemptStoreCommit reserve = await store.ApplyDecisionAsync(scenario.Reservation.Bytes, cancellationToken: Token);
        await AttemptStoreTests.RejectAsync("decision-reserved", () => store.ApplyDecisionAsync(scenario.Supersession.Bytes, cancellationToken: Token));

        SignedBytes<AttemptRecord> terminal = fixture.Revise(scenario.ProductionAttempt,
            scenario.ProductionAttempt.Record.Payload with { Outcome = outcome });
        await store.ReviseAsync(terminal.Bytes, terminal.Version, Token);
        SignedBytes<LatestWorkingPointerRecord>? pointer = outcome == "working" ? fixture.Pointer(terminal) : null;
        SignedBytes<PromotionDecisionRecord> close = fixture.Decision(
            "close", action, scenario.Binding, scenario.Release, terminal, scenario.Reservation, pointer);
        if (pointer is not null)
        {
            await AttemptStoreTests.RejectAsync("invalid-transition", () => store.ApplyDecisionAsync(close.Bytes, cancellationToken: Token));
            await store.MoveLatestWorkingAsync(pointer.Bytes, Token);
        }

        AttemptStoreCommit completion = await store.ApplyDecisionAsync(close.Bytes, cancellationToken: Token);
        AttemptStoreDecision? decided = await store.ReadDecisionAsync(scenario.Binding.DecisionId, Token);
        Assert.Equal(state, decided?.State);
        int writes = backend.Writes;
        Assert.Equal(reserve, await store.ApplyDecisionAsync(scenario.Reservation.Bytes, cancellationToken: Token));
        Assert.Equal(completion, await store.ApplyDecisionAsync(close.Bytes, cancellationToken: Token));
        Assert.Equal(writes, backend.Writes);
        if (outcome == "non-working")
        {
            await store.ApplyDecisionAsync(scenario.Supersession.Bytes, cancellationToken: Token);
            Assert.Equal("superseded", (await store.ReadDecisionAsync(scenario.Binding.DecisionId, Token))?.State);
        }
    }

    [Fact]
    public async Task AdministratorReservationProtectsPendingDecisionWithoutProductionAttemptBinding()
    {
        using RecordFixture fixture = new();
        AttemptStore store = new(new InMemoryAttemptObjectStore(), fixture.Trust);
        DecisionScenario scenario = await PrepareAsync(fixture, store);
        SignedBytes<PromotionDecisionRecord> action = fixture.Decision(
            "admin-reserve", "reserve-administrator", scenario.Binding, scenario.Release);
        await store.ApplyDecisionAsync(action.Bytes, cancellationToken: Token);
        AttemptStoreDecision? state = await store.ReadDecisionAsync(scenario.Binding.DecisionId, Token);
        Assert.Equal("reserved", state?.State);
        Assert.Equal(action.Reference, state?.AdministratorReservation?.Reference);
        await AttemptStoreTests.RejectAsync("decision-reserved", () => store.ApplyDecisionAsync(scenario.Supersession.Bytes, cancellationToken: Token));
        await store.ApplyDecisionAsync(scenario.Reservation.Bytes, cancellationToken: Token);
        Assert.Equal(action.Reference, (await store.ReadDecisionAsync(scenario.Binding.DecisionId, Token))?.AdministratorReservation?.Reference);
    }

    [Fact]
    public async Task AdmissionRevalidatesIfProductionChangesBeforeStagingWrite()
    {
        using RecordFixture fixture = new();
        InMemoryAttemptObjectStore backend = new();
        AttemptStore store = new(backend, fixture.Trust);
        DecisionScenario scenario = await PrepareAsync(fixture, store);
        SignedBytes<AttemptRecord> terminal = fixture.Revise(
            scenario.ProductionAttempt, scenario.ProductionAttempt.Record.Payload with { Outcome = "non-working" });
        string reservationKey = StoredRecord.Key(Staging, "promotion-decision", scenario.Reservation.Reference);
        backend.BeforeCreate = async (key, token) =>
        {
            if (key == reservationKey)
            {
                await store.ReviseAsync(terminal.Bytes, terminal.Version, token);
            }
        };

        int versions = backend.History(AttemptStoreTests.StagingHead).Count;
        await AttemptStoreTests.RejectAsync("lock-not-held", () => store.ApplyDecisionAsync(
            scenario.Reservation.Bytes, cancellationToken: Token));
        Assert.Equal(versions, backend.History(AttemptStoreTests.StagingHead).Count);
        Assert.Equal("pending", (await store.ReadDecisionAsync(scenario.Binding.DecisionId, Token))?.State);
    }

    [Fact]
    public async Task FreshIntakeRequiresAllPreviousDecisionsForTheReleaseToBeTerminal()
    {
        using RecordFixture fixture = new();
        InMemoryAttemptObjectStore backend = new();
        AttemptStore store = new(backend, fixture.Trust);
        SignedBytes<ReleaseRecord> release = fixture.Release();
        SignedBytes<ReleaseRecord> otherRelease = fixture.Release("release-b");
        StagingEvidenceBinding originalBinding = RecordFixture.Binding("original");
        StagingEvidenceBinding freshBinding = RecordFixture.Binding("fresh");
        SignedBytes<AttemptRecord> initial = fixture.Attempt("initial", Staging, 1, release, binding: originalBinding);
        await store.AcquireAsync(initial.Bytes, release.Bytes, initial.Version, Token);
        await FinishAsync(initial);

        SignedBytes<AttemptRecord> premature = fixture.Attempt("premature", Staging, 2, release, binding: freshBinding);
        int writes = backend.Writes;
        await AttemptStoreTests.RejectAsync("decision-binding-conflict", () => store.AcquireAsync(premature.Bytes, release.Bytes, premature.Version, Token));
        Assert.Equal(writes, backend.Writes);
        Assert.Null(await store.ReadDecisionAsync(freshBinding.DecisionId, Token));

        SignedBytes<AttemptRecord> initiator = fixture.Attempt("initiator", Staging, 2, otherRelease, binding: RecordFixture.Binding("other"));
        await store.AcquireAsync(initiator.Bytes, otherRelease.Bytes, initiator.Version, Token);
        SignedBytes<PromotionDecisionRecord> supersede = fixture.Decision("supersede", "supersede", originalBinding, release, initiator);
        await store.ApplyDecisionAsync(supersede.Bytes, cancellationToken: Token);
        await FinishAsync(initiator);

        SignedBytes<AttemptRecord> reopening = fixture.Attempt("reopening", Staging, 3, release, binding: originalBinding);
        await AttemptStoreTests.RejectAsync("decision-terminal", () => store.AcquireAsync(reopening.Bytes, release.Bytes, reopening.Version, Token));
        SignedBytes<AttemptRecord> fresh = fixture.Attempt("fresh", Staging, 3, release, binding: freshBinding);
        await store.AcquireAsync(fresh.Bytes, release.Bytes, fresh.Version, Token);
        AttemptStoreDecision? previous = await store.ReadDecisionAsync(originalBinding.DecisionId, Token);
        Assert.Equal(supersede.Reference, previous?.Terminal?.Reference);
        Assert.Equal("superseded", previous?.State);
        Assert.Equal("pending", (await store.ReadDecisionAsync(freshBinding.DecisionId, Token))?.State);
        await FinishAsync(fresh);

        SignedBytes<AttemptRecord> third = fixture.Attempt("third", Staging, 4, release, binding: RecordFixture.Binding("third"));
        await AttemptStoreTests.RejectAsync("decision-binding-conflict", () => store.AcquireAsync(third.Bytes, release.Bytes, third.Version, Token));

        async Task FinishAsync(SignedBytes<AttemptRecord> attempt)
        {
            SignedBytes<AttemptRecord> terminal = fixture.Revise(attempt, attempt.Record.Payload with { Outcome = "non-working" });
            await store.ReviseAsync(terminal.Bytes, terminal.Version, Token);
        }
    }

    [Fact]
    public async Task SupersedingResetRequiresIndependentPrerequisiteProof()
    {
        using RecordFixture fixture = new();
        AttemptStore store = new(new InMemoryAttemptObjectStore(), fixture.Trust);
        DecisionScenario scenario = await PrepareAsync(fixture, store, initiatingKind: "in-place-restore");

        await AttemptStoreTests.RejectAsync("unverified-prerequisite-reset", () => store.ApplyDecisionAsync(scenario.Supersession.Bytes, cancellationToken: Token));
        await AttemptStoreTests.RejectAsync("unverified-prerequisite-reset", () => store.ApplyDecisionAsync(
            scenario.Supersession.Bytes, (_, _, _, _, _) => Task.FromResult(false), Token));
        await store.ApplyDecisionAsync(scenario.Supersession.Bytes, (reset, initiatingRelease, targetRelease, decision, _) =>
        {
            Assert.Equal(scenario.StagingInitiator.Reference, reset.Reference);
            Assert.Equal(scenario.StagingInitiator.Record.Payload.Release, initiatingRelease.Reference);
            Assert.Equal(scenario.Release.Reference, targetRelease.Reference);
            Assert.Equal(scenario.Binding, decision);
            return Task.FromResult(true);
        }, Token);

        await AttemptStoreTests.RejectAsync("decision-terminal", () => store.ApplyDecisionAsync(scenario.Reservation.Bytes, cancellationToken: Token));
    }

    [Fact]
    public async Task TakeoverOfAdmittedAttemptDoesNotReleaseReservation()
    {
        using RecordFixture fixture = new();
        AttemptStore store = new(new InMemoryAttemptObjectStore(), fixture.Trust);
        DecisionScenario scenario = await PrepareAsync(fixture, store);
        await store.ApplyDecisionAsync(scenario.Reservation.Bytes, cancellationToken: Token);
        SignedBytes<AttemptRecord> predecessor = scenario.ProductionAttempt;
        SignedBytes<AttemptRecord> successor = fixture.Attempt("new-production", Production, 2, scenario.Release,
            "production-successor", predecessor.Reference, scenario.Binding);
        SignedBytes<AttemptRecord> final = fixture.Revise(predecessor,
            predecessor.Record.Payload with { LockEpoch = 2, Outcome = "superseded", SupersededBy = successor.Reference }, "production-successor");
        await store.TakeoverAsync(successor.Bytes, final.Bytes, scenario.Release.Bytes, successor.Version, final.Version, Token);
        SignedBytes<AttemptRecord> oldTerminal = fixture.Revise(predecessor, predecessor.Record.Payload with { Outcome = "non-working" });
        SignedBytes<PromotionDecisionRecord> stale = fixture.Decision(
            "stale-release", "release-reservation", scenario.Binding, scenario.Release, oldTerminal, scenario.Reservation);
        await AttemptStoreTests.RejectAsync("stale-epoch", () => store.ApplyDecisionAsync(stale.Bytes, cancellationToken: Token));

        SignedBytes<AttemptRecord> terminal = fixture.Revise(successor, successor.Record.Payload with { Outcome = "non-working" });
        await store.ReviseAsync(terminal.Bytes, terminal.Version, Token);
        SignedBytes<PromotionDecisionRecord> unqualified = fixture.Decision(
            "takeover-release", "release-reservation", scenario.Binding, scenario.Release, terminal, scenario.Reservation,
            writer: "production-successor");
        await AttemptStoreTests.RejectAsync("invalid-transition", () => store.ApplyDecisionAsync(unqualified.Bytes, cancellationToken: Token));
        Assert.Equal(scenario.Reservation.Reference, (await store.ReadDecisionAsync(scenario.Binding.DecisionId, Token))?.Reservation?.Reference);
        await AttemptStoreTests.RejectAsync("decision-reserved", () => store.ApplyDecisionAsync(scenario.Supersession.Bytes, cancellationToken: Token));
    }

    [Fact]
    public async Task UncertainClosureWithUnavailableReadKeepsConflictingWorkBlocked()
    {
        using RecordFixture fixture = new();
        InMemoryAttemptObjectStore backend = new();
        AttemptStore store = new(backend, fixture.Trust);
        DecisionScenario scenario = await PrepareAsync(fixture, store);
        await store.ApplyDecisionAsync(scenario.Reservation.Bytes, cancellationToken: Token);
        SignedBytes<AttemptRecord> terminal = fixture.Revise(
            scenario.ProductionAttempt, scenario.ProductionAttempt.Record.Payload with { Outcome = "non-working" });
        await store.ReviseAsync(terminal.Bytes, terminal.Version, Token);
        SignedBytes<PromotionDecisionRecord> release = fixture.Decision(
            "release", "release-reservation", scenario.Binding, scenario.Release, terminal, scenario.Reservation);
        backend.BeforeReplace = (key, _) =>
        {
            Assert.Equal(AttemptStoreTests.StagingHead, key);
            backend.BeforeRead = next => next == key ? new IOException("Read unavailable") : null;
            throw new AttemptStoreWriteUncertainException(new TimeoutException());
        };

        await Assert.ThrowsAsync<AttemptStoreWriteUncertainException>(() => store.ApplyDecisionAsync(release.Bytes, cancellationToken: Token));
        backend.BeforeRead = null;
        backend.BeforeReplace = null;
        Assert.Equal("reserved", (await store.ReadDecisionAsync(scenario.Binding.DecisionId, Token))?.State);
        await AttemptStoreTests.RejectAsync("decision-reserved", () => store.ApplyDecisionAsync(scenario.Supersession.Bytes, cancellationToken: Token));
        await store.ApplyDecisionAsync(release.Bytes, cancellationToken: Token);
        Assert.Equal("pending", (await store.ReadDecisionAsync(scenario.Binding.DecisionId, Token))?.State);
    }

    private static async Task<DecisionScenario> PrepareAsync(
        RecordFixture fixture, AttemptStore store, string initiatingKind = "release")
    {
        SignedBytes<ReleaseRecord> release = fixture.Release();
        SignedBytes<ReleaseRecord> other = fixture.Release("release-b");
        StagingEvidenceBinding binding = RecordFixture.Binding("decision-a");
        SignedBytes<AttemptRecord> initial = fixture.Attempt("initial", Staging, 1, release, binding: binding);
        await store.AcquireAsync(initial.Bytes, release.Bytes, initial.Version, Token);
        SignedBytes<AttemptRecord> working = fixture.Revise(initial, initial.Record.Payload with { Outcome = "working" });
        await store.ReviseAsync(working.Bytes, working.Version, Token);
        SignedBytes<AttemptRecord> staging = fixture.Attempt("initiator", Staging, 2, other,
            binding: RecordFixture.Binding("decision-b"), kind: initiatingKind);
        await store.AcquireAsync(staging.Bytes, other.Bytes, staging.Version, Token);
        SignedBytes<AttemptRecord> production = fixture.Attempt("production", Production, 1, release, binding: binding);
        await store.AcquireAsync(production.Bytes, release.Bytes, production.Version, Token);

        return new(release, binding, staging, production,
            fixture.Decision("reserve", "reserve-admission", binding, release, production),
            fixture.Decision("supersede", "supersede", binding, release, staging));
    }

    private sealed record DecisionScenario(
        SignedBytes<ReleaseRecord> Release,
        StagingEvidenceBinding Binding,
        SignedBytes<AttemptRecord> StagingInitiator,
        SignedBytes<AttemptRecord> ProductionAttempt,
        SignedBytes<PromotionDecisionRecord> Reservation,
        SignedBytes<PromotionDecisionRecord> Supersession);
}
