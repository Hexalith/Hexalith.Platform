using System.Text;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using System.Text.Json;
using Hexalith.Platform.Secrets.Runtime;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class Pass13OwnershipTests
{
    [Theory]
    [InlineData("success")][InlineData("failure")][InlineData("cancel")]
    public async Task ActualPrivateSpanCallbackClearsOwnedInputAndDecodeArrays(string outcome)
    {
        string file=Path.GetTempFileName();byte[]? owned=null;char[]? decoded=null;using var cancelled=new CancellationTokenSource();
        try
        {
            await File.WriteAllTextAsync(file,"  fixture-private-key-material  ",TestContext.Current.CancellationToken);
            var operation=BoundedRegularFile.UseUtf8SpanWithInspectionAsync(file,4096,text=>
            { text.SequenceEqual("fixture-private-key-material").ShouldBeTrue();if(outcome=="failure") throw new InvalidOperationException("fixture callback failure");return true; },cancelled.Token,(bytes,chars)=> { owned=bytes;decoded=chars;if(outcome=="cancel") cancelled.Cancel(); });
            if(outcome=="success") (await operation).ShouldBeTrue();else if(outcome=="failure") await Should.ThrowAsync<InvalidOperationException>(()=>operation);else await Should.ThrowAsync<OperationCanceledException>(()=>operation);
            owned.ShouldNotBeNull().ShouldAllBe(value=>value==0);decoded.ShouldNotBeNull().ShouldAllBe(value=>value=='\0');
        }
        finally { File.Delete(file); }
    }
    [Fact]
    public async Task ReceivingSlotsCoverHeldBodiesAndUnwrittenResponsesAndReleaseOnFailedWrite()
    {
        using var fixture=new IndependentAuthorityReceiverTests.Fixture();var releases=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var streams=Enumerable.Range(0,4).Select(_=>new HeldBody(Frame(),releases.Task)).ToArray();var contexts=streams.Select(Context).ToArray();
        var pending=contexts.Select(context=>fixture.Receiver.HandleAsync(context,"spool-current",TestContext.Current.CancellationToken)).ToArray();
        await Task.WhenAll(streams.Select(stream=>stream.Entered.Task)).WaitAsync(TimeSpan.FromSeconds(3),TestContext.Current.CancellationToken);
        var refused=Context(new NoRead());await (await fixture.Receiver.HandleAsync(refused,"spool-current",TestContext.Current.CancellationToken)).ExecuteAsync(refused);refused.Response.StatusCode.ShouldBe(503);
        releases.SetResult();var results=await Task.WhenAll(pending);
        refused=Context(new NoRead());await (await fixture.Receiver.HandleAsync(refused,"spool-current",TestContext.Current.CancellationToken)).ExecuteAsync(refused);refused.Response.StatusCode.ShouldBe(503);
        contexts[0].Response.Body=new FailedWrite();await Should.ThrowAsync<IOException>(()=>results[0].ExecuteAsync(contexts[0]));
        var accepted=Context(new MemoryStream(Frame(2*1024*1024)));await (await fixture.Receiver.HandleAsync(accepted,"spool-current",TestContext.Current.CancellationToken)).ExecuteAsync(accepted);accepted.Response.StatusCode.ShouldBe(200);
        for(int i=1;i<4;i++) await results[i].ExecuteAsync(contexts[i]);
    }
    [Fact]
    public async Task ActualLoopbackRouteRefusesSaturatedReceiverBeforeBodyAndAcceptsLargeSupportedFrameAfterRelease()
    {
        using var fixture=new IndependentAuthorityReceiverTests.Fixture();await using var app=IndependentAuthorityHttpApplication.BuildApplication(fixture.Receiver);
        app.Urls.Add("http://127.0.0.1:0");await app.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            var contexts=Enumerable.Range(0,4).Select(_=>Context(new MemoryStream(Frame()))).ToArray();
            var held=await Task.WhenAll(contexts.Select(context=>fixture.Receiver.HandleAsync(context,"spool-current",TestContext.Current.CancellationToken)));
            using var http=new HttpClient();string address=app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            async Task<System.Net.HttpStatusCode> Send(int size)
            {
                using var request=new HttpRequestMessage(HttpMethod.Post,address+"/platform-independent/v1/spool-current") { Content=new ByteArrayContent(Frame(size)) };
                request.Headers.Add("dapr-api-token","isolated-channel");request.Headers.Authorization=new("Bearer","isolated-caller");
                using var response=await http.SendAsync(request,TestContext.Current.CancellationToken);await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);return response.StatusCode;
            }
            (await Send(0)).ShouldBe(System.Net.HttpStatusCode.ServiceUnavailable);
            for(int i=0;i<4;i++) await held[i].ExecuteAsync(contexts[i]);
            (await Send(16*1024*1024)).ShouldBe(System.Net.HttpStatusCode.OK);
        }
        finally { await app.StopAsync(TestContext.Current.CancellationToken); }
    }
    [Fact]
    public async Task ActualBuiltServiceRunsBeyondStartupCancellationAndStopsWithCallerLifetime()
    {
        using var fixture=new IndependentAuthorityReceiverTests.Fixture();await using var app=IndependentAuthorityHttpApplication.BuildApplication(fixture.Receiver);
        app.Urls.Add("http://127.0.0.1:0");using var startup=new CancellationTokenSource();using var lifetime=new CancellationTokenSource();
        var work=IndependentAuthorityHttpApplication.RunServiceAsync(app,startup.Token,lifetime.Token);
        await Task.Delay(100,TestContext.Current.CancellationToken);startup.Cancel();await Task.Delay(80,TestContext.Current.CancellationToken);work.IsCompleted.ShouldBeFalse();
        lifetime.Cancel();await work.WaitAsync(TimeSpan.FromSeconds(3),TestContext.Current.CancellationToken);
    }
    private static byte[] Frame(int padding=0)=>JsonSerializer.SerializeToUtf8Bytes(new { Protocol="platform-independent-authority-v1",EnrollmentId="isolated-enrollment",Scope="isolated-owner",TrustRevision="current-1",Method="spool-current",Nonce=new string('A',32),Payload=new { padding=new string('x',padding) } },CustodyBindingContract.Json);
    private static DefaultHttpContext Context(Stream body)
    {
        var context=new DefaultHttpContext { RequestServices=new ServiceCollection().AddLogging().BuildServiceProvider() };
        context.Request.Headers["dapr-api-token"]="isolated-channel";context.Request.Headers.Authorization="Bearer isolated-caller";
        context.Request.Body=body;context.Response.Body=new MemoryStream();return context;
    }
    private sealed class HeldBody(byte[] bytes,Task release):MemoryStream(bytes)
    {
        internal readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken token=default) { Entered.TrySetResult();await release.WaitAsync(token);return await base.ReadAsync(buffer,token); }
    }
    private sealed class NoRead:MemoryStream { public override ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken token=default)=>throw new Exception("Saturated receiving allocated a body."); }
    private sealed class FailedWrite:MemoryStream { public override ValueTask WriteAsync(ReadOnlyMemory<byte> bytes,CancellationToken token=default)=>throw new IOException("Fixture response write failure."); }
}
