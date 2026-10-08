using System.Text;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Custody.Tests;

/// <summary>Actual component/CAS/recovery source and serialized persisted end-state under synthetic authorities/backends; no replica, backup or production proof.</summary>
public sealed class ReplicatedSecurityObservationSpoolTests
{
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
    [InlineData(false)][InlineData(true)]
    public async Task ComponentSaveFaultDistinguishesPersistedEndState(bool committed)
    {
        var f = new SecuritySpoolFixture { FailSave = true, CommitBeforeSaveFault = committed };
        var result = await f.Spool.ObserveAsync(SecuritySpoolFixture.Intent(), TestContext.Current.CancellationToken);
        (result is not null).ShouldBe(committed); (f.Persisted is not null).ShouldBe(committed); f.Anchor.ShouldBe(1);
        (await f.Spool.IsReadyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse(); f.PhysicalAppends.ShouldBe(0);
        if (committed) { f.Read()!.Records.Single().Receipt.ShouldBeNull(); }
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

}
