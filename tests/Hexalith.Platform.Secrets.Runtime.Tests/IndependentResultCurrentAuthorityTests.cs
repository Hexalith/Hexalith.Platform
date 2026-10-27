using Hexalith.Platform.Custody;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class IndependentResultCurrentAuthorityTests
{
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task ActualSecurityObservationLookupRechecksTheCurrentRecorderAfterNativeRead(bool revoke)
    {
        using var fixture=new IndependentAuthorityReceiverTests.Fixture();const string method="security-observation-lookup";
        fixture.State=fixture.State with { Callers=fixture.State.Callers.Select(value=>value with { Role="security-recorder",Methods=[method] }).ToArray() };
        var record=new SecurityObservationRecord(new("observation","tenant","EnvelopeDenied",new string('a',64),"digest-v1"),DateTimeOffset.UtcNow,"2026-10-27",1,null);
        var lookup=new SecurityEventRecorderLookup(SecurityEventRecorderLookupState.NotRecorded,null);
        fixture.RecordingSource.LookupAsync(record,Arg.Any<CancellationToken>()).Returns(_=> { if(revoke)fixture.RevokeCaller();return lookup; });
        var result=await fixture.Client().InvokeAsync<System.Text.Json.JsonElement?>(method,new { record },TestContext.Current.CancellationToken);
        if(revoke) result.ShouldBeNull();else result.ShouldNotBeNull();fixture.RecordingSource.ReceivedCalls().Count().ShouldBe(1);
    }
    [Theory]
    [InlineData("spool")][InlineData("capacity")]
    public async Task FinalConditionalSaveCannotReleaseReadinessAfterItsConsultedPolicyExpires(string policy)
    {
        var clock=new Clock();using var fixture=new IndependentAuthorityReceiverTests.Fixture(clock:clock);
        var target=fixture.Target with { ValidUntil=policy=="spool"?clock.GetUtcNow().AddSeconds(5):clock.GetUtcNow().AddHours(1) };
        fixture.State=fixture.State with { Callers=fixture.State.Callers.Select(value=>value with { Methods=[..value.Methods,"spool-authorize"] }).ToArray(),Spool=target,TransportCapacity=fixture.State.TransportCapacity! with { ValidUntil=policy=="capacity"?clock.GetUtcNow().AddSeconds(5):clock.GetUtcNow().AddHours(1) } };
        fixture.HeadSaveOverride=(_,_)=> { clock.Advance();return null; };
        (await new DaprReplicatedSpoolAuthority(fixture.Client()).AuthorizeAsync(target,"Readiness",null,TestContext.Current.CancellationToken)).ShouldBeFalse();
        fixture.Saves.ShouldBe(1);fixture.RecordingSource.ReceivedCalls().ShouldBeEmpty();
    }
    [Theory]
    [InlineData("unchanged")][InlineData("revoked")][InlineData("scope")][InlineData("expired")][InlineData("cas-lost")]
    public async Task NativeReplayLookupOrdersItsSignedResultAfterTheLastAwaitedRead(string change)
    {
        using var fixture=new IndependentAuthorityReceiverTests.Fixture();string method="replay-lookup-exact";
        fixture.State=fixture.State with { Callers=fixture.State.Callers.Select(value=>value with { Role="replay-registrar",Methods=[method] }).ToArray() };
        var intent=new TrustedEnvelopeReplayIntent("issuer","nonce","tenant","logical","v1",new string('a',64));
        var receipt=new TrustedEnvelopeReplayReceipt(intent,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow.AddDays(7),1,"exact-original");
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.RecordingSource.LookupAsync(intent,Arg.Any<CancellationToken>()).Returns(async _=> { entered.SetResult();await release.Task;return (TrustedEnvelopeReplayReceipt?)receipt; });
        var pending=fixture.Client().InvokeAsync<TrustedEnvelopeReplayReceipt>(method,new { intent },TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3),TestContext.Current.CancellationToken);
        if(change=="revoked") fixture.RevokeCaller();
        if(change is "scope" or "expired") fixture.State=fixture.State with { Callers=fixture.State.Callers.Select(value=>change=="scope"?value with { ScopeReference="substituted" }:value with { ValidUntil=DateTimeOffset.UtcNow.AddSeconds(-1) }).ToArray() };
        if(change=="cas-lost") fixture.RejectHeadPublication=_=>true;
        release.SetResult();var result=await pending;
        if(change=="unchanged") { result.ShouldBe(receipt);fixture.Saves.ShouldBe(1); }else { result.ShouldBeNull();fixture.Saves.ShouldBe(0); }
        System.Text.Json.JsonSerializer.Serialize(receipt,CustodyBindingContract.Json).ShouldContain("exact-original");
        fixture.RecordingSource.ReceivedCalls().Count().ShouldBe(1);
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task DirectSpoolArchiveReadCannotReleaseANewProofAfterRevocation(bool revoke)
    {
        var archive=Substitute.For<IIndependentArchiveStorage>();archive.AvailableBytes(Arg.Any<string>()).Returns(long.MaxValue);
        using var fixture=new IndependentAuthorityReceiverTests.Fixture(archiveStorage:archive);int reads=0;
        archive.AvailableBytes(Arg.Any<string>()).Returns(_=> { reads++;if(revoke) fixture.RevokeCaller();return long.MaxValue; });
        var result=await fixture.Client().InvokeAsync<ReplicatedSecuritySpoolTarget>("spool-current",new { },TestContext.Current.CancellationToken);
        reads.ShouldBeGreaterThan(0);if(revoke) result.ShouldBeNull();else result.ShouldBe(fixture.Target);
    }
    [Fact]
    public async Task CallerCancellationReturnsWhileTheInitialDaprReadAndAllFourSlotsRemainOwned()
    {
        using var fixture=new IndependentAuthorityReceiverTests.Fixture();using var cancelled=new CancellationTokenSource();int reads=0;
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.BeforeRead=async ()=> { if(Interlocked.Increment(ref reads)==4)entered.SetResult();await release.Task; };
        var pending=Enumerable.Range(0,4).Select(_=>fixture.Client().InvokeAsync<ReplicatedSecuritySpoolTarget>("spool-current",new { },cancelled.Token)).ToArray();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3),TestContext.Current.CancellationToken);cancelled.Cancel();
        foreach(var work in pending) await Should.ThrowAsync<OperationCanceledException>(()=>work.WaitAsync(TimeSpan.FromSeconds(3),TestContext.Current.CancellationToken));
        (await fixture.Client().InvokeAsync<ReplicatedSecuritySpoolTarget>("spool-current",new { },TestContext.Current.CancellationToken)).ShouldBeNull();reads.ShouldBe(4);fixture.Saves.ShouldBe(0);
        fixture.BeforeRead=null;release.SetResult();using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while(await fixture.Client().InvokeAsync<ReplicatedSecuritySpoolTarget>("spool-current",new { },deadline.Token) is null) await Task.Delay(10,deadline.Token);
    }
    [Fact]
    public async Task OwnFiniteReceiverBudgetReturns503BeforeAnIgnoringInitialReadCompletes()
    {
        using var fixture=new IndependentAuthorityReceiverTests.Fixture();var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.BeforeRead=async ()=> { entered.SetResult();await release.Task; };
        var context=new Microsoft.AspNetCore.Http.DefaultHttpContext();context.Request.Headers["dapr-api-token"]="isolated-channel";context.Request.Headers.Authorization="Bearer "+fixture.Bearer;
        context.Request.Body=new MemoryStream(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { Protocol="platform-independent-authority-v1",EnrollmentId="isolated-enrollment",Scope="isolated-owner",TrustRevision="current-1",Method="spool-current",Nonce=new string('a',32),Payload=new { } },CustodyBindingContract.Json));
        try
        {
            var pending=fixture.Receiver.HandleAsync(context,"spool-current",TestContext.Current.CancellationToken);await entered.Task.WaitAsync(TimeSpan.FromSeconds(3),TestContext.Current.CancellationToken);
            var result=await pending.WaitAsync(TimeSpan.FromSeconds(32),TestContext.Current.CancellationToken);((Microsoft.AspNetCore.Http.IStatusCodeHttpResult)result).StatusCode.ShouldBe(503);fixture.Saves.ShouldBe(0);
        }
        finally { release.TrySetResult();fixture.BeforeRead=null; }
    }
    private sealed class Clock:TimeProvider
    {
        private DateTimeOffset _now=DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow()=>_now;
        internal void Advance()=>_now=_now.AddSeconds(6);
    }
}
