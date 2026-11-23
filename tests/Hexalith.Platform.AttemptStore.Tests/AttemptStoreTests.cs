using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.Builds.Tooling.ReleaseRecords;

namespace Hexalith.Platform.AttemptStore.Tests;

public sealed class AttemptStoreTests
{
    private const string Staging = AttemptStore.Staging;
    private const string Production = AttemptStore.Production;
    internal const string StagingHead = "environments/staging/head.json";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task MissingHeadRefusesTransitionsUntilExplicitCreate()
    {
        using RecordFixture fixture = new();
        InMemoryAttemptObjectStore backend = new();
        AttemptStore store = new(backend, fixture.Trust);
        SignedBytes<ReleaseRecord> release = fixture.Release();
        SignedBytes<AttemptRecord> attempt = fixture.Attempt("initial", Staging, 1, release);
        SignedBytes<PromotionStopRecord> stop = fixture.Stop(Staging);

        Assert.Null(await store.ReadAsync(Staging, Token));
        await RejectAsync("head-missing", () => store.AcquireAsync(attempt.Bytes, release.Bytes, cancellationToken: Token));
        await RejectAsync("head-missing", () => store.SetStopAsync(stop.Bytes, Token));
        Assert.Equal(0, backend.Writes);

        Assert.True(await store.InitializeAsync(Staging, Token));
        AttemptStoreState? initial = await store.ReadAsync(Staging, Token);
        Assert.NotNull(initial);
        Assert.Equal((0L, 0L), (initial.Revision, initial.Epoch));
        await store.AcquireAsync(attempt.Bytes, release.Bytes, cancellationToken: Token);
        int writes = backend.Writes;
        Assert.False(await store.InitializeAsync(Staging, Token));
        Assert.Equal(attempt.Reference, (await store.ReadAsync(Staging, Token))?.Holder?.Reference);
        Assert.Equal(writes, backend.Writes);
    }

    [Fact]
    public async Task ParallelAcquisitionCommitsExactlyOneAndLinksHeadVersions()
    {
        using RecordFixture fixture = new();
        WaitForTwoWriters writers = new(StagingHead);
        InMemoryAttemptObjectStore backend = new() { BeforeReplace = writers.WaitAsync };
        AttemptStore first = new(backend, fixture.Trust);
        AttemptStore second = new(backend, fixture.Trust);
        Assert.True(await first.InitializeAsync(Staging, Token));
        SignedBytes<ReleaseRecord> release = fixture.Release();
        SignedBytes<AttemptRecord> a = fixture.Attempt("a", Staging, 1, release);
        SignedBytes<AttemptRecord> b = fixture.Attempt("b", Staging, 1, release);

        string?[] results = await Task.WhenAll(
            RefusalAsync(() => first.AcquireAsync(a.Bytes, release.Bytes, cancellationToken: Token)),
            RefusalAsync(() => second.AcquireAsync(b.Bytes, release.Bytes, cancellationToken: Token)));

        Assert.Contains(null, results);
        Assert.Contains("lock-held", results);
        AttemptStoreState? state = await second.ReadAsync(Staging, Token);
        Assert.NotNull(state);
        Assert.Equal((1L, 1L), (state.Revision, state.Epoch));
        RecordReference winning = results[0] is null ? a.Reference : b.Reference;
        RecordReference losing = results[0] is null ? b.Reference : a.Reference;
        Assert.Equal(winning, state.Holder?.Reference);
        Assert.Null(await first.ReadAttemptAsync(Staging, losing.RecordId, Token));
        IReadOnlyList<AttemptObject> versions = backend.History(StagingHead);
        Assert.Equal(2, versions.Count);
        Assert.Equal(StoredRecord.Digest(versions[0].Bytes), AttemptStoreHead.Decode(versions[1].Bytes, Staging).Previous);
    }

