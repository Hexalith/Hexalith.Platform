using Hexalith.Platform.Custody;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class IndependentRecoveryArchiveTests
{
    [Fact]
    public async Task ExpiredExactGrantRenewsWithoutChangingOriginalAndAllIdentitiesSurviveOutsideBoundedCurrentHead()
    {
        var clock=new Clock();using var fixture=new IndependentAuthorityReceiverTests.Fixture(clock:clock);
        fixture.State=fixture.State with { Callers=fixture.State.Callers.Select(value=>value with { Role="aged-recovery",Methods=["spool-admit-recovery","spool-verify-recovery"] }).ToArray() };
        var record=new SecurityObservationRecord(new("observation","tenant","invalid-tag",new string('a',64),"v1"),clock.Now,clock.Now.ToString("yyyy-MM-dd"),1,null);
        var proof=new SecurityEventRecorderLookup(SecurityEventRecorderLookupState.NotRecorded,NoRecordProof:new(PrivateRecordingIdentity.SecurityOriginalMessageId("observation"),PrivateRecordingIdentity.SecurityOriginalMessageId("observation"),new string('a',64),"fixture-native-source",0,new string('b',64),null,"RetainedUnsent"));
        fixture.RecordingSource.LookupAsync(record,Arg.Any<CancellationToken>()).Returns(proof);
        var authority=new DaprReplicatedSpoolAuthority(fixture.Client());
        var first=await authority.AuthorizeAgedOriginalRecoveryAsync(fixture.Target,record,proof,TestContext.Current.CancellationToken);first.ShouldNotBeNull();
        var duplicate=await authority.AuthorizeAgedOriginalRecoveryAsync(fixture.Target,record,proof,TestContext.Current.CancellationToken);duplicate.ShouldBe(first);
        clock.Now=clock.Now.AddMinutes(6);
        var renewed=await authority.AuthorizeAgedOriginalRecoveryAsync(fixture.Target,record,proof,TestContext.Current.CancellationToken);renewed.ShouldNotBeNull();renewed.ShouldNotBe(first);renewed.Original.ShouldBe(first.Original);renewed.Original.ObservedAt.ShouldBe(record.ObservedAt);
        fixture.State.Recoveries.Count.ShouldBe(1);
        Directory.GetFiles(Path.Combine(fixture.ArchiveDirectory,"recovery-grants"),"*.json",SearchOption.AllDirectories).Length.ShouldBe(2);
        (await authority.VerifyAgedOriginalRecoveryAsync(first,proof,TestContext.Current.CancellationToken)).ShouldBeFalse();
        fixture.State=fixture.State with { Recoveries=[] };fixture.Restart();authority=new(fixture.Client());
        (await authority.VerifyAgedOriginalRecoveryAsync(renewed,proof,TestContext.Current.CancellationToken)).ShouldBeTrue();
        fixture.State.Recoveries.ShouldBeEmpty();
        fixture.State=fixture.State with { Callers=fixture.State.Callers.Select(value=>value with { Revoked=true }).ToArray() };
        (await authority.VerifyAgedOriginalRecoveryAsync(renewed,proof,TestContext.Current.CancellationToken)).ShouldBeFalse();
    }
    private sealed class Clock:TimeProvider
    {
        internal DateTimeOffset Now=DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow()=>Now;
    }
}
