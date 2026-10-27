using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class QualificationHttpOwnershipTests
{
    [Theory]
    [InlineData("executor","auth")][InlineData("observer","auth")][InlineData("enrollment","auth")]
    [InlineData("executor","body")][InlineData("observer","provider")][InlineData("executor","retain")]
    public async Task ActualOuterReceiverKeepsLateChildrenChargedAfterOneAcquisitionDeadline(string role,string blocked)
    {
        var token=TestContext.Current.CancellationToken;var receiver=new QualificationHttpReceiver();var configuration=QualificationHostTests.Configuration() with { CallTimeout=TimeSpan.FromMilliseconds(150) };
        var gate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);int launches=0;
        var auth=Substitute.For<IQualificationCallerAuthenticator>();
        auth.AuthenticateAsync(Arg.Any<HttpContext>(),role,Arg.Any<CancellationToken>()).Returns(async _=> { if(blocked=="auth") { if(Interlocked.Increment(ref launches)==4)entered.TrySetResult();await gate.Task; }return (QualificationCallerContext?)new QualificationCallerContext("fixture-caller","fixture-scope"); });
        var provider=Substitute.For<IQualificationRoleProvider>();provider.Role.Returns(role);
        provider.AuthorizeAsync(Arg.Any<JsonObject>(),Arg.Any<QualificationCallerContext>(),Arg.Any<CancellationToken>()).Returns(true);
        provider.InvokeAsync(Arg.Any<string>(),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()).Returns(async _=> { if(blocked=="provider") { if(Interlocked.Increment(ref launches)==4)entered.TrySetResult();await gate.Task; }return QualificationHostTests.RolePayload(role) is { } payload?new JsonObject { ["payload"]=payload,["signature"]="fixture" }:null; });
        var store=Substitute.For<IQualificationOriginalStore>();store.BeginAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()).Returns(true);
        store.RetainAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()).Returns(async _=> { if(blocked=="retain") { if(Interlocked.Increment(ref launches)==4)entered.TrySetResult();await gate.Task; } });
        var verifier=Substitute.For<IQualificationPublicProofVerifier>();verifier.Verify(Arg.Any<string>(),Arg.Any<JsonObject>(),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()).Returns(true);verifier.VerifyOriginal(Arg.Any<string>(),Arg.Any<JsonObject>(),Arg.Any<JsonObject>(),Arg.Any<long>(),Arg.Any<CancellationToken>()).Returns(true);
        string operation=role=="executor"?"execute":role=="observer"?"observe":"enroll";
        var contexts=Enumerable.Range(0,4).Select(_=>Context(QualificationHostTests.Request(role).ToJsonString())).ToArray();
        if(blocked=="body") foreach(var context in contexts)context.Request.Body=new WaitingRead(gate.Task,()=> { if(Interlocked.Increment(ref launches)==4)entered.TrySetResult(); });
        var pending=contexts.Select(context=>receiver.HandleAsync(role,operation,configuration,context,"fixture-channel",auth,provider,store,verifier,token)).ToArray();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3),token);
        long before=IndependentReceivingDiagnostics.Count(IndependentReceivingFailure.CapacitySaturated);
        foreach(string body in new[]{"{",new string('x',1024*1024),QualificationHostTests.Request(role).ToJsonString()})
        {
            var context=Context(body);var counted=new CountedRead(body);context.Request.Body=counted;
            await (await receiver.HandleAsync(role,operation,configuration,context,"fixture-channel",auth,provider,store,verifier,token)).ExecuteAsync(context);
            context.Response.StatusCode.ShouldBe(503);counted.Reads.ShouldBe(0);
        }
        IndependentReceivingDiagnostics.Count(IndependentReceivingFailure.CapacitySaturated).ShouldBeGreaterThanOrEqualTo(before+3);
        var results=await Task.WhenAll(pending).WaitAsync(TimeSpan.FromSeconds(3),token);
        for(int index=0;index<results.Length;index++) await Should.ThrowAsync<OperationCanceledException>(()=>results[index].ExecuteAsync(contexts[index]));
        var stillFull=Context(QualificationHostTests.Request(role).ToJsonString());var untouched=new CountedRead(QualificationHostTests.Request(role).ToJsonString());stillFull.Request.Body=untouched;
        await (await receiver.HandleAsync(role,operation,configuration,stillFull,"fixture-channel",auth,provider,store,verifier,token)).ExecuteAsync(stillFull);stillFull.Response.StatusCode.ShouldBe(503);untouched.Reads.ShouldBe(0);launches.ShouldBe(4);
        gate.TrySetResult();
        await EventuallyReusable(receiver,configuration,token);launches.ShouldBe(4);
    }
    [Fact]
    public async Task ActualResponseDeliveryAndClientCancellationKeepAllFourOwnersUntilTransportSettles()
    {
        var receiver=new QualificationHttpReceiver();var token=TestContext.Current.CancellationToken;using var cancellation=new CancellationTokenSource();
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);int writers=0;
        var contexts=Enumerable.Range(0,4).Select(_=>Context("{}")).ToArray();var deliveries=new List<Task>();
        foreach(var context in contexts)
        {
            context.Response.Body=new WaitingWrite(release.Task,()=> { if(Interlocked.Increment(ref writers)==4)entered.TrySetResult(); });
            var result=await receiver.HandleAsync("executor","execute",QualificationHostTests.Configuration(),context,"wrong-channel",null,null,null,null,cancellation.Token);
            deliveries.Add(result.ExecuteAsync(context));
        }
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3),token);cancellation.Cancel();
        var saturated=Context("{}");await (await receiver.HandleAsync("executor","execute",QualificationHostTests.Configuration(),saturated,"fixture-channel",null,null,null,null,token)).ExecuteAsync(saturated);saturated.Response.StatusCode.ShouldBe(503);
        deliveries.ShouldAllBe(work=>!work.IsCompleted);release.TrySetResult();await Task.WhenAll(deliveries).WaitAsync(TimeSpan.FromSeconds(3),token);
        await EventuallyReusable(receiver,QualificationHostTests.Configuration(),token);
    }
    [Theory]
    [InlineData("executor","execute")][InlineData("observer","observe")][InlineData("enrollment","enroll")]
    public async Task ActualLoopbackEntrypointRetainsLateAuthenticationAfterClientLossAndEventuallyReusesCapacity(string role,string operation)
    {
        var token=TestContext.Current.CancellationToken;var receiver=new QualificationHttpReceiver();var auth=Substitute.For<IQualificationCallerAuthenticator>();
        var gate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);int calls=0;
        auth.AuthenticateAsync(Arg.Any<HttpContext>(),role,Arg.Any<CancellationToken>()).Returns(async _=> { if(Interlocked.Increment(ref calls)==4)entered.TrySetResult();await gate.Task;return (QualificationCallerContext?)null; });
        var builder=WebApplication.CreateSlimBuilder();builder.Logging.ClearProviders();builder.WebHost.UseUrls("http://127.0.0.1:0");await using var app=builder.Build();
        var configuration=QualificationHostTests.Configuration() with { CallTimeout=TimeSpan.FromMilliseconds(300) };
        app.MapPost("/ext-secrets-1/v1/"+operation,(HttpContext context,CancellationToken caller)=>receiver.HandleAsync(role,operation,configuration,context,"fixture-channel",auth,null,null,null,caller));
        await app.StartAsync(token);using var client=new HttpClient { BaseAddress=new Uri(app.Urls.Single()),Timeout=TimeSpan.FromSeconds(3) };
        async Task<HttpResponseMessage?> Send(string channel)
        {
            using var request=new HttpRequestMessage(HttpMethod.Post,"/ext-secrets-1/v1/"+operation) { Content=new StringContent("{}") };request.Headers.Add("dapr-api-token",channel);
            try { return await client.SendAsync(request,token); }catch(HttpRequestException) { return null; }
        }
        try
        {
            var pending=Enumerable.Range(0,4).Select(_=>Send("fixture-channel")).ToArray();await entered.Task.WaitAsync(TimeSpan.FromSeconds(3),token);
            foreach(var result in await Task.WhenAll(pending))result?.Dispose();calls.ShouldBe(4);
            using(var saturated=await Send("fixture-channel")) { saturated.ShouldNotBeNull();saturated.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);(await saturated.Content.ReadAsByteArrayAsync(token)).ShouldBeEmpty(); }
            calls.ShouldBe(4);gate.TrySetResult();
            using var budget=CancellationTokenSource.CreateLinkedTokenSource(token);budget.CancelAfter(TimeSpan.FromSeconds(3));
            while(true)
            {
                using var result=await Send("wrong-channel");result.ShouldNotBeNull();
                if((await result.Content.ReadAsStringAsync(token)).Length>0)break;await Task.Delay(10,budget.Token);
            }
            calls.ShouldBe(4);
        }
        finally { gate.TrySetResult();await app.StopAsync(token); }
    }
    [Fact]
    public async Task CallerCancellationPropagatesBeforeAuthenticationCompositionOrPublicVerification()
    {
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();var auth=Substitute.For<IQualificationCallerAuthenticator>();
        await Should.ThrowAsync<OperationCanceledException>(()=>new QualificationHttpReceiver().HandleAsync("executor","execute",QualificationHostTests.Configuration(),Context("{}"),"fixture-channel",auth,null,null,null,cancelled.Token));
        auth.ReceivedCalls().ShouldBeEmpty();var services=new ServiceCollection();
        Should.Throw<OperationCanceledException>(()=>services.AddInstalledQualificationRole("executor",null,cancelled.Token));services.ShouldBeEmpty();
        var verifier=new QualificationProofVerifier(new Dictionary<string,QualificationProofBinding>());
        Should.Throw<OperationCanceledException>(()=>verifier.Verify("executor",new(),new(),cancelled.Token));
        Should.Throw<OperationCanceledException>(()=>verifier.VerifyOriginal("executor",new(),new(),0,cancelled.Token));
    }
    private static DefaultHttpContext Context(string body)
    {
        var context=new DefaultHttpContext { RequestServices=new ServiceCollection().AddLogging().BuildServiceProvider() };
        context.Request.Headers["dapr-api-token"]="fixture-channel";context.Request.Body=new MemoryStream(Encoding.UTF8.GetBytes(body));context.Response.Body=new MemoryStream();return context;
    }
    private static async Task EventuallyReusable(QualificationHttpReceiver receiver,SecretsRuntimeConfiguration configuration,CancellationToken token)
    {
        using var budget=CancellationTokenSource.CreateLinkedTokenSource(token);budget.CancelAfter(TimeSpan.FromSeconds(3));
        while(true)
        {
            var context=Context("{}");var result=await receiver.HandleAsync("observer","observe",configuration,context,"wrong-channel",null,null,null,null,budget.Token);
            await result.ExecuteAsync(context);if(context.Response.Body.Length>0) return;await Task.Delay(10,budget.Token);
        }
    }
    private sealed class WaitingRead(Task gate,Action entered):MemoryStream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken token=default)
        { entered();await gate;return 0; }
    }
    private sealed class CountedRead(string body):MemoryStream(Encoding.UTF8.GetBytes(body))
    {
        internal int Reads;
        public override ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken token=default) { Reads++;return base.ReadAsync(buffer,token); }
    }
    private sealed class WaitingWrite(Task gate,Action entered):MemoryStream
    {
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,CancellationToken token=default)
        { entered();await gate;await base.WriteAsync(buffer,CancellationToken.None); }
    }
}