    [Fact]
    public async Task TakeoverCommitsBothRecordsAndRejectsMissingLinkAndStaleMutation()
    {
        using RecordFixture fixture = new();
        InMemoryAttemptObjectStore backend = new();
        AttemptStore store = new(backend, fixture.Trust);
        Assert.True(await store.InitializeAsync(Staging, Token));
        SignedBytes<ReleaseRecord> release = fixture.Release();
        SignedBytes<AttemptRecord> first = fixture.Attempt("first", Staging, 1, release);
        AttemptStoreCommit acquisition = await store.AcquireAsync(first.Bytes, release.Bytes, cancellationToken: Token);
        Assert.Equal(acquisition, await store.AcquireAsync(first.Bytes, release.Bytes, cancellationToken: Token));
        SignedBytes<AttemptRecord> successor = fixture.Attempt("successor", Staging, 2, release,
            writer: "staging-successor", supersedes: first.Reference);
        AttemptRecord original = first.Record.Payload;
        SignedBytes<AttemptRecord> mismatched = fixture.Revise(first,
            original with { LockEpoch = 2, Outcome = "superseded", SupersededBy = first.Reference }, "staging-successor");

        int writes = backend.Writes;
        await RejectAsync("invalid-transition", () => store.TakeoverAsync(successor.Bytes, mismatched.Bytes, release.Bytes, cancellationToken: Token));
        SignedBytes<AttemptRecord> unlinked = fixture.Attempt("unlinked", Staging, 2, release, writer: "staging-successor");
        await RejectAsync("invalid-transition", () => store.TakeoverAsync(unlinked.Bytes, mismatched.Bytes, release.Bytes, cancellationToken: Token));
        Assert.Equal(writes, backend.Writes);

        SignedBytes<AttemptRecord> final = fixture.Revise(first,
            original with { LockEpoch = 2, Outcome = "superseded", SupersededBy = successor.Reference }, "staging-successor");
        AttemptStoreCommit receipt = await store.TakeoverAsync(successor.Bytes, final.Bytes, release.Bytes, cancellationToken: Token);
        Assert.Equal(final.Reference, receipt.Companion);
        Assert.Equal((2L, 2L), (receipt.Revision, receipt.Epoch));
        AttemptStoreState? state = await store.ReadAsync(Staging, Token);
        Assert.Equal(successor.Reference, state?.Holder?.Reference);
        Assert.Equal(final.Reference, (await store.ReadAttemptAsync(Staging, "first", Token))?.Reference);

        SignedBytes<AttemptRecord> stale = fixture.Revise(first, original with { Outcome = "working" });
        writes = backend.Writes;
        await RejectAsync("stale-epoch", () => store.ReviseAsync(stale.Bytes, cancellationToken: Token));
        await RejectAsync("stale-epoch", () => store.AcquireAsync(first.Bytes, release.Bytes, cancellationToken: Token));
        Assert.Equal(receipt, await store.TakeoverAsync(successor.Bytes, final.Bytes, release.Bytes, cancellationToken: Token));
        Assert.Equal(writes, backend.Writes);
    }

