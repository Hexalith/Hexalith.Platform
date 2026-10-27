using Hexalith.EventStore.Contracts.Security;
using System.Security.Cryptography;
using Hexalith.Platform.Custody;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class SpoolPublicationReservationTests
{
    [Fact]
    public async Task ConcurrentLargeTransitionsReserveBeforeActualArchiveWriteAndLosingCasDoesNotConsumeWinningCompletionFloor()
    {
        const long capacity=40L*1024*1024;IndependentAuthorityReceiverTests.Fixture? current=null;
        var storage=Substitute.For<IIndependentArchiveStorage>();storage.AvailableBytes(Arg.Any<string>()).Returns(_=>capacity-(current is null?0:Directory.EnumerateFiles(current.ArchiveDirectory,"*",SearchOption.AllDirectories).Sum(path=>new FileInfo(path).Length)));
        using var fixture=new IndependentAuthorityReceiverTests.Fixture(storage);current=fixture;
        var transitions=Enumerable.Range(0,4).Select(index=>
        { byte[] bytes=new byte[4*1024*1024];bytes.AsSpan().Fill((byte)(index+1));return new AnchoredStateTransition(fixture.Scope,0,1,new string('A',64),Convert.ToHexString(SHA256.HashData(bytes)),bytes); }).ToArray();
        var results=await Task.WhenAll(transitions.Select(value=>new DaprReplicatedSpoolAuthority(fixture.Client()).AdmitTransitionAsync(value,TestContext.Current.CancellationToken)));
        results.Count(value=>value).ShouldBe(1);var winner=transitions[Array.FindIndex(results,value=>value)];
        Directory.EnumerateFiles(fixture.ArchiveDirectory,"*",SearchOption.AllDirectories).Count(path=>new FileInfo(path).Length>1024*1024).ShouldBe(1);
        fixture.State.SpoolPublication!.TransitionSha256.ShouldNotBeNull();fixture.Restart();
        (await new DaprReplicatedSpoolAuthority(fixture.Client()).RecordTransitionAsync(winner,TestContext.Current.CancellationToken)).ShouldBeTrue();
        fixture.State.SpoolPublication.ShouldBeNull();fixture.State.SpoolRevision.ShouldBe(1);
        Directory.EnumerateFiles(fixture.ArchiveDirectory,"*",SearchOption.AllDirectories).Count(path=>new FileInfo(path).Length>1024*1024).ShouldBe(2);
    }
    [Fact]
    public async Task LegacyArchivedAdmissionAcquiresSurvivingCompletionReservationBeforeAnyNewLargeWrite()
    {
        using var fixture=new IndependentAuthorityReceiverTests.Fixture();byte[] body=new byte[1024*1024];body.AsSpan().Fill(27);
        var original=new AnchoredStateTransition(fixture.Scope,0,1,new string('A',64),Convert.ToHexString(SHA256.HashData(body)),body);var authority=new DaprReplicatedSpoolAuthority(fixture.Client());
        (await authority.AdmitTransitionAsync(original,TestContext.Current.CancellationToken)).ShouldBeTrue();fixture.State=fixture.State with { SpoolPublication=null };fixture.Restart();
        fixture.RejectHeadPublication=next=>next.SpoolPublication is not null;
        (await authority.RecordTransitionAsync(original,TestContext.Current.CancellationToken)).ShouldBeFalse();fixture.State.SpoolRevision.ShouldBe(0);
        Directory.EnumerateFiles(fixture.ArchiveDirectory,"*",SearchOption.AllDirectories).Count(path=>new FileInfo(path).Length>1024*1024).ShouldBe(1);
        fixture.RejectHeadPublication=null;fixture.Restart();(await authority.RecordTransitionAsync(original,TestContext.Current.CancellationToken)).ShouldBeTrue();fixture.State.SpoolPublication.ShouldBeNull();
    }
    [Fact]
    public async Task LostHeadPublicationKeepsStableReservationAndExactArchiveAcrossRestart()
    {
        using var fixture=new IndependentAuthorityReceiverTests.Fixture();byte[] bytes=new byte[2*1024*1024];bytes.AsSpan().Fill(31);
        var transition=new AnchoredStateTransition(fixture.Scope,0,1,new string('A',64),Convert.ToHexString(SHA256.HashData(bytes)),bytes);
        var authority=new DaprReplicatedSpoolAuthority(fixture.Client());fixture.RejectHeadPublication=next=>next.SpoolPublication is not null;
        (await authority.AdmitTransitionAsync(transition,TestContext.Current.CancellationToken)).ShouldBeFalse();Directory.EnumerateFiles(fixture.ArchiveDirectory,"*",SearchOption.AllDirectories).Count().ShouldBe(1);
        fixture.RejectHeadPublication=null;(await authority.AdmitTransitionAsync(transition,TestContext.Current.CancellationToken)).ShouldBeTrue();
        fixture.RejectHeadPublication=next=>next.SpoolRevision>fixture.State.SpoolRevision;
        (await authority.RecordTransitionAsync(transition,TestContext.Current.CancellationToken)).ShouldBeFalse();fixture.State.SpoolPublication.ShouldNotBeNull();fixture.Restart();fixture.RejectHeadPublication=null;
        (await authority.RecoverTransitionAsync(transition,TestContext.Current.CancellationToken)).ShouldBeTrue();fixture.State.SpoolPublication.ShouldBeNull();
        Directory.EnumerateFiles(fixture.ArchiveDirectory,"*",SearchOption.AllDirectories).Count(path=>new FileInfo(path).Length>1024*1024).ShouldBe(2);
    }
}
