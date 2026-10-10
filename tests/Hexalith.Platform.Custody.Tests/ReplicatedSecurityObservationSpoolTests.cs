using System.Text;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Custody.Tests;

/// <summary>Actual component/CAS/recovery source and serialized persisted end-state under synthetic authorities/backends; no replica, backup or production proof.</summary>
public sealed class ReplicatedSecurityObservationSpoolTests
{
    /// <summary>Readiness needs the existing recorder on both an empty and fully acknowledged namespace.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecorderBindingIsRequiredForReadiness(bool acknowledged)
    {
        var f = new SecuritySpoolFixture();
        if (acknowledged)
        {
            await f.Spool.ObserveAsync(SecuritySpoolFixture.Intent(), TestContext.Current.CancellationToken);
            (await f.Spool.DrainAsync(1, TestContext.Current.CancellationToken)).ShouldBe(1);
        }
        (await f.Spool.IsReadyAsync(TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await new ReplicatedSecurityObservationSpool(f.Client, f.Clock, f.Authority).IsReadyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    /// <summary>A full acknowledged carrier archives before new admission; old originals and routing survive restart.</summary>
    [Fact]
    public async Task FullAcknowledgedSpoolContinuesAndKeepsOriginals()
    {
        var f = new SecuritySpoolFixture();
        var records = Enumerable.Range(1, 10000).Select(sequence =>
        {
            var original = new SecurityObservationRecord(SecuritySpoolFixture.Intent("observation-" + sequence), f.Clock.Now,
                f.Clock.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), sequence, null);
            return original with { Receipt = SecuritySpoolFixture.Receipt(original) };
        }).ToArray();
        var state = new SecuritySpoolSnapshot(f.Target.InstallationEpoch, 20000, records);
        f.Persisted = JsonSerializer.SerializeToUtf8Bytes(state); f.Anchor = state.Revision; f.AnchorDigest = ReplicatedSecurityObservationSpool.StateDigest(state);
        (await f.Spool.IsReadyAsync(TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await f.Spool.LookupAsync(records[0].Intent, TestContext.Current.CancellationToken)).ShouldBe(records[0]);
        (await f.Spool.ObserveAsync(records[0].Intent, TestContext.Current.CancellationToken)).ShouldBe(records[0]);
        var next = await f.Spool.ObserveAsync(SecuritySpoolFixture.Intent("new-original"), TestContext.Current.CancellationToken);
        next.ShouldNotBeNull(); next.Sequence.ShouldBe(10001);
        f.Read()!.PageIndex.ShouldBe(1); f.Read()!.Records.Single().ShouldBe(next); f.Archives.Count.ShouldBe(1);
        var restarted = new ReplicatedSecurityObservationSpool(f.Client, f.Clock, f.Authority, f.Recorder);
        (await restarted.LookupAsync(records[0].Intent, TestContext.Current.CancellationToken)).ShouldBe(records[0]);
        (await restarted.ObserveAsync(records[0].Intent, TestContext.Current.CancellationToken)).ShouldBe(records[0]);
        (await restarted.DrainAsync(1, TestContext.Current.CancellationToken)).ShouldBe(1);
        (await restarted.IsReadyAsync(TestContext.Current.CancellationToken)).ShouldBeTrue();
        byte[] archived = f.ArchiveAt(0); f.RemoveArchive(0);
        (await restarted.LookupAsync(records[0].Intent, TestContext.Current.CancellationToken)).ShouldBeNull();
        (await restarted.IsReadyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
        f.SetArchive(0, archived);
        f.PendingBytes.ShouldBeNull(); f.PhysicalAppends.ShouldBe(1);
    }

    /// <summary>Two variable-size linked pages preserve the oldest original and a missing middle page blocks every archived proof.</summary>
    [Fact]
    public async Task TwoArchivedPagesRequireCompleteChainForOldestOriginal()
    {
        var f = new SecuritySpoolFixture();
        var oldestIntent = SecuritySpoolFixture.Intent("oldest") with { RetainedServerReceiptKey = "server-oldest" };
        var middleIntent = SecuritySpoolFixture.Intent("middle") with { RetainedServerReceiptKey = "server-middle" };
        SecurityObservationRecord Original(SecurityObservationIntent intent, long sequence)
        {
            var pending = new SecurityObservationRecord(intent, f.Clock.Now, f.Clock.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), sequence, null);
            return pending with { Receipt = SecuritySpoolFixture.Receipt(pending) };
        }
        var oldest = Original(oldestIntent, 1); var middle = Original(middleIntent, 2);
        var first = new SecuritySpoolSnapshot(f.Target.InstallationEpoch, 2, [oldest]);
        var page0 = new SecuritySpoolArchivePage(0, first, null);
        string digest0 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(page0)));
        var second = new SecuritySpoolSnapshot(f.Target.InstallationEpoch, 5, [middle])
            { PageIndex = 1, ArchivedObservedCount = 1, ArchivedAcknowledgedCount = 1, ArchiveHeadDigest = digest0 };
        var page1 = new SecuritySpoolArchivePage(1, second, digest0);
        string digest1 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(page1)));
        var head = new SecuritySpoolSnapshot(f.Target.InstallationEpoch, 6, [])
            { PageIndex = 2, ArchivedObservedCount = 2, ArchivedAcknowledgedCount = 2, ArchiveHeadDigest = digest1 };
        f.SetArchive(0, JsonSerializer.SerializeToUtf8Bytes(page0)); f.SetArchive(1, JsonSerializer.SerializeToUtf8Bytes(page1));
        f.Persisted = JsonSerializer.SerializeToUtf8Bytes(head); f.Anchor = head.Revision; f.AnchorDigest = ReplicatedSecurityObservationSpool.StateDigest(head);
        var restarted = new ReplicatedSecurityObservationSpool(f.Client, f.Clock, f.Authority, f.Recorder);
        (await restarted.LookupAsync(oldestIntent, TestContext.Current.CancellationToken)).ShouldBe(oldest);
        byte[] middlePage = f.ArchiveAt(1);
        string middleKey = f.ArchiveKey(1, middlePage);
        var tampered = JsonSerializer.Deserialize<SecuritySpoolArchivePage>(middlePage)!;
        var changed = tampered.Snapshot.Records.Single() with
        { Receipt = tampered.Snapshot.Records.Single().Receipt! with { EventId = "valid-but-foreign-page-content" } };
        f.Archives[middleKey] = JsonSerializer.SerializeToUtf8Bytes(tampered with { Snapshot = tampered.Snapshot with { Records = [changed] } });
        (await restarted.LookupAsync(oldestIntent, TestContext.Current.CancellationToken)).ShouldBeNull();
        (await restarted.IsReadyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
        f.Archives[middleKey] = middlePage;
        var overstated = head with { Revision = head.Revision + 2, ArchivedObservedCount = 3, ArchivedAcknowledgedCount = 3 };
        f.Persisted = JsonSerializer.SerializeToUtf8Bytes(overstated); f.Anchor = overstated.Revision;
        f.AnchorDigest = ReplicatedSecurityObservationSpool.StateDigest(overstated);
        (await restarted.IsReadyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
        f.Persisted = JsonSerializer.SerializeToUtf8Bytes(head); f.Anchor = head.Revision;
        f.AnchorDigest = ReplicatedSecurityObservationSpool.StateDigest(head);
        var duplicatePending = new SecurityObservationRecord(oldestIntent, f.Clock.Now,
            f.Clock.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), 2, null);
        var duplicatePage = page1 with { Snapshot = second with { Records = [duplicatePending with { Receipt = SecuritySpoolFixture.Receipt(duplicatePending) }] } };
        byte[] duplicateBytes = JsonSerializer.SerializeToUtf8Bytes(duplicatePage);
        f.RemoveArchive(1); f.SetArchive(1, duplicateBytes);
        var duplicateHead = head with { ArchiveHeadDigest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(duplicateBytes)) };
        f.Persisted = JsonSerializer.SerializeToUtf8Bytes(duplicateHead); f.AnchorDigest = ReplicatedSecurityObservationSpool.StateDigest(duplicateHead);
        (await restarted.LookupAsync(oldestIntent, TestContext.Current.CancellationToken)).ShouldBeNull();
        f.RemoveArchive(1); f.SetArchive(1, middlePage);
        f.Persisted = JsonSerializer.SerializeToUtf8Bytes(head); f.AnchorDigest = ReplicatedSecurityObservationSpool.StateDigest(head);
        var exact = await restarted.LookupOriginalAsync(new("server-oldest", oldestIntent.ReasonCode, oldestIntent.UntrustedFieldsHmac, oldestIntent.DigestKeyVersion), TestContext.Current.CancellationToken);
        exact.IsAvailable.ShouldBeTrue(); exact.Record.ShouldBe(oldest);
        (await restarted.IsReadyAsync(TestContext.Current.CancellationToken)).ShouldBeTrue();
        f.RemoveArchive(1);
        (await restarted.LookupAsync(oldestIntent, TestContext.Current.CancellationToken)).ShouldBeNull();
        (await restarted.LookupOriginalAsync(new("server-oldest", oldestIntent.ReasonCode, oldestIntent.UntrustedFieldsHmac, oldestIntent.DigestKeyVersion), TestContext.Current.CancellationToken)).IsAvailable.ShouldBeFalse();
        (await restarted.IsReadyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await restarted.ObserveAsync(SecuritySpoolFixture.Intent("new-after-gap"), TestContext.Current.CancellationToken)).ShouldBeNull();
        f.SetArchive(1, middlePage);
        (await restarted.LookupAsync(oldestIntent, TestContext.Current.CancellationToken)).ShouldBe(oldest);
    }

    /// <summary>Sixteen authenticated pages are the finite local envelope; a seventeenth rollover refuses without discarding the acknowledged head.</summary>
    [Fact]
    public async Task ArchivePageCeilingRefusesSeventeenthRolloverWithoutChangingHead()
    {
        var f = new SecuritySpoolFixture(); string prior = SeedSixteenPages(f);
        var records = Enumerable.Range(17, 10000).Select(sequence =>
        {
            var pending = new SecurityObservationRecord(SecuritySpoolFixture.Intent("head-" + sequence), f.Clock.Now,
                f.Clock.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), sequence, null);
            return pending with { Receipt = SecuritySpoolFixture.Receipt(pending) };
        }).ToArray();
        var head = new SecuritySpoolSnapshot(f.Target.InstallationEpoch, 20048, records)
        { PageIndex = 16, ArchivedObservedCount = 16, ArchivedAcknowledgedCount = 16, ArchiveHeadDigest = prior };
        f.Persisted = JsonSerializer.SerializeToUtf8Bytes(head); f.Anchor = head.Revision; f.AnchorDigest = ReplicatedSecurityObservationSpool.StateDigest(head);
        byte[] before = f.Persisted.ToArray();
        (await f.Spool.IsReadyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await f.Spool.ObserveAsync(SecuritySpoolFixture.Intent("seventeenth-page"), TestContext.Current.CancellationToken)).ShouldBeNull();
        f.Persisted.ShouldBe(before); f.Archives.Count.ShouldBe(16); f.PendingBytes.ShouldBeNull();
        (await f.Spool.LookupAsync(records[0].Intent, TestContext.Current.CancellationToken)).ShouldBe(records[0]);
    }

    /// <summary>At the finite page ceiling, a byte-full acknowledged head cannot claim no-argument readiness for one worst-case valid original and receipt.</summary>
    [Fact]
    public async Task FinalPageReadinessReservesWorstCaseNextObservation()
    {
        var f = new SecuritySpoolFixture(); string prior = SeedSixteenPages(f);
        SecurityObservationRecord Seed(int sequence)
        {
            var intent = SecuritySpoolFixture.Intent("ceiling-" + sequence + new string('o', 1600)) with
            { RetainedServerReceiptKey = "server-" + sequence + new string('r', 1600), DigestKeyVersion = new string('k', 1600) };
            var pending = new SecurityObservationRecord(intent, f.Clock.Now, f.Clock.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), sequence, null);
            return pending with { Receipt = SecuritySpoolFixture.Receipt(pending) };
        }
        int perRecord = JsonSerializer.SerializeToUtf8Bytes(Seed(17)).Length + 1;
        var records = Enumerable.Range(17, (RecoverableAnchoredState.MaximumPendingBytes - 180000) / perRecord).Select(Seed).ToList();
        SecuritySpoolSnapshot Head() => new(f.Target.InstallationEpoch, 48 + records.Count * 2L, records.ToArray())
        { PageIndex = 16, ArchivedObservedCount = 16, ArchivedAcknowledgedCount = 16, ArchiveHeadDigest = prior };
        while (JsonSerializer.SerializeToUtf8Bytes(Head()).Length > RecoverableAnchoredState.MaximumPendingBytes - 130000) { records.RemoveAt(records.Count - 1); }
        while (JsonSerializer.SerializeToUtf8Bytes(Head()).Length < RecoverableAnchoredState.MaximumPendingBytes - 240000) { records.Add(Seed(records.Count + 17)); }
        records.Count.ShouldBeLessThan(10000);
        var head = Head();
        int currentBytes = JsonSerializer.SerializeToUtf8Bytes(head).Length;
        currentBytes.ShouldBeLessThan(RecoverableAnchoredState.MaximumPendingBytes);
        currentBytes.ShouldBeGreaterThan(RecoverableAnchoredState.MaximumPendingBytes - 262144);
        f.Persisted = JsonSerializer.SerializeToUtf8Bytes(head); f.Anchor = head.Revision;
        f.AnchorDigest = ReplicatedSecurityObservationSpool.StateDigest(head);
        (await f.Spool.IsReadyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
        f.Read()!.Revision.ShouldBe(head.Revision); f.Archives.Count.ShouldBe(16); f.PendingBytes.ShouldBeNull();
    }

    /// <summary>A durable immutable archive written before its head transition survives the failed conditional stage and exact restart retry.</summary>
    [Fact]
    public async Task ArchivePersistedBeforeHeadTransitionRecoversOriginalOnRestart()
    {
        var f = new SecuritySpoolFixture(); var originals = SeedFullAcknowledgedHead(f);
        f.FailSave = true; f.FailSaveStage = 1;
        var next = SecuritySpoolFixture.Intent("after-fault");
        (await f.Spool.ObserveAsync(next, TestContext.Current.CancellationToken)).ShouldBeNull();
        f.Archives.Count.ShouldBe(1); f.Read()!.PageIndex.ShouldBe(0); f.Read()!.Records.Count.ShouldBe(10000);
        f.FailSave = false;
        var restarted = new ReplicatedSecurityObservationSpool(f.Client, f.Clock, f.Authority, f.Recorder);
        (await restarted.ObserveAsync(next, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        f.Archives.Count.ShouldBe(1); f.Read()!.PageIndex.ShouldBe(1);
        (await restarted.LookupAsync(originals[0].Intent, TestContext.Current.CancellationToken)).ShouldBe(originals[0]);
    }

    /// <summary>An orphan from a smaller acknowledged predecessor cannot reserve the anchored head's content-addressed rollover slot.</summary>
    [Fact]
    public async Task StalePartialPageAtSameIndexCannotBlockCurrentRollover()
    {
        var f = new SecuritySpoolFixture(); var originals = SeedFullAcknowledgedHead(f);
        var partial = new SecuritySpoolSnapshot(f.Target.InstallationEpoch, 19998, originals.Take(9999).ToArray());
        byte[] orphan = JsonSerializer.SerializeToUtf8Bytes(new SecuritySpoolArchivePage(0, partial, null));
        f.SetArchive(0, orphan);
        (await f.Spool.IsReadyAsync(TestContext.Current.CancellationToken)).ShouldBeTrue();
        var next = await f.Spool.ObserveAsync(SecuritySpoolFixture.Intent("after-stale-page"), TestContext.Current.CancellationToken);
        next.ShouldNotBeNull(); next.Sequence.ShouldBe(10001);
        f.Archives.Count.ShouldBe(2); f.Read()!.PageIndex.ShouldBe(1);
        (await f.Spool.LookupAsync(originals[0].Intent, TestContext.Current.CancellationToken)).ShouldBe(originals[0]);
        f.Archives[f.ArchiveKey(0, orphan)].ShouldBe(orphan);
    }

    /// <summary>One caller pauses after immutable page persistence while a competing rollover advances the head; restart retains both exact new intents.</summary>
    [Fact]
    public async Task ConcurrentRolloverRetainsOnePageAndBothNewIntents()
    {
        var f = new SecuritySpoolFixture(); var originals = SeedFullAcknowledgedHead(f);
        var a = SecuritySpoolFixture.Intent("concurrent-a"); var b = SecuritySpoolFixture.Intent("concurrent-b");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int gated = 0;
        f.Client.GetStateAndETagAsync<SecuritySpoolArchivePage>(f.Target.ComponentName, Arg.Any<string>(), Arg.Any<Dapr.Client.ConsistencyMode?>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            string key = call.ArgAt<string>(1);
            long index = long.Parse(key.Split('/')[^2], System.Globalization.CultureInfo.InvariantCulture);
            if (!f.Archives.TryGetValue(key, out var bytes)) { return (null!, "0"); }
            if (index == 0 && Interlocked.Exchange(ref gated, 1) == 0) { entered.TrySetResult(); await release.Task; }
            return (JsonSerializer.Deserialize<SecuritySpoolArchivePage>(bytes)!, "1");
        });
        var first = f.Spool.ObserveAsync(a, TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        SecurityObservationRecord? observedB;
        try { observedB = await f.Spool.ObserveAsync(b, TestContext.Current.CancellationToken); }
        finally { release.TrySetResult(); }
        observedB.ShouldNotBeNull();
        (await first).ShouldBeNull();
        var restarted = new ReplicatedSecurityObservationSpool(f.Client, f.Clock, f.Authority, f.Recorder);
        var observedA = await restarted.ObserveAsync(a, TestContext.Current.CancellationToken);
        observedA.ShouldNotBeNull(); observedA.Sequence.ShouldBe(10002);
        (await restarted.ObserveAsync(b, TestContext.Current.CancellationToken)).ShouldBe(observedB);
        f.Archives.Count.ShouldBe(1); f.Read()!.Records.Count.ShouldBe(2);
        (await restarted.LookupAsync(originals[0].Intent, TestContext.Current.CancellationToken)).ShouldBe(originals[0]);
    }

    /// <summary>Pending admission reserves enough bytes for a later maximal exact recorder receipt, so drain can acknowledge and archive the original.</summary>
    [Fact]
    public async Task NearByteLimitAdmissionReservesFutureReceiptBeforeAcknowledgment()
    {
        var f = new SecuritySpoolFixture();
        SecurityObservationRecord Seed(int sequence)
        {
            var intent = SecuritySpoolFixture.Intent("seed-" + sequence + new string('o', 1600)) with
            { RetainedServerReceiptKey = "server-" + sequence + new string('r', 1600), DigestKeyVersion = new string('k', 1600) };
            var pending = new SecurityObservationRecord(intent, f.Clock.Now, f.Clock.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), sequence, null);
            return pending with { Receipt = SecuritySpoolFixture.Receipt(pending) with { EventId = "e" } };
        }
        var nextIntent = SecuritySpoolFixture.Intent("pending-after-near-full-head");
        int bound = RecoverableAnchoredState.MaximumPendingBytes;
        int perRecord = JsonSerializer.SerializeToUtf8Bytes(Seed(1)).Length + 1;
        int count = (bound - 20000) / perRecord;
        var records = Enumerable.Range(1, count).Select(Seed).ToList();
        int CandidateBytes()
        {
            var pending = new SecurityObservationRecord(nextIntent, f.Clock.Now, f.Clock.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), records.Count + 1L, null);
            return JsonSerializer.SerializeToUtf8Bytes(new SecuritySpoolSnapshot(f.Target.InstallationEpoch, records.Count * 2L + 1, records.Append(pending).ToArray())).Length;
        }
        while (CandidateBytes() > bound - 500) { records.RemoveAt(records.Count - 1); }
        int remaining = bound - 500 - CandidateBytes();
        for (int index = records.Count - 1; remaining > 0 && index >= 0; index--)
        {
            int fill = Math.Min(remaining, 2047);
            var record = records[index];
            records[index] = record with { Receipt = record.Receipt! with { EventId = "e" + new string('e', fill) } };
            remaining -= fill;
        }
        remaining.ShouldBe(0); records.Count.ShouldBeLessThan(10000); CandidateBytes().ShouldBe(bound - 500);
        var wouldBePending = new SecurityObservationRecord(nextIntent, f.Clock.Now, f.Clock.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), records.Count + 1L, null);
        var maximalReceipt = SecuritySpoolFixture.Receipt(wouldBePending) with { EventId = new string('\0', 2048) };
        var stranded = new SecuritySpoolSnapshot(f.Target.InstallationEpoch, records.Count * 2L + 2,
            records.Append(wouldBePending with { Receipt = maximalReceipt }).ToArray());
        JsonSerializer.SerializeToUtf8Bytes(stranded).Length.ShouldBeGreaterThan(bound);
        var head = new SecuritySpoolSnapshot(f.Target.InstallationEpoch, records.Count * 2L, records.ToArray());
        f.Persisted = JsonSerializer.SerializeToUtf8Bytes(head); f.Anchor = head.Revision;
        f.AnchorDigest = ReplicatedSecurityObservationSpool.StateDigest(head);
        var observed = await f.Spool.ObserveAsync(nextIntent, TestContext.Current.CancellationToken);
        observed.ShouldNotBeNull(); observed.Sequence.ShouldBe(records.Count + 1L);
        f.Archives.Count.ShouldBe(1); f.Read()!.PageIndex.ShouldBe(1); f.Read()!.Records.Single().ShouldBe(observed);
        f.Recorded[nextIntent.ObservationId] = SecuritySpoolFixture.Receipt(observed) with { EventId = maximalReceipt.EventId };
        (await f.Spool.DrainAsync(1, TestContext.Current.CancellationToken)).ShouldBe(1);
        f.Read()!.Records.Single().Receipt.ShouldBe(f.Recorded[nextIntent.ObservationId]);
        f.PendingBytes.ShouldBeNull();
        (await f.Spool.LookupAsync(records[0].Intent, TestContext.Current.CancellationToken)).ShouldBe(records[0]);
    }

    /// <summary>Admission reserves the future maximal receipt of every older pending original, not only the newly proposed observation.</summary>
    [Fact]
    public async Task NearLimitSeveralPendingOriginalsCannotBeStrandedByOneMoreAdmission()
    {
        var f = new SecuritySpoolFixture(); int bound = RecoverableAnchoredState.MaximumPendingBytes;
        SecurityObservationRecord Seed(int sequence)
        {
            var intent = SecuritySpoolFixture.Intent("seed-" + sequence + new string('o', 1600)) with
            { RetainedServerReceiptKey = "server-" + sequence + new string('r', 1600), DigestKeyVersion = new string('k', 1600) };
            var pending = new SecurityObservationRecord(intent, f.Clock.Now, f.Clock.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), sequence, null);
            return pending with { Receipt = SecuritySpoolFixture.Receipt(pending) with { EventId = "e" } };
        }
        var nextIntent = SecuritySpoolFixture.Intent("proposed-after-two-pending");
        int perRecord = JsonSerializer.SerializeToUtf8Bytes(Seed(1)).Length + 1;
        var records = Enumerable.Range(1, (bound - 30000) / perRecord).Select(Seed).ToList();
        int ProposedBytes()
        {
            var proposed = new SecurityObservationRecord(nextIntent, f.Clock.Now, f.Clock.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), records.Count + 1L, null);
            return JsonSerializer.SerializeToUtf8Bytes(new SecuritySpoolSnapshot(f.Target.InstallationEpoch, records.Count * 2L + 1, records.Append(proposed).ToArray())).Length;
        }
        while (ProposedBytes() > bound - 30000) { records.RemoveAt(records.Count - 1); }
        int fillRemaining = bound - 30000 - ProposedBytes();
        for (int index = records.Count - 3; fillRemaining > 0 && index >= 0; index--)
        {
            int fill = Math.Min(fillRemaining, 2047);
            records[index] = records[index] with { Receipt = records[index].Receipt! with { EventId = "e" + new string('e', fill) } };
            fillRemaining -= fill;
        }
        fillRemaining.ShouldBe(0);
        records[^1] = records[^1] with { Receipt = null };
        records[^2] = records[^2] with { Receipt = null };
        var pendingNext = new SecurityObservationRecord(nextIntent, f.Clock.Now, f.Clock.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), records.Count + 1L, null);
        var candidate = records.Append(pendingNext).ToArray();
        SecurityObservationRecord Reserved(SecurityObservationRecord record) => record.Receipt is not null ? record : record with
        { Receipt = SecuritySpoolFixture.Receipt(record) with { SourceRevision = long.MaxValue, EventId = new string('\0', 2048) } };
        int projectedRevision = records.Count * 2 - 2 + 1;
        var onlyNew = new SecuritySpoolSnapshot(f.Target.InstallationEpoch, projectedRevision, records.Append(Reserved(pendingNext)).ToArray());
        var allPending = onlyNew with { Records = candidate.Select(Reserved).ToArray() };
        JsonSerializer.SerializeToUtf8Bytes(new SecuritySpoolSnapshot(f.Target.InstallationEpoch, projectedRevision - 1, records.Select(Reserved).ToArray())).Length.ShouldBeLessThanOrEqualTo(bound);
        JsonSerializer.SerializeToUtf8Bytes(onlyNew).Length.ShouldBeLessThanOrEqualTo(bound);
        JsonSerializer.SerializeToUtf8Bytes(allPending).Length.ShouldBeGreaterThan(bound);
        var head = new SecuritySpoolSnapshot(f.Target.InstallationEpoch, projectedRevision - 1, records.ToArray());
        f.Persisted = JsonSerializer.SerializeToUtf8Bytes(head); f.Anchor = head.Revision; f.AnchorDigest = ReplicatedSecurityObservationSpool.StateDigest(head);
        byte[] before = f.Persisted.ToArray();
        (await f.Spool.ObserveAsync(nextIntent, TestContext.Current.CancellationToken)).ShouldBeNull();
        f.Persisted.ShouldBe(before); f.PendingBytes.ShouldBeNull(); f.Archives.Count.ShouldBe(0);
    }

    /// <summary>An out-of-contract recorder event identity cannot be acknowledged or used to mutate the pending original.</summary>
    [Theory]
    [InlineData("ascii")][InlineData("multibyte")][InlineData("invalid-utf16")]
    public async Task OversizedRecorderEventIdFailsClosedWithoutChangingPendingOriginal(string vector)
    {
        var f = new SecuritySpoolFixture();
        var original = await f.Spool.ObserveAsync(SecuritySpoolFixture.Intent(), TestContext.Current.CancellationToken);
        original.ShouldNotBeNull();
        string eventId = vector switch
        {
            "multibyte" => new string('é', 1025),
            "invalid-utf16" => new string('\uD800', 1),
            _ => new string('e', 2049),
        };
        f.Recorded[original.Intent.ObservationId] = SecuritySpoolFixture.Receipt(original) with { EventId = eventId };
        (await f.Spool.DrainAsync(1, TestContext.Current.CancellationToken)).ShouldBe(0);
        f.Read()!.Records.Single().Receipt.ShouldBeNull();
        f.PendingBytes.ShouldBeNull();
    }

    /// <summary>An acknowledged carrier rolls over when the exact next pending bytes exceed 32 MiB before its record count is full.</summary>
    [Fact]
    public async Task ByteCapacityRolloverArchivesVariableCountWithoutDroppingOriginals()
    {
        var f = new SecuritySpoolFixture();
        SecurityObservationRecord Seed(int sequence)
        {
            var intent = SecuritySpoolFixture.Intent("seed-" + sequence + new string('x', 1000)) with
            { RetainedServerReceiptKey = "receipt-" + sequence + new string('r', 1000), DigestKeyVersion = new string('k', 1000) };
            var pending = new SecurityObservationRecord(intent, f.Clock.Now, f.Clock.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), sequence, null);
            return pending with { Receipt = SecuritySpoolFixture.Receipt(pending) };
        }
        var seed = Seed(1); int perRecord = JsonSerializer.SerializeToUtf8Bytes(seed).Length + 1;
        int count = Math.Min(9000, (RecoverableAnchoredState.MaximumPendingBytes - 4096) / perRecord);
        var records = Enumerable.Range(1, count).Select(Seed).ToList();
        var nextIntent = SecuritySpoolFixture.Intent("next-" + new string('n', 1900)) with
        { RetainedServerReceiptKey = new string('r', 1900), DigestKeyVersion = new string('k', 1900) };
        int CurrentBytes() => JsonSerializer.SerializeToUtf8Bytes(new SecuritySpoolSnapshot(f.Target.InstallationEpoch, records.Count * 2L, records)).Length;
        int CandidateBytes()
        {
            var pending = new SecurityObservationRecord(nextIntent, f.Clock.Now, f.Clock.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), records.Count + 1L, null);
            return JsonSerializer.SerializeToUtf8Bytes(new SecuritySpoolSnapshot(f.Target.InstallationEpoch, records.Count * 2L + 1, records.Append(pending).ToArray())).Length;
        }
        while (CurrentBytes() > RecoverableAnchoredState.MaximumPendingBytes) { records.RemoveAt(records.Count - 1); }
        while (CandidateBytes() <= RecoverableAnchoredState.MaximumPendingBytes && records.Count < 9999) { records.Add(Seed(records.Count + 1)); }
        records.Count.ShouldBeLessThan(10000); CurrentBytes().ShouldBeLessThanOrEqualTo(RecoverableAnchoredState.MaximumPendingBytes);
        CandidateBytes().ShouldBeGreaterThan(RecoverableAnchoredState.MaximumPendingBytes);
        var head = new SecuritySpoolSnapshot(f.Target.InstallationEpoch, records.Count * 2L, records.ToArray());
        f.Persisted = JsonSerializer.SerializeToUtf8Bytes(head); f.Anchor = head.Revision; f.AnchorDigest = ReplicatedSecurityObservationSpool.StateDigest(head);
        (await f.Spool.IsReadyAsync(TestContext.Current.CancellationToken)).ShouldBeTrue();
        var observed = await f.Spool.ObserveAsync(nextIntent, TestContext.Current.CancellationToken);
        observed.ShouldNotBeNull(); observed.Sequence.ShouldBe(records.Count + 1L);
        f.Archives.Count.ShouldBe(1); f.Read()!.PageIndex.ShouldBe(1); f.Read()!.ArchivedObservedCount.ShouldBe(records.Count);
        (await f.Spool.LookupAsync(records[0].Intent, TestContext.Current.CancellationToken)).ShouldBe(records[0]);
    }

    private static string SeedSixteenPages(SecuritySpoolFixture fixture)
    {
        string? prior = null;
        for (int index = 0; index < 16; index++)
        {
            var pending = new SecurityObservationRecord(SecuritySpoolFixture.Intent("page-" + index), fixture.Clock.Now,
                fixture.Clock.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), index + 1, null);
            var record = pending with { Receipt = SecuritySpoolFixture.Receipt(pending) };
            var snapshot = new SecuritySpoolSnapshot(fixture.Target.InstallationEpoch, 3L * index + 2, [record])
            { PageIndex = index, ArchivedObservedCount = index, ArchivedAcknowledgedCount = index, ArchiveHeadDigest = prior };
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new SecuritySpoolArchivePage(index, snapshot, prior));
            fixture.SetArchive(index, bytes);
            prior = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
        }
        return prior!;
    }

    private static SecurityObservationRecord[] SeedFullAcknowledgedHead(SecuritySpoolFixture fixture)
    {
        var records = Enumerable.Range(1, 10000).Select(sequence =>
        {
            var pending = new SecurityObservationRecord(SecuritySpoolFixture.Intent("seed-" + sequence), fixture.Clock.Now,
                fixture.Clock.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), sequence, null);
            return pending with { Receipt = SecuritySpoolFixture.Receipt(pending) };
        }).ToArray();
        var head = new SecuritySpoolSnapshot(fixture.Target.InstallationEpoch, 20000, records);
        fixture.Persisted = JsonSerializer.SerializeToUtf8Bytes(head); fixture.Anchor = head.Revision;
        fixture.AnchorDigest = ReplicatedSecurityObservationSpool.StateDigest(head);
        return records;
    }

    /// <summary>A separately authorized mutation recovers the independently admitted exact stage after restart without the original caller; read-only evidence cannot advance it and physical effects are not duplicated.</summary>
    [Fact]
    public async Task LaterMutationRecoversPreJournalOriginalWithoutItsCaller()
    {
        var f = new SecuritySpoolFixture(); var original = SecuritySpoolFixture.Intent(); AnchoredFixtureJournal.SetAvailable(f.Authority, false);
        (await f.Spool.ObserveAsync(original, TestContext.Current.CancellationToken)).ShouldBeNull();
        var pending = f.PendingBytes!.ToArray(); var staged = JsonSerializer.Deserialize<SecuritySpoolSnapshot>(JsonSerializer.Deserialize<AnchoredStateTransition>(pending)!.TargetBytes)!;
        f.Authority.ClearReceivedCalls(); var restarted = new ReplicatedSecurityObservationSpool(f.Client, f.Clock, f.Authority, f.Recorder);
        (await restarted.LookupAsync(original, TestContext.Current.CancellationToken)).ShouldBeNull(); (await restarted.IsReadyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
        await f.Authority.DidNotReceive().RecordTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>());
        f.PendingBytes.ShouldBe(pending); f.Anchor.ShouldBe(0); f.PhysicalAppends.ShouldBe(0);
        f.Clock.Now = f.Clock.Now.AddHours(3); AnchoredFixtureJournal.SetAvailable(f.Authority, true);
        (await restarted.DrainAsync(1, TestContext.Current.CancellationToken)).ShouldBe(1);
        var saved = f.Read()!; saved.Records.Single().Intent.ShouldBe(original);
        saved.Records.Single().ObservedAt.ShouldBe(staged.Records.Single().ObservedAt); saved.Records.Single().UtcDay.ShouldBe(staged.Records.Single().UtcDay);
        saved.Records.Single().Sequence.ShouldBe(staged.Records.Single().Sequence); saved.Records.Single().Receipt.ShouldNotBeNull();
        f.PhysicalAppends.ShouldBe(1); (await restarted.DrainAsync(1, TestContext.Current.CancellationToken)).ShouldBe(0);
    }

    /// <summary>Original UTC first-seen/day/sequence survive retries, clock rollover and serialized restart; only HMAC safe fields are stored.</summary>
    [Fact]
    public async Task OriginalObservationIsDurableAndStableAcrossRetryAndRestart()
    {
        var f = new SecuritySpoolFixture(); var intent = SecuritySpoolFixture.Intent(); var original = await f.Spool.ObserveAsync(intent, TestContext.Current.CancellationToken);
        original.ShouldNotBeNull(); (await f.Spool.IsReadyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
        string persisted = Encoding.UTF8.GetString(f.Persisted!); persisted.ShouldNotContain("synthetic-untrusted-secret");
        f.Clock.Now = f.Clock.Now.AddHours(2); var restarted = new ReplicatedSecurityObservationSpool(f.Client, f.Clock, f.Authority, f.Recorder);
        (await restarted.ObserveAsync(intent, TestContext.Current.CancellationToken)).ShouldBe(original); f.Read()!.Records.Count.ShouldBe(1); f.Read()!.Revision.ShouldBe(1);
        (await restarted.ObserveAsync(intent with { RoutingTenantId = "tenant-b" }, TestContext.Current.CancellationToken)).ShouldBeNull(); f.Read()!.Records.Single().Intent.ShouldBe(intent);
    }
    /// <summary>Readback resolves committed lost acknowledgements; precommit loss closes readiness and never certifies a staged/nonexistent observation.</summary>
    [Theory]
    [InlineData(1, false)][InlineData(1, true)][InlineData(2, false)][InlineData(2, true)]
    public async Task ComponentSaveFaultDistinguishesPersistedEndState(int failSave, bool committed)
    {
        var f = new SecuritySpoolFixture { FailSave = true, CommitBeforeSaveFault = committed, FailSaveStage = failSave };
        var result = await f.Spool.ObserveAsync(SecuritySpoolFixture.Intent(), TestContext.Current.CancellationToken);
        result.ShouldBeNull(); f.Anchor.ShouldBe(failSave == 1 ? 0 : 1);
        (f.Persisted is not null).ShouldBe(failSave == 2 && committed); f.PhysicalAppends.ShouldBe(0);
        f.FailSave = false;
        var recovered = await new ReplicatedSecurityObservationSpool(f.Client, f.Clock, f.Authority, f.Recorder).ObserveAsync(SecuritySpoolFixture.Intent(), TestContext.Current.CancellationToken);
        recovered.ShouldNotBeNull(); f.Read()!.Records.Single().Receipt.ShouldBeNull(); f.Anchor.ShouldBe(1);
        (await f.Spool.IsReadyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
    }
    /// <summary>Exact independent source proof resolves recorder loss; spool acknowledges once and restart cannot append it again.</summary>
    [Fact]
    public async Task LostRecorderAcknowledgementRecoversExactSourceBeforeSpoolAck()
    {
        var f = new SecuritySpoolFixture { LoseAppendAcknowledgement = true }; await f.Spool.ObserveAsync(SecuritySpoolFixture.Intent(), TestContext.Current.CancellationToken);
        (await f.Spool.DrainAsync(10, TestContext.Current.CancellationToken)).ShouldBe(1); f.Read()!.Records.Single().Receipt.ShouldBe(f.Recorded.Single().Value);
        (await f.Spool.IsReadyAsync(TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await new ReplicatedSecurityObservationSpool(f.Client, f.Clock, f.Authority, f.Recorder).DrainAsync(10, TestContext.Current.CancellationToken)).ShouldBe(0); f.PhysicalAppends.ShouldBe(1);
    }
    /// <summary>Accepted but unpersisted recording, unknown lookup, or malformed/cross-tenant receipt cannot acknowledge or skip pending evidence.</summary>
    [Theory]
    [InlineData("unpersisted")][InlineData("unknown")][InlineData("wrong-receipt")]
    public async Task UnknownOrMalformedRecorderEvidenceRetainsPending(string vector)
    {
        var f = new SecuritySpoolFixture { AcceptWithoutPersistence = true }; await f.Spool.ObserveAsync(SecuritySpoolFixture.Intent(), TestContext.Current.CancellationToken);
        if (vector == "unknown") { f.Recorder.LookupAsync(Arg.Any<SecurityObservationRecord>(), Arg.Any<CancellationToken>()).Returns(new SecurityEventRecorderLookup(SecurityEventRecorderLookupState.Unknown)); }
        if (vector == "wrong-receipt") { var record = f.Read()!.Records.Single(); f.Recorded[record.Intent.ObservationId] = SecuritySpoolFixture.Receipt(record) with { RoutingTenantId = "tenant-b" }; }
        (await f.Spool.DrainAsync(10, TestContext.Current.CancellationToken)).ShouldBe(0); f.Read()!.Records.Single().Receipt.ShouldBeNull();
        (await f.Spool.IsReadyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse(); if (vector != "unpersisted") { f.PhysicalAppends.ShouldBe(0); }
    }
    /// <summary>Missing qualification/private credentials or restored older state cannot certify an empty namespace or release a recorder request.</summary>
    [Fact]
    public async Task MissingPrivateAuthorityOrRollbackAlwaysBlocks()
    {
        var f = new SecuritySpoolFixture(); (await new ReplicatedSecurityObservationSpool(f.Client, f.Clock).IsReadyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await new ReplicatedSecurityObservationSpool(f.Client, f.Clock).ObserveAsync(SecuritySpoolFixture.Intent(), TestContext.Current.CancellationToken)).ShouldBeNull();
        await f.Spool.ObserveAsync(SecuritySpoolFixture.Intent(), TestContext.Current.CancellationToken); f.Persisted = null;
        (await f.Spool.IsReadyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse(); (await f.Spool.DrainAsync(10, TestContext.Current.CancellationToken)).ShouldBe(0); f.PhysicalAppends.ShouldBe(0);
    }
    /// <summary>Crossing the accepted automatic recovery horizon leaves original pending evidence for separately authorized recovery.</summary>
    [Fact]
    public async Task AutomaticRecoveryHorizonDoesNotDiscardOldPendingObservation()
    {
        var f = new SecuritySpoolFixture(); await f.Spool.ObserveAsync(SecuritySpoolFixture.Intent(), TestContext.Current.CancellationToken);
        f.Clock.Now += PlatformAcceptedEnvelopeTiming.RecoveryHorizon;
        (await f.Spool.DrainAsync(10, TestContext.Current.CancellationToken)).ShouldBe(0); f.Read()!.Records.Single().Receipt.ShouldBeNull(); f.PhysicalAppends.ShouldBe(0);
        (await f.Spool.IsReadyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
    }
    /// <summary>Independent anchor rejects a divergent restored vector even when its epoch and monotonic revision match.</summary>
    [Fact]
    public async Task SameRevisionDifferentStateCannotPassRestoreProof()
    {
        var f = new SecuritySpoolFixture(); var intent = SecuritySpoolFixture.Intent();
        await f.Spool.ObserveAsync(intent, TestContext.Current.CancellationToken); var state = f.Read()!;
        f.Persisted = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(state with { Records = [state.Records.Single() with { Intent = intent with { ReasonCode = "scope-mismatch" } }] });
        (await f.Spool.LookupAsync(intent, TestContext.Current.CancellationToken)).ShouldBeNull();
        (await f.Spool.IsReadyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await f.Spool.DrainAsync(10, TestContext.Current.CancellationToken)).ShouldBe(0); f.PhysicalAppends.ShouldBe(0); f.Anchor.ShouldBe(1);
    }
    /// <summary>Old original source evidence remains reconcilable without granting a new automatic append after H.</summary>
    [Fact]
    public async Task ExactRecordedOutcomeBeyondHorizonAcknowledgesWithoutAppend()
    {
        var f = new SecuritySpoolFixture(); var intent = SecuritySpoolFixture.Intent();
        var original = (await f.Spool.ObserveAsync(intent, TestContext.Current.CancellationToken))!;
        f.Recorded[intent.ObservationId] = SecuritySpoolFixture.Receipt(original);
        f.Clock.Now += PlatformAcceptedEnvelopeTiming.RecoveryHorizon;
        (await f.Spool.DrainAsync(10, TestContext.Current.CancellationToken)).ShouldBe(1);
        f.PhysicalAppends.ShouldBe(0); f.Read()!.Records.Single().Receipt.ShouldBe(f.Recorded[intent.ObservationId]);
        (await f.Spool.IsReadyAsync(TestContext.Current.CancellationToken)).ShouldBeTrue();
    }
    /// <summary>Exact current lookup is read-only, rejects changed intent, and releases nothing after private authority withdrawal.</summary>
    [Fact]
    public async Task ExactLookupDoesNotMutateAndRequiresCurrentPrivateAuthority()
    {
        var f = new SecuritySpoolFixture(); var intent = SecuritySpoolFixture.Intent();
        var original = await f.Spool.ObserveAsync(intent, TestContext.Current.CancellationToken); byte[] before = f.Persisted!.ToArray();
        (await f.Spool.LookupAsync(intent, TestContext.Current.CancellationToken)).ShouldBe(original);
        (await f.Spool.LookupAsync(intent with { ReasonCode = "scope-mismatch" }, TestContext.Current.CancellationToken)).ShouldBeNull();
        f.Authority.AuthorizeAsync(f.Target, "Lookup", intent, Arg.Any<CancellationToken>()).Returns(false);
        (await f.Spool.LookupAsync(intent, TestContext.Current.CancellationToken)).ShouldBeNull(); f.Persisted.ShouldBe(before); f.PhysicalAppends.ShouldBe(0);
    }


    /// <summary>Definitive pre-anchor failure retains the first caller's exact journal stage; a concurrent different caller cannot replace it and the admitted original recovers after restart/clock advance.</summary>
    [Fact]
    public async Task StagedOriginalSurvivesJournalFailureClockChangeAndConcurrentOtherIntent()
    {
        var f = new SecuritySpoolFixture(); var intent = SecuritySpoolFixture.Intent();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Authority.RecordTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(_ => { entered.TrySetResult(); return release.Task; });
        var first = f.Spool.ObserveAsync(intent, TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); byte[] originalStage = f.PendingBytes!.ToArray();
        var staged = JsonSerializer.Deserialize<SecuritySpoolSnapshot>(JsonSerializer.Deserialize<AnchoredStateTransition>(originalStage)!.TargetBytes)!;
        (await new ReplicatedSecurityObservationSpool(f.Client, f.Clock, f.Authority, f.Recorder).ObserveAsync(SecuritySpoolFixture.Intent("other-in-flight"), TestContext.Current.CancellationToken)).ShouldBeNull();
        f.PendingBytes.ShouldBe(originalStage); f.Persisted.ShouldBeNull(); f.Anchor.ShouldBe(0);
        release.SetResult(false); (await first).ShouldBeNull(); f.InstallJournal(); f.Clock.Now = f.Clock.Now.AddHours(3);
        var restarted = new ReplicatedSecurityObservationSpool(f.Client, f.Clock, f.Authority, f.Recorder);
        (await restarted.IsReadyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse(); f.PendingBytes.ShouldBe(originalStage); f.Anchor.ShouldBe(0);
        (await restarted.ObserveAsync(intent with { ReasonCode = "scope-mismatch" }, TestContext.Current.CancellationToken)).ShouldBeNull(); f.PendingBytes.ShouldBeNull(); f.Read()!.Records.Single().ShouldBe(staged.Records.Single()); f.Anchor.ShouldBe(1); f.PhysicalAppends.ShouldBe(0);
        var recovered = (await restarted.ObserveAsync(intent, TestContext.Current.CancellationToken))!;
        recovered.ShouldBe(staged.Records.Single()); f.Read()!.Records.Single().ShouldBe(recovered); f.Anchor.ShouldBe(1); f.PendingBytes.ShouldBeNull(); f.PhysicalAppends.ShouldBe(0);
    }
    /// <summary>Actually suspended noncooperative journal record/verification is bounded by caller cancellation and the original thirty-second operation deadline; no observation or readiness success is certified.</summary>
    [Theory]
    [InlineData(false, false)][InlineData(false, true)][InlineData(true, false)][InlineData(true, true)]
    public async Task SuspendedIndependentJournalCannotRetainCallerOrCertifySuccess(bool verification, bool callerCancellation)
    {
        var f = new SecuritySpoolFixture(); var intent = SecuritySpoolFixture.Intent();
        if (verification) { (await f.Spool.ObserveAsync(intent, TestContext.Current.CancellationToken)).ShouldNotBeNull(); f.PendingBytes = f.Journal.Values.Single().ToArray(); f.Persisted = null; }
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (verification) { f.Authority.VerifyTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(_ => { entered.TrySetResult(); return release.Task; }); }
        else { f.Authority.RecordTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(_ => { entered.TrySetResult(); return release.Task; }); }
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var result = verification ? f.Spool.LookupAsync(intent, caller.Token) : f.Spool.ObserveAsync(intent, caller.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        try
        {
            if (callerCancellation) { caller.Cancel(); await Should.ThrowAsync<OperationCanceledException>(() => result.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)); }
            else { (await result.WaitAsync(TimeSpan.FromSeconds(35), TestContext.Current.CancellationToken)).ShouldBeNull(); }
            f.Persisted.ShouldBeNull(); f.PendingBytes.ShouldNotBeNull(); f.Anchor.ShouldBe(verification ? 1 : 0); f.PhysicalAppends.ShouldBe(0);
        }
        finally { release.TrySetResult(false); }
    }
    /// <summary>An Unknown first source cannot starve another tenant with bound one; conditional scheduling survives restart without skipping same-source originals or changing first-seen facts.</summary>
    [Fact]
    public async Task DurableFairDrainRetainsUnknownAndAdvancesOtherTenantAfterRestart()
    {
        var f = new SecuritySpoolFixture(); var a = SecuritySpoolFixture.Intent("a-unknown"); var a2 = SecuritySpoolFixture.Intent("a-later");
        var b = SecuritySpoolFixture.Intent("b-recorded") with { RoutingTenantId = "tenant-b" };
        var first = await f.Spool.ObserveAsync(a, TestContext.Current.CancellationToken);
        var later = await f.Spool.ObserveAsync(a2, TestContext.Current.CancellationToken);
        var other = await f.Spool.ObserveAsync(b, TestContext.Current.CancellationToken);
        var visited = new List<string>();
        f.Recorder.LookupAsync(Arg.Any<SecurityObservationRecord>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var record = call.Arg<SecurityObservationRecord>(); visited.Add(record.Intent.ObservationId);
            return record.Intent.RoutingTenantId == "tenant-a" ? new SecurityEventRecorderLookup(SecurityEventRecorderLookupState.Unknown)
                : new SecurityEventRecorderLookup(SecurityEventRecorderLookupState.Recorded, SecuritySpoolFixture.Receipt(record));
        });
        (await f.Spool.DrainAsync(1, TestContext.Current.CancellationToken)).ShouldBe(0);
        var scheduled = f.Read()!; scheduled.DrainAfterSequence.ShouldBe(first!.Sequence); scheduled.DrainRevision.ShouldBe(1);
        (await f.Spool.IsReadyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
        f.Persisted = JsonSerializer.SerializeToUtf8Bytes(scheduled); f.Clock.Now = f.Clock.Now.AddHours(2);
        var restarted = new ReplicatedSecurityObservationSpool(f.Client, f.Clock, f.Authority, f.Recorder);
        (await restarted.DrainAsync(1, TestContext.Current.CancellationToken)).ShouldBe(1);
        var saved = f.Read()!; saved.Records[0].ShouldBe(first); saved.Records[1].ShouldBe(later);
        (saved.Records[2] with { Receipt = null }).ShouldBe(other); saved.Records[2].Receipt.ShouldNotBeNull();
        visited.ShouldBe(["a-unknown", "b-recorded"]); f.PhysicalAppends.ShouldBe(0);
        (await restarted.DrainAsync(1, TestContext.Current.CancellationToken)).ShouldBe(0);
        visited.ShouldBe(["a-unknown", "b-recorded", "a-unknown"]);
        (await restarted.IsReadyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    /// <summary>Automatic H remains fixed while separate exact-original recovery records an aged authoritative absence after restart/lost response, preserving original routing/day/first-seen/sequence.</summary>
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task AgedOriginalUsesOnlySeparateCurrentRecoveryAfterRestart(bool loseResponse)
    {
        var f = new SecuritySpoolFixture(); var intent = SecuritySpoolFixture.Intent(); var original = await f.Spool.ObserveAsync(intent, TestContext.Current.CancellationToken);
        original.ShouldNotBeNull(); f.Clock.Now += PlatformAcceptedEnvelopeTiming.RecoveryHorizon;
        (await f.Spool.DrainAsync(1, TestContext.Current.CancellationToken)).ShouldBe(0); f.PhysicalAppends.ShouldBe(0);
        (await f.Spool.IsReadyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
        var restarted = new ReplicatedSecurityObservationSpool(f.Client, f.Clock, f.Authority, f.Recorder);
        (await restarted.RecoverOriginalAsync(intent, TestContext.Current.CancellationToken)).ShouldBeNull(); f.RecoveryRequests.ShouldBe(0);
        f.RecoveryPermission = true; f.LoseAppendAcknowledgement = loseResponse;
        var recovered = await restarted.RecoverOriginalAsync(intent, TestContext.Current.CancellationToken);
        recovered.ShouldNotBeNull(); (recovered with { Receipt = null }).ShouldBe(original); recovered.Receipt.ShouldBe(SecuritySpoolFixture.Receipt(original));
        f.PhysicalAppends.ShouldBe(1); f.RecoveryRequests.ShouldBe(1);
        await f.Recorder.DidNotReceive().AppendAsync(Arg.Any<SecurityObservationRecord>(), Arg.Any<CancellationToken>());
        var saved = f.Read()!.Records.Single(); saved.ShouldBe(recovered); saved.ObservedAt.ShouldBe(original.ObservedAt); saved.UtcDay.ShouldBe(original.UtcDay); saved.Sequence.ShouldBe(original.Sequence);
        (await restarted.IsReadyAsync(TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await new ReplicatedSecurityObservationSpool(f.Client, f.Clock, f.Authority, f.Recorder).RecoverOriginalAsync(intent, TestContext.Current.CancellationToken)).ShouldBe(recovered);
        f.PhysicalAppends.ShouldBe(1); f.RecoveryRequests.ShouldBe(1);
    }

    /// <summary>Foreign/stale/malformed recovery carriers and unknown/changed source proof retain the exact aged original and cannot borrow automatic append authority.</summary>
    [Theory]
    [InlineData("missing")][InlineData("intent")][InlineData("routing")][InlineData("first-seen")][InlineData("day")][InlineData("sequence")]
    [InlineData("installation")][InlineData("authority")][InlineData("expired")][InlineData("future")][InlineData("unknown")][InlineData("proof")]
    public async Task AgedRecoveryRequiresExactIndependentOriginalAndFreshGrant(string vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        var f = new SecuritySpoolFixture(); var intent = SecuritySpoolFixture.Intent(); var original = await f.Spool.ObserveAsync(intent, TestContext.Current.CancellationToken);
        original.ShouldNotBeNull(); f.Clock.Now += PlatformAcceptedEnvelopeTiming.RecoveryHorizon; f.RecoveryPermission = vector != "missing";
        f.AlterRecoveryGrant = grant => vector switch
        {
            "intent" => grant with { Original = grant.Original with { Intent = grant.Original.Intent with { ObservationId = "foreign" } } },
            "routing" => grant with { Original = grant.Original with { Intent = grant.Original.Intent with { RoutingTenantId = "foreign" } } },
            "first-seen" => grant with { Original = grant.Original with { ObservedAt = f.Clock.Now } },
            "day" => grant with { Original = grant.Original with { UtcDay = "2026-10-07" } },
            "sequence" => grant with { Original = grant.Original with { Sequence = 2 } },
            "installation" => grant with { Target = grant.Target with { InstallationEpoch = "foreign" } },
            "authority" => grant with { RecoveryAuthorityRevision = "foreign" },
            "expired" => grant with { ValidUntil = f.Clock.Now },
            "future" => grant with { ObservedAt = f.Clock.Now.AddMinutes(1) },
            "proof" => grant with { OriginalProof = new(SecurityEventRecorderLookupState.Recorded, SecuritySpoolFixture.Receipt(original)) },
            _ => grant,
        };
        if (vector == "unknown") { f.Recorder.LookupAsync(Arg.Any<SecurityObservationRecord>(), Arg.Any<CancellationToken>()).Returns(new SecurityEventRecorderLookup(SecurityEventRecorderLookupState.Unknown)); }
        string before = System.Text.Json.JsonSerializer.Serialize(f.Read());
        (await f.Spool.RecoverOriginalAsync(intent, TestContext.Current.CancellationToken)).ShouldBeNull();
        f.PhysicalAppends.ShouldBe(0); f.RecoveryRequests.ShouldBe(0); System.Text.Json.JsonSerializer.Serialize(f.Read()).ShouldBe(before);
        (await f.Spool.IsReadyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    /// <summary>Fresh permission withdrawal during an actual source lookup denies a new effect; uncertain recovery acknowledgement retains pending and later exact Recorded proof reconciles without another append.</summary>
    [Theory]
    [InlineData("withdraw")][InlineData("unknown-after-recovery")][InlineData("lost-spool-save")]
    public async Task AgedRecoveryWithdrawalOrUnknownOutcomePreservesOriginal(string vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        var f = new SecuritySpoolFixture(); var intent = SecuritySpoolFixture.Intent(); var original = await f.Spool.ObserveAsync(intent, TestContext.Current.CancellationToken);
        original.ShouldNotBeNull(); f.Clock.Now += PlatformAcceptedEnvelopeTiming.RecoveryHorizon; f.RecoveryPermission = true;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int lookups = 0;
        if (vector == "withdraw")
        {
            f.Recorder.LookupAsync(Arg.Any<SecurityObservationRecord>(), Arg.Any<CancellationToken>()).Returns(async _ =>
            { if (++lookups == 2) { entered.TrySetResult(); await release.Task; } return new SecurityEventRecorderLookup(SecurityEventRecorderLookupState.NotRecorded); });
        }
        if (vector == "unknown-after-recovery")
        {
            f.Recorder.LookupAsync(Arg.Any<SecurityObservationRecord>(), Arg.Any<CancellationToken>()).Returns(_ =>
                ++lookups > 2 ? new SecurityEventRecorderLookup(SecurityEventRecorderLookupState.Unknown) : new(SecurityEventRecorderLookupState.NotRecorded));
        }
        if (vector == "lost-spool-save") { f.FailSave = true; f.FailSaveStage = 2; f.CommitBeforeSaveFault = true; }
        var pending = f.Spool.RecoverOriginalAsync(intent, TestContext.Current.CancellationToken);
        if (vector == "withdraw") { await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); f.RecoveryPermission = false; release.TrySetResult(); }
        (await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).ShouldBeNull();
        if (vector == "withdraw") { f.PhysicalAppends.ShouldBe(0); f.RecoveryRequests.ShouldBe(0); f.Read()!.Records.Single().ShouldBe(original); return; }
        f.PhysicalAppends.ShouldBe(1); f.RecoveryRequests.ShouldBe(1); f.FailSave = false;
        if (vector == "unknown-after-recovery") { (await f.Spool.IsReadyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse(); }
        f.Recorder.LookupAsync(Arg.Any<SecurityObservationRecord>(), Arg.Any<CancellationToken>()).Returns(call => new SecurityEventRecorderLookup(SecurityEventRecorderLookupState.Recorded, f.Recorded[call.Arg<SecurityObservationRecord>().Intent.ObservationId]));
        var recovered = await new ReplicatedSecurityObservationSpool(f.Client, f.Clock, f.Authority, f.Recorder).RecoverOriginalAsync(intent, TestContext.Current.CancellationToken);
        recovered.ShouldNotBeNull(); (recovered with { Receipt = null }).ShouldBe(original); recovered.Receipt.ShouldBe(SecuritySpoolFixture.Receipt(original));
        f.PhysicalAppends.ShouldBe(1); f.RecoveryRequests.ShouldBe(1); (await f.Spool.IsReadyAsync(TestContext.Current.CancellationToken)).ShouldBeTrue();
    }
}
