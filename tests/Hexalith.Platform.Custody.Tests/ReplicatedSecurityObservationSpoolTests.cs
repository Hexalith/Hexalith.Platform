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

    /// <summary>Retained acknowledged originals still support exact lookup/duplicates, but cannot claim capacity for another identity.</summary>
    [Fact]
    public async Task FullAcknowledgedSpoolIsNotReadyAndKeepsOriginals()
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
        byte[] exact = f.Persisted.ToArray();

        (await f.Spool.IsReadyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await f.Spool.LookupAsync(records[0].Intent, TestContext.Current.CancellationToken)).ShouldBe(records[0]);
        (await f.Spool.ObserveAsync(records[0].Intent, TestContext.Current.CancellationToken)).ShouldBe(records[0]);
        (await f.Spool.ObserveAsync(SecuritySpoolFixture.Intent("new-original"), TestContext.Current.CancellationToken)).ShouldBeNull();
        f.Persisted.ShouldBe(exact); f.PendingBytes.ShouldBeNull(); f.PhysicalAppends.ShouldBe(0);
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
