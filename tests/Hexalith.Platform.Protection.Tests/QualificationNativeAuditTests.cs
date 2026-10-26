using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Platform.Secrets.Runtime;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

public sealed class QualificationNativeAuditTests
{
    [Fact]
    public async Task ActualNativeWorkloadStatusStreamAndProjectionReadsRejectForeignStaleOrMissingProofAndDoNotInventFullAudit()
    {
        using var authority=new ReceivingAuthorityFixture("executor");var gateway=Substitute.For<IEventStoreGatewayClient>();
        var request=new JsonObject { ["caseId"]="S1.reject.missing-config",["requestId"]=new string('a',64),["enrolledScope"]=new JsonObject { ["tenantId"]="tenant" } };
        string requestId=request["requestId"]!.GetValue<string>();
        authority.State=authority.State with { Revision=3,Originals=new() { ["executor:"+requestId+":case-result"]=new(JsonSerializer.SerializeToElement(request),JsonSerializer.SerializeToElement(new { outcome="Denied" }),RetainedRevision:1,ResultRetainedRevision:2) } };
        var identity=new AggregateIdentity("tenant","fixture-domain","canary");string incarnation="01ARZ3NDEKTSV4RRFFQ69G5FAV";
        var item=new StreamReadEvent(1,"ActualFixtureEvent",Encoding.UTF8.GetBytes("{\"value\":\"actual-native-canary\"}"),"json",1,incarnation,null,null,DateTimeOffset.UtcNow,"private-worker",null) { NativeEventDigest=new string('b',64) };
        bool foreign=false,missingNative=false,stale=false;
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(),Arg.Any<CancellationToken>()).Returns(call=>
        {
            var read=call.ArgAt<StreamReadRequest>(0);read.RequireNativeProof.ShouldBeTrue();
            var proof=NativeSourceProofCodec.ExtendDigest(NativeSourceProofCodec.CreateSeed(identity,incarnation),identity,1,item.MessageId,item.NativeEventDigest!);
            var events=read.FromSequence==0 && read.ToSequence!=0?new[]{item}:Array.Empty<StreamReadEvent>();
            return new StreamReadPage(identity.TenantId,identity.Domain,identity.AggregateId,events,new(read.FromSequence,read.ToSequence,events.Length==0?null:1,1,events.Length,false,null) { NativeProof=missingNative?null:proof });
        });
        gateway.GetWorkloadCommandStatusAsync("tenant","message",Arg.Any<CancellationToken>()).Returns(_=>new CommandStatusQueryResponse("correlation","Completed",(int)CommandStatus.Completed,MessageId:"message") { TenantId=foreign?"foreign":"tenant",Domain=identity.Domain,AggregateId=identity.AggregateId,CommittedEventSequence=1,EventCount=1 });
        var query=new SubmitQueryRequest("tenant",identity.Domain,identity.AggregateId,"ExactCanaryQuery",Payload:JsonSerializer.SerializeToElement(new { original="exact" }));
        using var rsa=RSA.Create(2048);string keyFile=Path.GetTempFileName(),tokenFile=Path.GetTempFileName();
        File.WriteAllText(keyFile,Convert.ToBase64String(rsa.ExportPkcs8PrivateKey()));File.WriteAllText(tokenFile,"fixture-issued-assertion");
        using var files=new Cleanup(keyFile,tokenFile);
        bool current=true;var readAuthority=NSubstitute.Substitute.For<ICustodyOwnerStateAuthority>();
        readAuthority.AuthorizeOperationAsync(Arg.Any<string>(),"ReadQualificationOwnerQuery",Arg.Any<CancellationToken>()).Returns(call=>current && call.ArgAt<string>(0)==QualificationOwnerQueryClient.ScopeId(new(query,"private-principal")));
        using var handler=new QueryTransport(async message=>
        {
            message.RequestUri!.AbsolutePath.ShouldBe("/api/v1/queries/owner-authority/workload");
            var supplied=JsonSerializer.Deserialize<BoundOwnerQueryRequest>(await message.Content!.ReadAsStringAsync(),new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            bool allowed=message.Headers.TryGetValues("X-Hexalith-Workload-Assertion",out var assertion) && assertion.Single()=="fixture-issued-assertion"
                && BoundOwnerQueryProof.Verify(supplied,message.Headers.GetValues(BoundOwnerQueryProof.HeaderName).Single(),"fixture-reader",Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo()),DateTimeOffset.UtcNow);
            return new(allowed?System.Net.HttpStatusCode.OK:System.Net.HttpStatusCode.Forbidden) { Content=new StringContent(JsonSerializer.Serialize(new SubmitQueryResponse("query-correlation",JsonSerializer.SerializeToElement(new { receipt="actual-projection-receipt" }),Metadata:new(ProjectionVersion:"projection-v1",ServedAt:DateTimeOffset.UtcNow,IsStale:stale,IsDegraded:false) { Provenance=QueryResponseProvenance.ProjectionBacked,Lifecycle=stale?ProjectionLifecycleState.Stale:ProjectionLifecycleState.Current }),new JsonSerializerOptions(JsonSerializerDefaults.Web))) };
        });
        using var http=new HttpClient(handler) { BaseAddress=new("https://fixture.invalid/") };
        string assertionId=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { operation="eventstore:owner-authority-query",tenant="tenant",domain=identity.Domain })));
        var queryClient=new QualificationOwnerQueryClient(http,new("fixture-reader",keyFile,new Dictionary<string,string>{{assertionId,tokenFile}},"private-principal"),readAuthority,[query],TimeProvider.System);
        var scope=new QualificationEventStoreAuditScope("tenant",QualificationProtocolSchema.Digest(request["enrolledScope"]!),[":case-result"],["message"],[identity],[query]);
        var reader=new QualificationEventStoreAuditReader(authority.Authority,gateway,TimeProvider.System,new Dictionary<string,QualificationEventStoreAuditScope>{{"S1.reject.missing-config",scope}},queryClient);
        var actual=await reader.ReadAsync(request,TestContext.Current.CancellationToken);actual.ShouldNotBeNull();actual.NativeStreams.Single().Events.Single().PayloadSha256.ShouldBe(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(item.Payload)));
        actual.Projections.Single().Metadata.Provenance.ShouldBe(QueryResponseProvenance.ProjectionBacked);actual.MissingInputs.ShouldNotBeEmpty();
        JsonSerializer.Serialize(actual).ShouldNotContain("actual-native-canary");JsonSerializer.Serialize(actual).ShouldNotContain("actual-projection-receipt");
        foreign=true;(await reader.ReadAsync(request,TestContext.Current.CancellationToken)).ShouldBeNull();foreign=false;
        missingNative=true;(await reader.ReadAsync(request,TestContext.Current.CancellationToken)).ShouldBeNull();missingNative=false;
        stale=true;(await reader.ReadAsync(request,TestContext.Current.CancellationToken)).ShouldBeNull();
        stale=false;current=false;(await reader.ReadAsync(request,TestContext.Current.CancellationToken)).ShouldBeNull();current=true;
        File.WriteAllText(tokenFile,"substituted-assertion");(await reader.ReadAsync(request,TestContext.Current.CancellationToken)).ShouldBeNull();File.WriteAllText(tokenFile,"fixture-issued-assertion");
        using var changedKey=RSA.Create(2048);File.WriteAllText(keyFile,Convert.ToBase64String(changedKey.ExportPkcs8PrivateKey()));(await reader.ReadAsync(request,TestContext.Current.CancellationToken)).ShouldBeNull();
        (await queryClient.ReadAsync(query with { Payload=JsonSerializer.SerializeToElement(new { original="changed" }) },TestContext.Current.CancellationToken)).ShouldBeNull();
        File.Delete(tokenFile);(await reader.ReadAsync(request,TestContext.Current.CancellationToken)).ShouldBeNull();
        gateway.ReceivedCalls().ShouldAllBe(call=>new[]{nameof(IEventStoreGatewayClient.ReadWorkloadStreamAsync),nameof(IEventStoreGatewayClient.GetWorkloadCommandStatusAsync)}.Contains(call.GetMethodInfo().Name));
    }
    private sealed class QueryTransport(Func<HttpRequestMessage,Task<HttpResponseMessage>> receive):HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage message,CancellationToken token)=>receive(message); }
    private sealed class Cleanup(params string[] files):IDisposable { public void Dispose() { foreach(string file in files) File.Delete(file); } }
}
