using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Shouldly;
namespace Hexalith.Platform.Secrets.Runtime.Tests;
// Controlled loopback signed authorities exercise actual owning-process registrations; no installed FR-34 property is claimed.
public sealed class AgentsOwningProcessRegistrationTests
{
    [Fact]
    public async Task ActualCompositionRunsStartupAndEveryRegisteredReadinessEvaluationThroughFreshAuthoritiesAndFailureRefuses()
    {
        using var fixture=new Authorities();using var dapr=Substitute.For<DaprClient>();
        dapr.CreateInvokeMethodRequest(Arg.Any<HttpMethod>(),Arg.Any<string>(),Arg.Any<string>()).Returns(call=>new HttpRequestMessage(call.ArgAt<HttpMethod>(0),fixture.Url+call.ArgAt<string>(1)+"/"+call.ArgAt<string>(2)));
        dapr.CreateInvokableHttpClient(Arg.Any<string>()).Returns(_=>new HttpClient());
        var services=new ServiceCollection().AddLogging();var config=new SecretsRuntimeConfiguration { CompromiseSubscriptionReference="compromise",CompromiseSubscriberCredentialReference="file:/fixture",CompromiseGuardMirrorReference="guard",CurrentRevocationReference="revocations",CallTimeout=TimeSpan.FromSeconds(5),Issuer="issuer",Audience="audience" };
        services.AddInstalledAgentsOwningProcess(dapr,new(fixture.Binding("spool"),fixture.Binding("recorder"),fixture.Binding("replay"),config,new("issuer","audience","tenant"),fixture.Binding("engine"),fixture.Binding("observer"),fixture.Target,TimeSpan.FromSeconds(5),1),Substitute.For<IGovernanceScopeGuard>());
        using var provider=services.BuildServiceProvider();var startup=provider.GetServices<IHostedService>().OfType<Fr34ProtectionStartupGate>().Single();
        try { await startup.StartAsync(TestContext.Current.CancellationToken); }catch(Exception error) { throw new InvalidOperationException("Fixture methods: "+string.Join(",",fixture.Methods),error); }fixture.Canaries.Count.ShouldBe(1);
        var health=provider.GetRequiredService<HealthCheckService>();
        for(int count=2;count<=3;count++) { var report=await health.CheckHealthAsync(registration=>registration.Name=="agents-protection-fr34",TestContext.Current.CancellationToken);report.Entries["agents-protection-fr34"].Status.ShouldBe(HealthStatus.Healthy);fixture.Canaries.Count.ShouldBe(count); }
        fixture.Revoked=true;var denied=await health.CheckHealthAsync(registration=>registration.Name=="agents-protection-fr34",TestContext.Current.CancellationToken);denied.Entries["agents-protection-fr34"].Status.ShouldBe(HealthStatus.Unhealthy);
        await Should.ThrowAsync<InvalidOperationException>(()=>startup.StartAsync(TestContext.Current.CancellationToken));fixture.Canaries.Count.ShouldBe(3);
        provider.GetService<ITrustedEnvelopeReplayRegistrar>().ShouldBeNull();provider.GetService<ReplicatedSecurityObservationSpool>().ShouldBeNull();
    }
    private sealed class Authorities:IDisposable
    {
        private readonly HttpListener _listener=new();private readonly CancellationTokenSource _stop=new();private readonly Task _server;private readonly ECDsa _engine=ECDsa.Create(ECCurve.NamedCurves.nistP256),_observer=ECDsa.Create(ECCurve.NamedCurves.nistP256);private readonly string _credential=Path.GetTempFileName();
        internal readonly Fr34ProtectionTarget Target=new("real-fixture-engine",new string('a',40),"custody","tenant");internal readonly Dictionary<string,byte[]> Canaries=[];internal readonly List<string> Methods=[];internal bool Revoked;internal string Url;private readonly DateTimeOffset _observed=DateTimeOffset.UtcNow.AddSeconds(-1),_valid=DateTimeOffset.UtcNow.AddMinutes(2);
        internal Authorities() { using var socket=new TcpListener(IPAddress.Loopback,0);socket.Start();int port=((IPEndPoint)socket.LocalEndpoint).Port;socket.Stop();Url="http://127.0.0.1:"+port+"/";_listener.Prefixes.Add(Url);_listener.Start();File.WriteAllText(_credential,"fixture-workload");_server=Serve(); }
        internal IndependentDaprAuthorityBinding Binding(string app)=>new(app,"file:"+_credential,(app=="engine"?_engine:_observer).ExportSubjectPublicKeyInfoPem(),"fixture-enrollment","fixture-scope","trust-1");
        private async Task Serve()
        {
            while(!_stop.IsCancellationRequested)
            {
                HttpListenerContext context;try { context=await _listener.GetContextAsync().WaitAsync(_stop.Token); }catch(Exception) { return; }
                try
                {
                    if(Revoked) { context.Response.StatusCode=503;context.Response.Close();continue; }
                    using var stream=new MemoryStream();await context.Request.InputStream.CopyToAsync(stream);byte[] request=stream.ToArray();using var document=JsonDocument.Parse(request);var original=document.RootElement;
                    string method=original.GetProperty("Method").GetString()!;Methods.Add(method);var payload=original.GetProperty("Payload");object? result;
                    Fr34CanaryReference Reference()=>payload.GetProperty("reference").Deserialize<Fr34CanaryReference>(CustodyBindingContract.Json)!;
                    switch(method)
                    {
                        case "fr34-observe-current":result=new Fr34CanaryAuthorization(Target,"revision",_observed,_valid);break;
                        case "fr34-seal-persist":string id=payload.GetProperty("canaryId").GetString()!;Canaries[id]=payload.GetProperty("plaintext").GetBytesFromBase64();result=new Fr34CanaryReference(Target,id,"record-"+id,"key-"+id);break;
                        case "fr34-read-persisted":var reference=Reference();result=new Fr34PersistedCanary(reference,"fixture-ciphertext"u8.ToArray(),new(PayloadProtectionState.Protected,1,"hexalith-pdenc-v2",reference.KeyReference,null,null));break;
                        case "fr34-unseal":result=Canaries[Reference().CanaryId].ToArray();break;
                        case "fr34-replay":result=PayloadUnprotectionOutcome.Unreadable(UnreadableProtectedDataReason.KeyInvalidatedOrDeleted,new(PayloadProtectionState.Protected,1,"hexalith-pdenc-v2",Reference().KeyReference,null,null));break;
                        default:result=new { Allowed=true };break;
                    }
                    long now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();var response=new Response("fixture-enrollment","fixture-scope","trust-1",method,original.GetProperty("Nonce").GetString()!,Convert.ToHexString(SHA256.HashData(request)),now,now+30,JsonSerializer.SerializeToElement(result,CustodyBindingContract.Json),[]);
                    var key=context.Request.Url!.AbsolutePath.StartsWith("/engine/",StringComparison.Ordinal)?_engine:_observer;response=response with { Signature=key.SignData(JsonSerializer.SerializeToUtf8Bytes(response,CustodyBindingContract.Json),HashAlgorithmName.SHA256,DSASignatureFormat.Rfc3279DerSequence) };
                    byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(response,CustodyBindingContract.Json);context.Response.ContentLength64=bytes.Length;await context.Response.OutputStream.WriteAsync(bytes);context.Response.Close();
                }
                catch(Exception error) { Methods.Add("error:"+error.Message);context.Response.StatusCode=503;context.Response.Close(); }
            }
        }
        public void Dispose() { _stop.Cancel();_listener.Close();_server.GetAwaiter().GetResult();_stop.Dispose();_engine.Dispose();_observer.Dispose();File.Delete(_credential);foreach(var bytes in Canaries.Values) CryptographicOperations.ZeroMemory(bytes); }
        private sealed record Response(string EnrollmentId,string Scope,string TrustRevision,string Method,string Nonce,string RequestSha256,long ObservedAt,long ValidUntil,JsonElement Payload,byte[] Signature);
    }
}