    [Fact]
    public async Task AcquireAndTakeoverRequireExactActiveStop()
    {
        using RecordFixture fixture = new();
        InMemoryAttemptObjectStore backend = new();
        AttemptStore store = new(backend, fixture.Trust);
        Assert.True(await store.InitializeAsync(Staging, Token));
        SignedBytes<ReleaseRecord> release = fixture.Release();
        SignedBytes<PromotionStopRecord> firstStop = fixture.Stop(Staging);
        await store.SetStopAsync(firstStop.Bytes, Token);
        SignedBytes<AttemptRecord> unobserved = fixture.Attempt("first", Staging, 1, release);
        int writes = backend.Writes;
        await RejectAsync("stop-revision-mismatch", () => store.AcquireAsync(unobserved.Bytes, release.Bytes, cancellationToken: Token));
        Assert.Equal(writes, backend.Writes);
        SignedBytes<AttemptRecord> first = fixture.Attempt("first", Staging, 1, release, observedStop: firstStop.Reference);
        await store.AcquireAsync(first.Bytes, release.Bytes, cancellationToken: Token);

        SignedBytes<PromotionStopRecord> secondStop = fixture.Stop(Staging, 2, firstStop.Reference);
        await store.SetStopAsync(secondStop.Bytes, Token);
        SignedBytes<AttemptRecord> stale = Successor(firstStop.Reference);
        SignedBytes<AttemptRecord> staleFinal = Final(stale);
        writes = backend.Writes;
        await RejectAsync("stop-revision-mismatch", () => store.TakeoverAsync(stale.Bytes, staleFinal.Bytes, release.Bytes, cancellationToken: Token));
        Assert.Equal(writes, backend.Writes);
        SignedBytes<AttemptRecord> fresh = Successor(secondStop.Reference);
        await store.TakeoverAsync(fresh.Bytes, Final(fresh).Bytes, release.Bytes, cancellationToken: Token);
        Assert.Equal(fresh.Reference, (await store.ReadAsync(Staging, Token))?.Holder?.Reference);

        SignedBytes<AttemptRecord> Successor(RecordReference observed) =>
            fixture.Attempt("successor", Staging, 2, release, "staging-successor", first.Reference, observedStop: observed);

        SignedBytes<AttemptRecord> Final(SignedBytes<AttemptRecord> successor) => fixture.Revise(first,
            first.Record.Payload with { LockEpoch = 2, Outcome = "superseded", SupersededBy = successor.Reference }, "staging-successor");
    }

    [Fact]
    public async Task InProgressRevisionPreservesHolderAndEpoch()
    {
        using RecordFixture fixture = new();
        InMemoryAttemptObjectStore backend = new();
        AttemptStore store = new(backend, fixture.Trust);
        Assert.True(await store.InitializeAsync(Staging, Token));
        SignedBytes<ReleaseRecord> release = fixture.Release();
        SignedBytes<AttemptRecord> initial = fixture.Attempt("initial", Staging, 1, release);
        await store.AcquireAsync(initial.Bytes, release.Bytes, cancellationToken: Token);
        SignedBytes<AttemptRecord> revised = fixture.Revise(initial,
            initial.Record.Payload with { FirstMutationUtc = RecordFixture.Timestamp.AddSeconds(1) });

        AttemptStoreCommit receipt = await store.ReviseAsync(revised.Bytes, cancellationToken: Token);

        Assert.Equal((2L, 1L), (receipt.Revision, receipt.Epoch));
        AttemptStoreState? state = await store.ReadAsync(Staging, Token);
        Assert.NotNull(state);
        Assert.Equal((2L, 1L), (state.Revision, state.Epoch));
        Assert.Equal(revised.Reference, state.Holder?.Reference);
        Assert.Equal("in-progress", state.Holder?.Record.Payload.Outcome);
    }

    [Fact]
    public async Task AuthoritativeReadRefusesResignedBytesAtTheSameArchiveReference()
    {
        using RecordFixture fixture = new();
        InMemoryAttemptObjectStore backend = new();
        AttemptStore store = new(backend, fixture.Trust);
        Assert.True(await store.InitializeAsync(Staging, Token));
        SignedBytes<ReleaseRecord> release = fixture.Release();
        SignedBytes<AttemptRecord> initial = fixture.Attempt("initial", Staging, 1, release);
        await store.AcquireAsync(initial.Bytes, release.Bytes, cancellationToken: Token);
        SignedBytes<AttemptRecord> resigned = fixture.Revise(initial, initial.Record.Payload, seconds: 0);
        Assert.Equal(initial.Reference, resigned.Reference);
        Assert.False(initial.Bytes.SequenceEqual(resigned.Bytes));
        string archiveKey = StoredRecord.Key(Staging, "attempt", initial.Reference);
        AttemptObject original = Assert.Single(backend.History(archiveKey));
        Assert.True(await backend.ReplaceAsync(archiveKey, original.ETag, resigned.Bytes, Token));

        RecordValidationException error = await Assert.ThrowsAsync<RecordValidationException>(() =>
            store.ReadAttemptAsync(Staging, initial.Reference.RecordId, Token));
        Assert.Equal("record-content-conflict", error.Code);
    }

