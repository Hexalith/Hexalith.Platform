using System.Security.Cryptography;
using System.Text;
using Dapr.Client;
using Hexalith.Platform.Secrets.Runtime;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Hexalith.Platform.Protection.Tests;

// Isolated real receiving handler, independent in-memory test transport/storage and ephemeral fixture anchors only.
internal sealed class ReceivingAuthorityFixture : ISecretsCredentialBoundDaprInvoker, IDisposable
{
    private readonly string _channel = Path.GetTempFileName();
    private readonly string _archive = Path.Combine(Path.GetTempPath(),"story54-receiving-archive-"+Guid.NewGuid().ToString("N"));
    internal readonly RSA ProofKey=RSA.Create(2048);
    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly DaprClient _dapr = Substitute.For<DaprClient>();
    private readonly IndependentAuthorityReceiver _receiver;
    private int _revision;
    private readonly TimeProvider _clock;
    internal IndependentAuthorityState State;
    internal string ArchiveDirectory=>_archive;
    internal IndependentAuthorityReceiver Receiver=>_receiver;
    internal readonly List<string> Methods=[];
    internal bool LoseRetentionResponse;
    internal bool FailResponseTransport;
    internal string? DropNextResponseForMethod;
    internal Action? BeforeConditionalSave;
    internal Func<Task>? BeforeRead;
    internal IndependentDaprAuthority Authority => new(this,new("isolated-authority","file:/isolated",_key.ExportSubjectPublicKeyInfoPem(),"isolated-enrollment","isolated-owner","trust-1"),_clock);
    internal IndependentDaprAuthority AuthorityForBearer(string bearer)=>new(new Caller(this,bearer),new("isolated-authority","file:/isolated",_key.ExportSubjectPublicKeyInfoPem(),"isolated-enrollment","isolated-owner","trust-1"),_clock);
    internal IndependentDaprAuthorityBinding Binding(string name)=>new(name,"file:/fixture-"+name,_key.ExportSubjectPublicKeyInfoPem(),"isolated-enrollment","isolated-owner","trust-1");
    internal ReceivingAuthorityFixture(string role,IPrivateRecordingEventStore? recordingSource=null,IReadOnlyList<IIndependentOwnerOperationHandler>? handlers=null,IIndependentArchiveStorage? storage=null,IIndependentContentArchive? originalArchive=null,TimeProvider? clock=null)
    {
        _clock=clock??TimeProvider.System;
        if(storage is null) { storage=Substitute.For<IIndependentArchiveStorage>();storage.AvailableBytes(Arg.Any<string>()).Returns(long.MaxValue); }
        File.WriteAllText(_channel,"fixture-channel");Directory.CreateDirectory(_archive);File.WriteAllText(Path.Combine(_archive,"enrollment.json"),"fixture-enrollment");
        State = new("isolated-enrollment","isolated-owner","trust-1",0,DateTimeOffset.UtcNow.AddHours(1),
            [new(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("fixture-workload"))),"recording-owner","private-worker",role,
                ["qualification-begin-original","qualification-retain-result","qualification-lookup-original","recording-dispatch-admit","recording-command-authorize","fr34-command-authorize","recording-replay-recovery-command-authorize","recording-replay-recovery-dispatch-admit","recording-source:trusted-envelope-replay","recording-source:security-event-log","recording-source:fr34-canary"],DateTimeOffset.UtcNow.AddHours(1),false,Convert.ToBase64String(ProofKey.ExportSubjectPublicKeyInfo()))],
            null,0,new string('A',64),[],[],[],[],[],[]);
        _dapr.GetStateAndETagAsync<IndependentAuthorityState>("fixture-store","head",Arg.Any<ConsistencyMode?>(),Arg.Any<IReadOnlyDictionary<string,string>>(),Arg.Any<CancellationToken>())
            .Returns(async _=> { var read=BeforeRead;BeforeRead=null;if(read is not null) await read();return (State,_revision.ToString()); });
        _dapr.TrySaveStateAsync("fixture-store","head",Arg.Any<IndependentAuthorityState>(),Arg.Any<string>(),Arg.Any<StateOptions>(),Arg.Any<IReadOnlyDictionary<string,string>>(),Arg.Any<CancellationToken>())
            .Returns(call=> { BeforeConditionalSave?.Invoke();if(call.ArgAt<string>(3)!=_revision.ToString()) return false; State=call.ArgAt<IndependentAuthorityState>(2); _revision++; return true; });
        _receiver = new(_dapr,new("isolated-enrollment","isolated-owner","trust-1","fixture-store","head","unused",_key.ExportSubjectPublicKeyInfoPem(),"file:"+_channel,OriginalArchiveDirectory:_archive,ArchiveEnrollmentSha256:Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(_archive,"enrollment.json")))),CompletionReserveBytes:1024*1024),_key,_clock,recordingSource,handlers,storage,originalArchive);
    }
    public Task<string?> InvokeAsync(string appId,string method,string jsonBody,CancellationToken token)=>throw new InvalidOperationException("Uncredentialed fixture call.");
    public async Task<string?> InvokeCredentialBoundAsync(string appId,string credentialReference,string method,string jsonBody,CancellationToken token)
        =>await InvokeWithBearer(method,jsonBody,"fixture-workload",token);
    private async Task<string?> InvokeWithBearer(string method,string jsonBody,string bearer,CancellationToken token)
    {
        Methods.Add(method[(method.LastIndexOf('/')+1)..]);
        var context=new DefaultHttpContext { RequestServices=new ServiceCollection().AddLogging().BuildServiceProvider() };
        context.Request.Headers["dapr-api-token"]="fixture-channel"; context.Request.Headers.Authorization="Bearer "+bearer;
        context.Request.Body=new MemoryStream(Encoding.UTF8.GetBytes(jsonBody)); context.Response.Body=new MemoryStream();
        var response=await _receiver.HandleAsync(context,method[(method.LastIndexOf('/')+1)..],token);
        if(FailResponseTransport) context.Response.Body=new FailedTransport();
        await response.ExecuteAsync(context);
        if(context.Response.StatusCode==200 && DropNextResponseForMethod is { } lost && method.EndsWith(lost,StringComparison.Ordinal)) { DropNextResponseForMethod=null;return null; }
        if(context.Response.StatusCode==200 && LoseRetentionResponse && method.EndsWith("qualification-retain-result",StringComparison.Ordinal)) { LoseRetentionResponse=false;return null; }
        if(context.Response.StatusCode!=200) Methods.Add("status:"+context.Response.StatusCode);
        return context.Response.StatusCode==200 ? Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray()) : null;
    }
    internal void BreakSigner()=>_key.Dispose();
    internal void RevokeWorkload(string identity) { State=State with { Revision=checked(State.Revision+1),Callers=State.Callers.Select(value=>value.Identity==identity?value with { Revoked=true }:value).ToArray() };_revision++; }
    public void Dispose() { ProofKey.Dispose();_key.Dispose(); _dapr.Dispose(); File.Delete(_channel);Directory.Delete(_archive,true); }
    private sealed class FailedTransport:MemoryStream
    {
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,CancellationToken token=default)=>ValueTask.FromException(new IOException("isolated transport completion failure"));
    }
    private sealed class Caller(ReceivingAuthorityFixture fixture,string bearer):ISecretsCredentialBoundDaprInvoker
    {
        public Task<string?> InvokeAsync(string app,string method,string json,CancellationToken token)=>Task.FromResult<string?>(null);
        public Task<string?> InvokeCredentialBoundAsync(string app,string credential,string method,string json,CancellationToken token)=>fixture.InvokeWithBearer(method,json,bearer,token);
    }
}
