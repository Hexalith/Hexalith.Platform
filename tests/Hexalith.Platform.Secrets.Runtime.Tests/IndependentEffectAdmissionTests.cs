using System.Text.Json;
using Hexalith.Platform.Custody;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class IndependentEffectAdmissionTests
{
    [Fact]
    public async Task RevocationWinningTheSurvivingAdmissionCasDeniesReplayAndCustomOwnerBeforeEffects()
    {
        foreach(bool custom in new[]{false,true})
        {
            var handler=new Handler();using var fixture=new IndependentAuthorityReceiverTests.Fixture(handlers:[handler]);Configure(fixture,custom);
            fixture.RejectHeadPublication=next=>
            {
                if(next.PrivateOriginalsCount>fixture.State.PrivateOriginalsCount) { fixture.RevokeCaller();return true; }return false;
            };
            _=await fixture.Client().InvokeAsync<JsonElement>(custom?"fixture-owner-effect":"replay-register-exact",Payload(custom),TestContext.Current.CancellationToken);
            handler.Effects.ShouldBe(0);fixture.RecordingSource.ReceivedCalls().ShouldBeEmpty();
        }
    }
    [Fact]
    public async Task AdmissionWinningBeforeRevocationDoesNotHoldNativeWorkLockAndLaterCallsDeny()
    {
        foreach(bool custom in new[]{false,true})
        {
            var admitted=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var finish=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var handler=new Handler { Started=admitted,Finish=finish };using var fixture=new IndependentAuthorityReceiverTests.Fixture(handlers:[handler]);Configure(fixture,custom);
            fixture.RecordingSource.RegisterAsync(Arg.Any<TrustedEnvelopeReplayIntent>(),Arg.Any<DateTimeOffset>(),Arg.Any<TimeSpan>(),Arg.Any<CancellationToken>()).Returns(async call=>
            { admitted.TrySetResult();await finish.Task;return (TrustedEnvelopeReplayReceipt?)new TrustedEnvelopeReplayReceipt(call.ArgAt<TrustedEnvelopeReplayIntent>(0),DateTimeOffset.UtcNow,DateTimeOffset.UtcNow.AddDays(7),1,"actual-isolated-native-receipt"); });
            var task=fixture.Client().InvokeAsync<JsonElement>(custom?"fixture-owner-effect":"replay-register-exact",Payload(custom),TestContext.Current.CancellationToken);
            await admitted.Task.WaitAsync(TimeSpan.FromSeconds(5),TestContext.Current.CancellationToken);fixture.RevokeCaller();finish.SetResult();await task;
            fixture.Restart();_=await fixture.Client().InvokeAsync<JsonElement>(custom?"fixture-owner-effect":"replay-register-exact",Payload(custom),TestContext.Current.CancellationToken);
            if(custom) handler.Effects.ShouldBe(1);else await fixture.RecordingSource.Received(1).RegisterAsync(Arg.Any<TrustedEnvelopeReplayIntent>(),Arg.Any<DateTimeOffset>(),Arg.Any<TimeSpan>(),Arg.Any<CancellationToken>());
        }
    }
    [Fact]
    public async Task ConcurrentOrLostAdmissionCannotDispatchUnknownOriginalAgainWithANewFrameNonce()
    {
        var handler=new Handler { Lose=true };using var fixture=new IndependentAuthorityReceiverTests.Fixture(handlers:[handler]);Configure(fixture,true);
        _=await Task.WhenAll(Enumerable.Range(0,4).Select(_=>fixture.Client().InvokeAsync<JsonElement>("fixture-owner-effect",Payload(true),TestContext.Current.CancellationToken)));
        fixture.Restart();_=await fixture.Client().InvokeAsync<JsonElement>("fixture-owner-effect",Payload(true),TestContext.Current.CancellationToken);
        handler.Effects.ShouldBe(1);fixture.State.PrivateOriginalsCount.ShouldBe(1);fixture.State.PendingOriginalCompletionBytes.ShouldBe(0);
    }
    private static object Payload(bool custom)=>custom?new { operationId="exact-original",payload="unchanged" }:new { intent=new TrustedEnvelopeReplayIntent("issuer","nonce","tenant","logical","v1",new string('A',64)),evaluatedAt=new DateTimeOffset(2026,10,25,0,0,0,TimeSpan.Zero),retentionTicks=TimeSpan.FromSeconds(604800).Ticks };
    private static void Configure(IndependentAuthorityReceiverTests.Fixture fixture,bool custom)=>fixture.State=fixture.State with { Callers=fixture.State.Callers.Select(value=>value with { Role=custom?"executor":"replay-registrar",Methods=[custom?"fixture-owner-effect":"replay-register-exact"] }).ToArray() };
    private sealed class Handler:IIndependentOwnerOperationHandler
    {
        public IReadOnlySet<string> Methods { get; }=new HashSet<string>(["fixture-owner-effect"]);internal int Effects;internal bool Lose;internal TaskCompletionSource? Started,Finish;
        public async Task<object?> HandleAsync(string method,JsonElement payload,IndependentCallerGrant caller,IndependentAuthorityState state,CancellationToken token)
        { Interlocked.Increment(ref Effects);Started?.TrySetResult();if(Finish is not null) await Finish.Task.WaitAsync(token);if(Lose) throw new IOException("Lost native outcome");return new { receipt="exact-native-original" }; }
    }
}
