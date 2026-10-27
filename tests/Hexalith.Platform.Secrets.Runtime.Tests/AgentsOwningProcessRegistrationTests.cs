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
        var readyClock=new ReadinessClock();Func<Task>? keyRead=null;var services=new ServiceCollection().AddLogging();var profiles=Substitute.For<IPlatformSigningProfileProvider>();var profile=new PlatformSigningProfile("v1","issuer","audience",DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddDays(1),TimeSpan.FromSeconds(300),TimeSpan.FromSeconds(30),TimeSpan.FromSeconds(600),TimeSpan.FromDays(1),TimeSpan.FromDays(7));profiles.GetCurrent().Returns(profile);
        var keys=Substitute.For<IPlatformHmacKeyProvider>();bool keyAvailable=true;keys.ResolveAsync(Arg.Any<PlatformHmacScope>(),Arg.Any<string?>(),Arg.Any<CancellationToken>()).Returns(call=>new ValueTask<PlatformHmacKeyResolution>(ReadKey(call.Arg<PlatformHmacScope>())));async Task<PlatformHmacKeyResolution> ReadKey(PlatformHmacScope scope) { if(keyRead is not null) await keyRead();return keyAvailable?new(CustodyStatus.Succeeded,new(scope,new("v1",PlatformHmacKeyState.Active,DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddDays(1),null),new byte[32])):new(CustodyStatus.Unavailable); }services.AddSingleton(profiles);services.AddSingleton(keys);var config=new SecretsRuntimeConfiguration { CompromiseSubscriptionReference="compromise",CompromiseSubscriberCredentialReference="file:/fixture",CompromiseGuardMirrorReference="guard",CurrentRevocationReference="revocations",CallTimeout=TimeSpan.FromSeconds(5),Issuer="issuer",Audience="audience" };
        services.AddInstalledAgentsOwningProcess(dapr,new(fixture.Binding("spool"),fixture.Binding("recorder"),fixture.Binding("replay"),config,new("issuer","audience","tenant"),fixture.Binding("engine"),fixture.Binding("observer"),fixture.Target,TimeSpan.FromSeconds(5),1),Substitute.For<IGovernanceScopeGuard>(),readyClock);
        bool storageOutage=false;
        dapr.GetStateAndETagAsync<SecuritySpoolSnapshot>("fixture-spool","system/security-observations/fixture-epoch",Arg.Any<ConsistencyMode?>(),Arg.Any<IReadOnlyDictionary<string,string>>(),Arg.Any<CancellationToken>())
            .Returns(_=>storageOutage?Task.FromException<(SecuritySpoolSnapshot,string)>(new IOException("isolated spool storage outage")):Task.FromResult((new SecuritySpoolSnapshot("fixture-epoch",0,[]),"fixture-etag")));
        using var provider=services.BuildServiceProvider();var startup=provider.GetServices<IHostedService>().OfType<Fr34ProtectionStartupGate>().Single();
        try { await startup.StartAsync(TestContext.Current.CancellationToken); }catch(Exception error) { throw new InvalidOperationException("Fixture methods: "+string.Join(",",fixture.Methods),error); }fixture.Canaries.Count.ShouldBe(1);
        provider.GetRequiredService<TrustedEnvelopeReplayVerifier>().ShouldNotBeNull();provider.GetRequiredService<PlatformSecurityDenialRecorder>().ShouldNotBeNull();
        var ingress=provider.GetServices<IHostedService>().OfType<AgentsIngressReadiness>().Single();await ingress.StartAsync(TestContext.Current.CancellationToken);
        var health=provider.GetRequiredService<HealthCheckService>();
        async Task<HealthStatus> Audit()=> (await health.CheckHealthAsync(registration=>registration.Name=="agents-security-audit",TestContext.Current.CancellationToken)).Entries["agents-security-audit"].Status;
        async Task<HealthStatus> Ingress()=> (await health.CheckHealthAsync(registration=>registration.Name=="agents-private-ingress",TestContext.Current.CancellationToken)).Entries["agents-private-ingress"].Status;
        int canaries=fixture.Canaries.Count;
        (await Ingress()).ShouldBe(HealthStatus.Healthy);keyAvailable=false;(await Ingress()).ShouldBe(HealthStatus.Unhealthy);keyAvailable=true;(await Ingress()).ShouldBe(HealthStatus.Healthy);
        profiles.GetCurrent().Returns(profile with { ValidUntil=DateTimeOffset.UtcNow.AddSeconds(-1) });(await Ingress()).ShouldBe(HealthStatus.Unhealthy);profiles.GetCurrent().Returns(profile);
        fixture.ReplayUnavailable=true;(await Ingress()).ShouldBe(HealthStatus.Unhealthy);(await Audit()).ShouldBe(HealthStatus.Healthy);fixture.ReplayUnavailable=false;(await Ingress()).ShouldBe(HealthStatus.Healthy);
        fixture.OnReplayRead=()=>keyAvailable=false;(await Ingress()).ShouldBe(HealthStatus.Unhealthy);fixture.OnReplayRead=null;keyAvailable=true;
        fixture.ProjectCredential("spool");fixture.ProjectCredential("recorder");(await Ingress()).ShouldBe(HealthStatus.Healthy);fixture.RotateProjectedCredential("recorder");(await Ingress()).ShouldBe(HealthStatus.Healthy);
        fixture.ReplayTtlSeconds=1;keyRead=async ()=> { await Task.Yield();readyClock.Offset=TimeSpan.FromSeconds(3); };(await Ingress()).ShouldBe(HealthStatus.Unhealthy);keyRead=null;readyClock.Offset=TimeSpan.Zero;fixture.ReplayTtlSeconds=30;(await Ingress()).ShouldBe(HealthStatus.Healthy);
        fixture.CopyCredential("spool","recorder");(await Ingress()).ShouldBe(HealthStatus.Unhealthy);fixture.RotateCredential("recorder");(await Ingress()).ShouldBe(HealthStatus.Healthy);
        fixture.Canaries.Count.ShouldBe(canaries);fixture.Methods.ShouldNotContain("replay-register-exact");fixture.Methods.ShouldNotContain("security-observation-append");
        (await Audit()).ShouldBe(HealthStatus.Healthy);storageOutage=true;(await Audit()).ShouldBe(HealthStatus.Unhealthy);storageOutage=false;(await Audit()).ShouldBe(HealthStatus.Healthy);
        for(int count=2;count<=3;count++) { var report=await health.CheckHealthAsync(registration=>registration.Name=="agents-protection-fr34",TestContext.Current.CancellationToken);report.Entries["agents-protection-fr34"].Status.ShouldBe(HealthStatus.Healthy);fixture.Canaries.Count.ShouldBe(count); }
        fixture.Revoked=true;var denied=await health.CheckHealthAsync(registration=>registration.Name=="agents-protection-fr34",TestContext.Current.CancellationToken);denied.Entries["agents-protection-fr34"].Status.ShouldBe(HealthStatus.Unhealthy);
        (await Audit()).ShouldBe(HealthStatus.Unhealthy);
        await Should.ThrowAsync<InvalidOperationException>(()=>startup.StartAsync(TestContext.Current.CancellationToken));fixture.Canaries.Count.ShouldBe(3);
        provider.GetService<ITrustedEnvelopeReplayRegistrar>().ShouldBeNull();provider.GetService<ReplicatedSecurityObservationSpool>().ShouldBeNull();
    }
    [Theory]
    [InlineData("spool-key")][InlineData("engine-key")][InlineData("spool-credential")][InlineData("engine-credential")][InlineData("copied-bearer")][InlineData("copied-bearer-newline")][InlineData("spaced-path-bearer")][InlineData("hardlink-bearer")][InlineData("distinct")]
    public void ActualCompositionRequiresDistinctImportedKeysAndNormalizedPrivateBindings(string mutation)
    {
        using var fixture=new Authorities();using var dapr=Substitute.For<DaprClient>();var services=new ServiceCollection().AddLogging();
        var spool=fixture.Binding("spool");var recorder=fixture.Binding("recorder");var engine=fixture.Binding("engine");var observer=fixture.Binding("observer");
        string OtherEncoding(string pem)
        {
            using var key=ECDsa.Create();key.ImportFromPem(pem);
            // A valid, differently wrapped public encoding imports to the same cryptographic identity.
            return "-----BEGIN PUBLIC KEY-----\r\n"+Convert.ToBase64String(key.ExportSubjectPublicKeyInfo())+"\r\n-----END PUBLIC KEY-----\r\n";
        }
        if(mutation=="spool-key") recorder=recorder with { PublicKeyPem=OtherEncoding(spool.PublicKeyPem) };
        if(mutation=="engine-key") observer=observer with { PublicKeyPem=OtherEncoding(engine.PublicKeyPem) };
        if(mutation=="spool-credential") recorder=recorder with { CredentialReference="file:"+Path.Combine(Path.GetDirectoryName(spool.CredentialReference[5..])!,".",Path.GetFileName(spool.CredentialReference[5..])) };
        if(mutation=="engine-credential") observer=observer with { CredentialReference=engine.CredentialReference };
        if(mutation=="copied-bearer") fixture.CopyCredential("spool","recorder");
        if(mutation=="copied-bearer-newline") fixture.CopyCredentialWithNewline("spool","recorder");
        if(mutation=="spaced-path-bearer") recorder=recorder with { CredentialReference=fixture.SpacedAlias("spool","recorder") };
        if(mutation=="hardlink-bearer") fixture.LinkCredential("engine","observer");
        var config=new SecretsRuntimeConfiguration { CompromiseSubscriptionReference="compromise",CompromiseSubscriberCredentialReference="file:/fixture",CompromiseGuardMirrorReference="guard",CurrentRevocationReference="revocations",CallTimeout=TimeSpan.FromSeconds(5),Issuer="issuer",Audience="audience" };
        void Compose()=>services.AddInstalledAgentsOwningProcess(dapr,new(spool,recorder,fixture.Binding("replay"),config,new("issuer","audience","tenant"),engine,observer,fixture.Target,TimeSpan.FromSeconds(5),1),Substitute.For<IGovernanceScopeGuard>());
        if(mutation=="distinct") { Compose();services.Count.ShouldBeGreaterThan(0); }
        else { Should.Throw<ArgumentException>(Compose);services.Any(value=>value.ServiceType==typeof(Fr34ProtectionGate)).ShouldBeFalse(); }
    }
    [Theory]
    [InlineData("missing")][InlineData("key-construction")][InlineData("profile-construction")]
    public async Task ActualPrivateFactoriesAndStartupRefuseUnavailableOrUnconstructibleIngress(string defect)
    {
        using var fixture=new Authorities();using var dapr=Substitute.For<DaprClient>();var services=new ServiceCollection().AddLogging();
        if(defect=="key-construction") services.AddSingleton<IPlatformHmacKeyProvider>(_=>throw new InvalidOperationException("isolated key graph failure"));
        if(defect=="profile-construction") services.AddSingleton<IPlatformSigningProfileProvider>(_=>throw new InvalidOperationException("isolated profile graph failure"));
        var config=new SecretsRuntimeConfiguration { CompromiseSubscriptionReference="compromise",CompromiseSubscriberCredentialReference="file:/fixture",CompromiseGuardMirrorReference="guard",CurrentRevocationReference="revocations",CallTimeout=TimeSpan.FromSeconds(5),Issuer="issuer",Audience="audience" };
        services.AddInstalledAgentsOwningProcess(dapr,new(fixture.Binding("spool"),fixture.Binding("recorder"),fixture.Binding("replay"),config,new("issuer","audience","tenant"),fixture.Binding("engine"),fixture.Binding("observer"),fixture.Target,TimeSpan.FromSeconds(5),1),Substitute.For<IGovernanceScopeGuard>());
        using var provider=services.BuildServiceProvider();
        if(defect!="missing") Should.Throw<InvalidOperationException>(()=>provider.GetRequiredService<AgentsIngressReadiness>()).Message.ShouldContain("InstalledConstructiblePrivateIngress");
        else
        {
            provider.GetRequiredService<TrustedEnvelopeReplayVerifier>().ShouldNotBeNull();provider.GetRequiredService<PlatformSecurityDenialRecorder>().ShouldNotBeNull();
            var gate=provider.GetRequiredService<AgentsIngressReadiness>();(await gate.CheckHealthAsync(new(),TestContext.Current.CancellationToken)).Status.ShouldBe(HealthStatus.Unhealthy);
            (await Should.ThrowAsync<InvalidOperationException>(()=>gate.StartAsync(TestContext.Current.CancellationToken))).Message.ShouldContain("InstalledCurrentPrivateIngress");
        }
        fixture.Methods.ShouldNotContain("replay-register-exact");fixture.Canaries.ShouldBeEmpty();
    }
    private sealed class ReadinessClock:TimeProvider { internal TimeSpan Offset;public override DateTimeOffset GetUtcNow()=>DateTimeOffset.UtcNow+Offset; }
    private sealed class Authorities:IDisposable
    {
        private readonly HttpListener _listener=new();private readonly CancellationTokenSource _stop=new();private readonly Task _server;private readonly ECDsa _engine=ECDsa.Create(ECCurve.NamedCurves.nistP256),_observer=ECDsa.Create(ECCurve.NamedCurves.nistP256),_spool=ECDsa.Create(ECCurve.NamedCurves.nistP256),_recorder=ECDsa.Create(ECCurve.NamedCurves.nistP256);private readonly Dictionary<string,string> _credentials=[];private readonly List<string> _extras=[];
        internal readonly Fr34ProtectionTarget Target=new("real-fixture-engine",new string('a',40),"custody","tenant");internal readonly Dictionary<string,byte[]> Canaries=[];internal readonly List<string> Methods=[];internal bool Revoked,ReplayUnavailable;internal Action? OnReplayRead;internal int ReplayTtlSeconds=30;internal string Url;private readonly DateTimeOffset _observed=DateTimeOffset.UtcNow.AddSeconds(-1),_valid=DateTimeOffset.UtcNow.AddMinutes(2);
        internal Authorities() { using var socket=new TcpListener(IPAddress.Loopback,0);socket.Start();int port=((IPEndPoint)socket.LocalEndpoint).Port;socket.Stop();Url="http://127.0.0.1:"+port+"/";_listener.Prefixes.Add(Url);_listener.Start();foreach(string app in new[]{"engine","observer","spool","recorder","replay"}) { string credential=Path.GetTempFileName();File.WriteAllText(credential,"fixture-workload-"+app);_credentials.Add(app,credential); }_server=Serve(); }
        internal void ProjectCredential(string app) { string source=_credentials[app];Directory.CreateDirectory(source+"-projection");string target=Path.Combine(source+"-projection","version0");File.Move(source,target);File.CreateSymbolicLink(source,target); }
        internal void RotateProjectedCredential(string app) { string source=_credentials[app],target=Path.Combine(source+"-projection","version1");File.WriteAllText(target,"fixture-projected-rotation-"+app+"\r\n");File.CreateSymbolicLink(source+"-next",target);File.Move(source+"-next",source,true); }
        internal string SpacedAlias(string source,string target) { string path=_credentials[target]+" ";File.WriteAllBytes(path,File.ReadAllBytes(_credentials[source]));_extras.Add(path);return "file:"+path; }
        internal void CopyCredentialWithNewline(string source,string target)=>File.WriteAllText(_credentials[target],File.ReadAllText(_credentials[source])+"\r\n");
        internal void CopyCredential(string source,string target)=>File.WriteAllBytes(_credentials[target],File.ReadAllBytes(_credentials[source]));
        internal void RotateCredential(string app)=>File.WriteAllText(_credentials[app],"fixture-rotated-"+Guid.NewGuid().ToString("N"));
        internal void LinkCredential(string source,string target) { File.Delete(_credentials[target]);Link(_credentials[source],_credentials[target]).ShouldBe(0); }
        [System.Runtime.InteropServices.DllImport("libc",EntryPoint="link")] private static extern int Link(string source,string target);
        internal IndependentDaprAuthorityBinding Binding(string app)=>new(app,"file:"+_credentials[app],Key(app).ExportSubjectPublicKeyInfoPem(),"fixture-enrollment","fixture-scope","trust-1");
        private ECDsa Key(string app)=>app switch { "engine"=>_engine,"spool"=>_spool,"recorder"=>_recorder,_=>_observer };
        private async Task Serve()
        {
            while(!_stop.IsCancellationRequested)
            {
                HttpListenerContext context;try { context=await _listener.GetContextAsync().WaitAsync(_stop.Token); }catch(Exception) { return; }
                try
                {
                    if(Revoked || ReplayUnavailable && context.Request.Url!.AbsolutePath.StartsWith("/replay/",StringComparison.Ordinal)) { context.Response.StatusCode=503;context.Response.Close();continue; }
                    using var stream=new MemoryStream();await context.Request.InputStream.CopyToAsync(stream);byte[] request=stream.ToArray();using var document=JsonDocument.Parse(request);var original=document.RootElement;
                    string method=original.GetProperty("Method").GetString()!;Methods.Add(method);var payload=original.GetProperty("Payload");object? result;
                    Fr34CanaryReference Reference()=>payload.GetProperty("reference").Deserialize<Fr34CanaryReference>(CustodyBindingContract.Json)!;
                    switch(method)
                    {
                        case "replay-read-current-ready":OnReplayRead?.Invoke();result=new { Allowed=true };break;
                        case "spool-current":result=new ReplicatedSecuritySpoolTarget("fixture-spool","fixture-epoch","fixture-policy",_valid);break;
                        case "fr34-observe-current":result=new Fr34CanaryAuthorization(Target,"revision",_observed,_valid);break;
                        case "fr34-seal-persist":string id=payload.GetProperty("canaryId").GetString()!;Canaries[id]=payload.GetProperty("plaintext").GetBytesFromBase64();result=new Fr34CanaryReference(Target,id,"record-"+id,"key-"+id);break;
                        case "fr34-read-persisted":var reference=Reference();result=new Fr34PersistedCanary(reference,"fixture-ciphertext"u8.ToArray(),new(PayloadProtectionState.Protected,1,"hexalith-pdenc-v2",reference.KeyReference,null,null));break;
                        case "fr34-unseal":result=Canaries[Reference().CanaryId].ToArray();break;
                        case "fr34-replay":result=PayloadUnprotectionOutcome.Unreadable(UnreadableProtectedDataReason.KeyInvalidatedOrDeleted,new(PayloadProtectionState.Protected,1,"hexalith-pdenc-v2",Reference().KeyReference,null,null));break;
                        default:result=new { Allowed=true };break;
                    }
                    long now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();var response=new Response("fixture-enrollment","fixture-scope","trust-1",method,original.GetProperty("Nonce").GetString()!,Convert.ToHexString(SHA256.HashData(request)),now,now+(method=="replay-read-current-ready"?ReplayTtlSeconds:30),JsonSerializer.SerializeToElement(result,CustodyBindingContract.Json),[]);
                    var key=Key(context.Request.Url!.AbsolutePath.Split('/',StringSplitOptions.RemoveEmptyEntries)[0]);response=response with { Signature=key.SignData(JsonSerializer.SerializeToUtf8Bytes(response,CustodyBindingContract.Json),HashAlgorithmName.SHA256,DSASignatureFormat.Rfc3279DerSequence) };
                    byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(response,CustodyBindingContract.Json);context.Response.ContentLength64=bytes.Length;await context.Response.OutputStream.WriteAsync(bytes);context.Response.Close();
                }
                catch(Exception error) { Methods.Add("error:"+error.Message);context.Response.StatusCode=503;context.Response.Close(); }
            }
        }
        public void Dispose() { _stop.Cancel();_listener.Close();_server.GetAwaiter().GetResult();_stop.Dispose();_engine.Dispose();_observer.Dispose();_spool.Dispose();_recorder.Dispose();foreach(string credential in _credentials.Values) { File.Delete(credential);if(Directory.Exists(credential+"-projection")) Directory.Delete(credential+"-projection",true); }foreach(string extra in _extras) File.Delete(extra);foreach(var bytes in Canaries.Values) CryptographicOperations.ZeroMemory(bytes); }
        private sealed record Response(string EnrollmentId,string Scope,string TrustRevision,string Method,string Nonce,string RequestSha256,long ObservedAt,long ValidUntil,JsonElement Payload,byte[] Signature);
    }
}