    [Fact]
    public async Task TerminalRevisionIsImmutableAndReplayUsesExactEnvelope()
    {
        using RecordFixture fixture = new();
        InMemoryAttemptObjectStore backend = new();
        AttemptStore store = new(backend, fixture.Trust);
        Assert.True(await store.InitializeAsync(Staging, Token));
        SignedBytes<ReleaseRecord> release = fixture.Release();
        SignedBytes<AttemptRecord> first = fixture.Attempt("initial", Staging, 1, release);
        await store.AcquireAsync(first.Bytes, release.Bytes, cancellationToken: Token);
        SignedBytes<AttemptRecord> working = fixture.Revise(first, first.Record.Payload with { Outcome = "working" });
        AttemptStoreCommit receipt = await store.ReviseAsync(working.Bytes, cancellationToken: Token);
        Assert.Null((await store.ReadAsync(Staging, Token))?.Holder);

        SignedBytes<AttemptRecord> changed = fixture.Revise(first, first.Record.Payload with { Outcome = "non-working" });
        SignedBytes<AttemptRecord> resigned = fixture.Revise(first, first.Record.Payload with { Outcome = "working" });
        int writes = backend.Writes;
        await RejectAsync("terminal-attempt", () => store.ReviseAsync(changed.Bytes, cancellationToken: Token));
        Assert.Equal(working.Reference, resigned.Reference);
        Assert.False(working.Bytes.SequenceEqual(resigned.Bytes));
        await RejectAsync("record-content-conflict", () => store.ReviseAsync(resigned.Bytes, cancellationToken: Token));
        Assert.Equal(receipt, await store.ReviseAsync(working.Bytes, cancellationToken: Token));
        Assert.Equal(writes, backend.Writes);
    }

    [Fact]
    public async Task LostAcknowledgementReconcilesFromReceiptEvenAfterASecondCommit()
    {
        using RecordFixture fixture = new();
        InMemoryAttemptObjectStore backend = new();
        AttemptStore store = new(backend, fixture.Trust);
        Assert.True(await store.InitializeAsync(Staging, Token));
        SignedBytes<ReleaseRecord> release = fixture.Release();
        SignedBytes<AttemptRecord> attempt = fixture.Attempt("initial", Staging, 1, release);
        SignedBytes<PromotionStopRecord> stop = fixture.Stop(Staging);
        backend.AfterReplace = async (_, token) =>
        {
            backend.AfterReplace = null;
            await store.SetStopAsync(stop.Bytes, token);
            throw new AttemptStoreWriteUncertainException(new TimeoutException());
        };

        AttemptStoreCommit receipt = await store.AcquireAsync(attempt.Bytes, release.Bytes, cancellationToken: Token);
        Assert.Equal(1L, receipt.Revision);
        Assert.Equal(2L, (await store.ReadAsync(Staging, Token))?.Revision);
        int writes = backend.Writes;
        Assert.Equal(receipt, await store.AcquireAsync(attempt.Bytes, release.Bytes, cancellationToken: Token));
        SignedBytes<ReleaseRecord> resigned = fixture.ResignRelease(release);
        Assert.Equal(release.Reference, resigned.Reference);
        Assert.False(release.Bytes.SequenceEqual(resigned.Bytes));
        await RejectAsync("record-content-conflict", () => store.AcquireAsync(attempt.Bytes, resigned.Bytes, cancellationToken: Token));
        Assert.Equal(writes, backend.Writes);
    }

