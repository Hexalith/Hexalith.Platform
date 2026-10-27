using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Platform.Secrets.Runtime;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

public sealed class QualificationGatewayReadProofReceivingTests
{
    [System.Runtime.InteropServices.DllImport("libc",EntryPoint="mkfifo",SetLastError=true)] private static extern int Mkfifo(string path,int mode);
    [Fact]
    public async Task RealProofAdapterAndGatewayBindAuthenticatedCallerExactStreamMountedInputsAndCurrentReadAuthority()
    {
        using var rsa=RSA.Create(2048);string directory=Path.Combine(Path.GetTempPath(),"stream-proof-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try
        {
            string key=Path.Combine(directory,"key"),assertion=Path.Combine(directory,"assertion");
            string privateKey=Convert.ToBase64String(rsa.ExportPkcs8PrivateKey());await File.WriteAllTextAsync(key,privateKey,TestContext.Current.CancellationToken);await File.WriteAllTextAsync(assertion,"independently-issued-fixture-assertion",TestContext.Current.CancellationToken);
            var scope=new AggregateIdentity("tenant","native-domain","exact-aggregate");var request=new StreamReadRequest(scope.TenantId,scope.Domain,scope.AggregateId,ToSequence:1) { RequireNativeProof=true };
            string assertionId=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { operation=EventStoreGatewayWorkloadOperations.StreamRead,tenant=scope.TenantId,domain=scope.Domain })));
            var authority=Substitute.For<ICustodyOwnerStateAuthority>();bool current=true;
            authority.AuthorizeOperationAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(_=>current);
            var credentials=new QualificationMountedReadCredentials("fixture-authenticated-caller",key,new Dictionary<string,string>{{assertionId,assertion}});
            var receiver=new Receiver(scope,Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo()));using var http=new HttpClient(receiver) { BaseAddress=new("https://fixture.invalid/") };
            EventStoreGatewayClient Client(QualificationMountedReadCredentials value)
            { var proofs=new QualificationGatewayReadProofs(value,authority,[scope],TimeProvider.System);return new(http,Options.Create(new EventStoreGatewayClientOptions { BaseAddress=http.BaseAddress,UseWorkloadTransport=true,MaxStreamReadResponseBytes=4096 }),proofs,proofs); }
            var client=Client(credentials);var page=await client.ReadWorkloadStreamAsync(request,TestContext.Current.CancellationToken);page.Tenant.ShouldBe(scope.TenantId);page.Events.Count.ShouldBe(1);page.Metadata.NativeProof.ShouldNotBeNull();
            (await new QualificationNativeSourceReader(new QualificationWorkloadReadGateway(client),TimeProvider.System).ReadAsync([scope],TestContext.Current.CancellationToken)).ShouldNotBeNull();int accepted=receiver.Accepted;accepted.ShouldBeGreaterThan(1);
            foreach(string mounted in new[]{key,assertion})
            {
                string saved=await File.ReadAllTextAsync(mounted,TestContext.Current.CancellationToken);File.Delete(mounted);Mkfifo(mounted,0x180).ShouldBe(0);
                await Should.ThrowAsync<InvalidOperationException>(()=>client.ReadWorkloadStreamAsync(request,TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(1),TestContext.Current.CancellationToken));
                File.Delete(mounted);await File.WriteAllTextAsync(mounted,saved,TestContext.Current.CancellationToken);
            }
            foreach(var wrong in new[]{request with { Tenant="wrong" },request with { Domain="wrong" },request with { AggregateId="wrong" }})
                await Should.ThrowAsync<InvalidOperationException>(()=>client.ReadWorkloadStreamAsync(wrong,TestContext.Current.CancellationToken));
            await Should.ThrowAsync<EventStoreGatewayException>(()=>Client(credentials with { Caller="wrong-caller" }).ReadWorkloadStreamAsync(request,TestContext.Current.CancellationToken));
            using(var changed=RSA.Create(2048)) await File.WriteAllTextAsync(key,Convert.ToBase64String(changed.ExportPkcs8PrivateKey()),TestContext.Current.CancellationToken);
            await Should.ThrowAsync<EventStoreGatewayException>(()=>client.ReadWorkloadStreamAsync(request,TestContext.Current.CancellationToken));
            File.Delete(key);await Should.ThrowAsync<InvalidOperationException>(()=>client.ReadWorkloadStreamAsync(request,TestContext.Current.CancellationToken));
            await File.WriteAllTextAsync(key,privateKey,TestContext.Current.CancellationToken);await File.WriteAllTextAsync(assertion,"substituted-assertion",TestContext.Current.CancellationToken);
            await Should.ThrowAsync<EventStoreGatewayException>(()=>client.ReadWorkloadStreamAsync(request,TestContext.Current.CancellationToken));
            File.Delete(assertion);await Should.ThrowAsync<InvalidOperationException>(()=>client.ReadWorkloadStreamAsync(request,TestContext.Current.CancellationToken));
            await File.WriteAllTextAsync(assertion,"independently-issued-fixture-assertion",TestContext.Current.CancellationToken);current=false;
            await Should.ThrowAsync<InvalidOperationException>(()=>client.ReadWorkloadStreamAsync(request,TestContext.Current.CancellationToken));
            receiver.Accepted.ShouldBe(accepted);
        }
        finally { Directory.Delete(directory,true); }
    }
    private sealed class Receiver(AggregateIdentity enrolled,string publicKey):HttpMessageHandler
    {
        internal int Accepted;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage http,CancellationToken token)
        {
            var request=await http.Content!.ReadFromJsonAsync<StreamReadRequest>(token);
            bool allowed=http.RequestUri!.AbsolutePath=="/api/v1/streams/read/workload" && http.Headers.TryGetValues("X-Hexalith-Workload-Assertion",out var assertions) && assertions.Single()=="independently-issued-fixture-assertion"
                && request is not null && request.Tenant==enrolled.TenantId && request.Domain==enrolled.Domain && request.AggregateId==enrolled.AggregateId
                && http.Headers.TryGetValues(BoundStreamReadProof.HeaderName,out var proofs) && BoundStreamReadProof.Verify(proofs.Single(),request,"fixture-authenticated-caller",publicKey,DateTimeOffset.UtcNow);
            if(!allowed) return new(HttpStatusCode.Forbidden) { Content=JsonContent.Create(new { code="ExactCurrentWorkloadStreamProofRequired" }) };
            const string message="01ARZ3NDEKTSV4RRFFQ69G5FAV";string digest=new('a',64);
            var item=new StreamReadEvent(1,"FixtureNativeEvent","ciphertext-only-fixture"u8.ToArray(),"json",1,message,null,null,DateTimeOffset.UtcNow,"fixture-writer",null) { NativeEventDigest=digest };
            var native=NativeSourceProofCodec.ExtendDigest(NativeSourceProofCodec.CreateSeed(enrolled,message),enrolled,1,message,digest);
            var events=request!.FromSequence==0 && request.ToSequence!=0?new[]{item}:Array.Empty<StreamReadEvent>();
            Accepted++;return new(HttpStatusCode.OK) { Content=JsonContent.Create(new StreamReadPage(request.Tenant,request.Domain,request.AggregateId,events,new(request.FromSequence,request.ToSequence,events.Length==0?null:1,1,events.Length,false,null) { NativeProof=native })) };
        }
    }
}