    [Fact]
    public async Task LostAcknowledgementOfActiveAttemptRefusesAfterEpochAdvances()
    {
        using RecordFixture fixture = new();
        InMemoryAttemptObjectStore backend = new();
        AttemptStore store = new(backend, fixture.Trust);
        Assert.True(await store.InitializeAsync(Staging, Token));
        SignedBytes<ReleaseRecord> release = fixture.Release();
        SignedBytes<AttemptRecord> attempt = fixture.Attempt("initial", Staging, 1, release);
        SignedBytes<AttemptRecord> successor = fixture.Attempt("successor", Staging, 2, release, "staging-successor", attempt.Reference);
        SignedBytes<AttemptRecord> final = fixture.Revise(attempt,
            attempt.Record.Payload with { LockEpoch = 2, Outcome = "superseded", SupersededBy = successor.Reference }, "staging-successor");
        backend.AfterReplace = async (_, token) =>
        {
            backend.AfterReplace = null;
            await store.TakeoverAsync(successor.Bytes, final.Bytes, release.Bytes, cancellationToken: token);
            throw new AttemptStoreWriteUncertainException(new TimeoutException());
        };

        await RejectAsync("stale-epoch", () => store.AcquireAsync(attempt.Bytes, release.Bytes, cancellationToken: Token));
        await RejectAsync("stale-epoch", () => store.AcquireAsync(attempt.Bytes, release.Bytes, cancellationToken: Token));
        Assert.Equal(final.Reference, (await store.ReadAttemptAsync(Staging, "initial", Token))?.Reference);
        Assert.Equal(successor.Reference, (await store.ReadAsync(Staging, Token))?.Holder?.Reference);
        Assert.Equal(3, backend.History(StagingHead).Count);
    }

    [Fact]
    public async Task UncertainPutWithUnchangedETagReturnsUncertaintyWithoutRetry()
    {
        using RecordFixture fixture = new();
        InMemoryAttemptObjectStore backend = new();
        AttemptStore store = new(backend, fixture.Trust);
        Assert.True(await store.InitializeAsync(Staging, Token));
        SignedBytes<ReleaseRecord> release = fixture.Release();
        SignedBytes<AttemptRecord> attempt = fixture.Attempt("initial", Staging, 1, release);
        int replaceCalls = 0;
        backend.BeforeReplace = (_, _) =>
        {
            if (++replaceCalls == 1)
            {
                throw new AttemptStoreWriteUncertainException(new TimeoutException());
            }

            return Task.CompletedTask;
        };

        await Assert.ThrowsAsync<AttemptStoreWriteUncertainException>(() => store.AcquireAsync(attempt.Bytes, release.Bytes, cancellationToken: Token));
        Assert.Equal(1, replaceCalls);
        Assert.Null((await store.ReadAsync(Staging, Token))?.Holder);
        SignedBytes<ReleaseRecord> resigned = fixture.ResignRelease(release);
        await RejectAsync("record-content-conflict", () => store.AcquireAsync(attempt.Bytes, resigned.Bytes, cancellationToken: Token));
        Assert.Equal(1L, (await store.AcquireAsync(attempt.Bytes, release.Bytes, cancellationToken: Token)).Revision);
    }

    [Fact]
    public async Task LostAcknowledgementWithUnreadableReleaseProofRemainsUncertain()
    {
        using RecordFixture fixture = new();
        InMemoryAttemptObjectStore backend = new();
        AttemptStore store = new(backend, fixture.Trust);
        Assert.True(await store.InitializeAsync(Staging, Token));
        SignedBytes<ReleaseRecord> release = fixture.Release();
        SignedBytes<AttemptRecord> attempt = fixture.Attempt("initial", Staging, 1, release);
        string releaseKey = StoredRecord.Key(Staging, "release", release.Reference);
        backend.AfterReplace = (_, _) =>
        {
            backend.BeforeRead = key => key == releaseKey ? new IOException("Archive temporarily unavailable") : null;
            throw new AttemptStoreWriteUncertainException(new TimeoutException());
        };

        await Assert.ThrowsAsync<AttemptStoreWriteUncertainException>(() => store.AcquireAsync(attempt.Bytes, release.Bytes, cancellationToken: Token));
        backend.BeforeRead = null;
        int writes = backend.Writes;
        Assert.Equal(1L, (await store.AcquireAsync(attempt.Bytes, release.Bytes, cancellationToken: Token)).Revision);
        Assert.Equal(writes, backend.Writes);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LastRereadFindsAnExactCommitOrReportsContention(bool commitExactActionOnLastAttempt)
    {
        const int conditionalAttempts = 16;
        using RecordFixture fixture = new();
        InMemoryAttemptObjectStore backend = new();
        AttemptStore store = new(backend, fixture.Trust);
        Assert.True(await store.InitializeAsync(Staging, Token));
        SignedBytes<ReleaseRecord> release = fixture.Release();
        SignedBytes<AttemptRecord> attempt = fixture.Attempt("initial", Staging, 1, release);
        await store.AcquireAsync(attempt.Bytes, release.Bytes, cancellationToken: Token);
        SignedBytes<AttemptRecord> working = fixture.Revise(attempt, attempt.Record.Payload with { Outcome = "working" });
        SignedBytes<PromotionStopRecord>? previous = null;
        AttemptStoreCommit? concurrent = null;
        int replaceCalls = 0;
        backend.BeforeReplace = CommitFirstAsync;

        if (commitExactActionOnLastAttempt)
        {
            AttemptStoreCommit receipt = await store.ReviseAsync(working.Bytes, cancellationToken: Token);
            Assert.NotNull(concurrent);
            Assert.Equal(concurrent, receipt);
            Assert.Equal(conditionalAttempts + 1, receipt.Revision);
        }
        else
        {
            await RejectAsync("contention", () => store.ReviseAsync(working.Bytes, cancellationToken: Token));
            Assert.Equal(attempt.Reference, (await store.ReadAsync(Staging, Token))?.Holder?.Reference);
        }

        Assert.Equal(conditionalAttempts, replaceCalls);
        Assert.Equal(conditionalAttempts + 2, backend.History(StagingHead).Count);

        async Task CommitFirstAsync(string _, CancellationToken token)
        {
            backend.BeforeReplace = null;
            if (++replaceCalls == conditionalAttempts && commitExactActionOnLastAttempt)
            {
                concurrent = await store.ReviseAsync(working.Bytes, cancellationToken: token);
            }
            else
            {
                previous = fixture.Stop(Staging, replaceCalls, previous?.Reference);
                await store.SetStopAsync(previous.Bytes, token);
            }

            backend.BeforeReplace = CommitFirstAsync;
        }
    }

    [Fact]
    public async Task UnprovenWriteRereadsAndRejectsAfterAnotherWriterAcquires()
    {
        using RecordFixture fixture = new();
        InMemoryAttemptObjectStore backend = new();
        AttemptStore store = new(backend, fixture.Trust);
        Assert.True(await store.InitializeAsync(Staging, Token));
        SignedBytes<ReleaseRecord> release = fixture.Release();
        SignedBytes<AttemptRecord> a = fixture.Attempt("a", Staging, 1, release);
        SignedBytes<AttemptRecord> b = fixture.Attempt("b", Staging, 1, release);
        backend.BeforeReplace = async (_, token) =>
        {
            backend.BeforeReplace = null;
            await store.AcquireAsync(b.Bytes, release.Bytes, cancellationToken: token);
            throw new AttemptStoreWriteUncertainException(new TimeoutException());
        };

        await RejectAsync("lock-held", () => store.AcquireAsync(a.Bytes, release.Bytes, cancellationToken: Token));
        Assert.Null(await store.ReadAttemptAsync(Staging, "a", Token));
        Assert.Equal(b.Reference, (await store.ReadAsync(Staging, Token))?.Holder?.Reference);
    }

    [Theory]
    [InlineData("null-decision")]
    [InlineData("null-release")]
    [InlineData("orphan-active-attempt")]
    public async Task MalformedHeadFailsWithInvalidHead(string damage)
    {
        using RecordFixture fixture = new();
        InMemoryAttemptObjectStore backend = new();
        AttemptStore store = new(backend, fixture.Trust);
        Assert.True(await store.InitializeAsync(Staging, Token));
        SignedBytes<ReleaseRecord> release = fixture.Release();
        SignedBytes<AttemptRecord> attempt = fixture.Attempt("initial", Staging, 1, release, binding: RecordFixture.Binding("decision"));
        await store.AcquireAsync(attempt.Bytes, release.Bytes, attempt.Version, Token);
        AttemptObject original = backend.History(StagingHead)[^1];
        JsonNode head = JsonNode.Parse(original.Bytes)!;
        switch (damage)
        {
            case "null-decision":
                head["decisions"]!["decision"] = null;
                break;
            case "null-release":
                head["decisions"]!["decision"]!["release"] = null;
                break;
            default:
                head["holder"] = null;
                break;
        }

        Assert.True(await backend.ReplaceAsync(StagingHead, original.ETag, JsonSerializer.SerializeToUtf8Bytes(head), Token));
        RecordValidationException exception = await Assert.ThrowsAsync<RecordValidationException>(() => store.ReadAsync(Staging, Token));
        Assert.Equal("invalid-head", exception.Code);
    }

    [Fact]
    public async Task ClearsOnlyStagingWithExactPointerAndAllOutstandingCauses()
    {
        using RecordFixture fixture = new();
        InMemoryAttemptObjectStore backend = new();
        AttemptStore store = new(backend, fixture.Trust);
        Assert.True(await store.InitializeAsync(Staging, Token));
        SignedBytes<ReleaseRecord> release = fixture.Release();
        SignedBytes<AttemptRecord> staging = fixture.Attempt("staging-initial", Staging, 1, release);
        await store.AcquireAsync(staging.Bytes, release.Bytes, cancellationToken: Token);
        SignedBytes<AttemptRecord> working = fixture.Revise(staging, staging.Record.Payload with { Outcome = "working" });
        await store.ReviseAsync(working.Bytes, cancellationToken: Token);
        SignedBytes<LatestWorkingPointerRecord> pointer = fixture.Pointer(working);
        AttemptStoreCommit firstPointer = await store.MoveLatestWorkingAsync(pointer.Bytes, Token);
        SignedBytes<AttemptRecord> another = fixture.Attempt("staging-second", Staging, 2, release);
        await store.AcquireAsync(another.Bytes, release.Bytes, cancellationToken: Token);
        SignedBytes<AttemptRecord> nonPointer = fixture.Revise(another, another.Record.Payload with { Outcome = "working" });
        await store.ReviseAsync(nonPointer.Bytes, cancellationToken: Token);

        Assert.True(await store.InitializeAsync(Production, Token));
        SignedBytes<PromotionStopRecord> production = fixture.Stop(Production);
        await store.SetStopAsync(production.Bytes, Token);
        SignedBytes<PromotionStopRecord> first = fixture.Stop(Staging);
        SignedBytes<PromotionStopRecord> second = fixture.Stop(Staging, 2, first.Reference, "readiness");
        await store.SetStopAsync(first.Bytes, Token);
        await store.SetStopAsync(second.Bytes, Token);
        SignedBytes<AdministratorRecord> wrongAttempt = fixture.Clear("wrong", second, nonPointer, ("health", "accepted"), ("readiness", "resolved"));
        SignedBytes<AdministratorRecord> crossEnvironment = fixture.Clear("cross-environment", production, working, ("health", "resolved"));
        SignedBytes<AdministratorRecord> partial = fixture.Clear("partial", second, working, ("readiness", "resolved"));
        int writes = backend.Writes;
        await RejectAsync("working-attempt-mismatch", () => store.ClearStopAsync(wrongAttempt.Bytes, Token));
        await RejectAsync("working-attempt-mismatch", () => store.ClearStopAsync(crossEnvironment.Bytes, Token));
        await RejectAsync("untrusted-writer", () => store.SetStopAsync(fixture.UntrustedProductionStop(), Token));
        await RejectAsync("undisposed-stop-cause", () => store.ClearStopAsync(partial.Bytes, Token));
        Assert.Equal(writes, backend.Writes);

        SignedBytes<AdministratorRecord> clear = fixture.Clear("clear", second, working, ("health", "accepted"), ("readiness", "resolved"));
        AttemptStoreCommit receipt = await store.ClearStopAsync(clear.Bytes, Token);
        Assert.Null((await store.ReadAsync(Staging, Token))?.Stop);
        Assert.Equal(2, (await store.ReadAsync(Staging, Token))?.StopRevision);
        Assert.Equal(production.Reference, (await store.ReadAsync(Production, Token))?.Stop?.Reference);
        SignedBytes<PromotionStopRecord> third = fixture.Stop(Staging, 3, second.Reference, "integrity");
        await store.SetStopAsync(third.Bytes, Token);
        writes = backend.Writes;
        Assert.Equal(receipt, await store.ClearStopAsync(clear.Bytes, Token));
        Assert.Equal(third.Reference, (await store.ReadAsync(Staging, Token))?.Stop?.Reference);
        Assert.Equal(writes, backend.Writes);
        SignedBytes<AdministratorRecord> conflicting = fixture.Clear("clear", third, working, ("integrity", "resolved"));
        await RejectAsync("record-id-conflict", () => store.ClearStopAsync(conflicting.Bytes, Token));
        SignedBytes<AdministratorRecord> foreign = fixture.Clear("clear-next", third, working, partial.Reference, ("integrity", "resolved"));
        await RejectAsync("invalid-transition", () => store.ClearStopAsync(foreign.Bytes, Token));
        Assert.Equal(writes, backend.Writes);
        SignedBytes<AdministratorRecord> nextClear = fixture.Clear("clear-next", third, working, clear.Reference, ("integrity", "resolved"));
        await store.ClearStopAsync(nextClear.Bytes, Token);
        AttemptStoreState? cleared = await store.ReadAsync(Staging, Token);
        Assert.NotNull(cleared);
        Assert.Null(cleared.Stop);
        Assert.Equal(3, cleared.StopRevision);
        Assert.Equal(production.Reference, (await store.ReadAsync(Production, Token))?.Stop?.Reference);

        SignedBytes<LatestWorkingPointerRecord> wrongLineage = fixture.Pointer(nonPointer, "new-pointer-id");
        await RejectAsync("invalid-transition", () => store.MoveLatestWorkingAsync(wrongLineage.Bytes, Token));
        SignedBytes<LatestWorkingPointerRecord> nextPointer = fixture.Pointer(nonPointer);
        await store.MoveLatestWorkingAsync(nextPointer.Bytes, Token);
        writes = backend.Writes;
        Assert.Equal(firstPointer, await store.MoveLatestWorkingAsync(pointer.Bytes, Token));
        Assert.Equal(nextPointer.Reference, (await store.ReadAsync(Staging, Token))?.LatestWorking?.Reference);
        Assert.Equal(writes, backend.Writes);
    }

    internal static async Task<string?> RefusalAsync(Func<Task<AttemptStoreCommit>> commit)
    {
        try
        {
            await commit();
            return null;
        }
        catch (RecordValidationException exception)
        {
            return exception.Code;
        }
    }

    internal static async Task RejectAsync(string code, Func<Task<AttemptStoreCommit>> commit) =>
        Assert.Equal(code, await RefusalAsync(commit));
}
