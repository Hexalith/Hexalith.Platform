using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
        var query=new SubmitQueryRequest("tenant",identity.Domain,identity.AggregateId,"ExactCanaryQuery","FixtureProjection");
        gateway.SubmitQueryAsync(query,null,Arg.Any<CancellationToken>()).Returns(_=>new EventStoreQueryResult("query-correlation",JsonSerializer.SerializeToElement(new { receipt="actual-projection-receipt" }),false,"etag") {
            Metadata=new(ProjectionVersion:"projection-v1",ServedAt:DateTimeOffset.UtcNow,IsStale:stale,IsDegraded:false) { Provenance=QueryResponseProvenance.ProjectionBacked,Lifecycle=stale?ProjectionLifecycleState.Stale:ProjectionLifecycleState.Current } });
        var scope=new QualificationEventStoreAuditScope("tenant",QualificationProtocolSchema.Digest(request["enrolledScope"]!),[":case-result"],["message"],[identity],[query]);
        var reader=new QualificationEventStoreAuditReader(authority.Authority,gateway,TimeProvider.System,new Dictionary<string,QualificationEventStoreAuditScope>{{"S1.reject.missing-config",scope}});
        var actual=await reader.ReadAsync(request,TestContext.Current.CancellationToken);actual.ShouldNotBeNull();actual.NativeStreams.Single().Events.Single().PayloadSha256.ShouldBe(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(item.Payload)));
        actual.Projections.Single().Metadata.Provenance.ShouldBe(QueryResponseProvenance.ProjectionBacked);actual.MissingInputs.ShouldNotBeEmpty();
        JsonSerializer.Serialize(actual).ShouldNotContain("actual-native-canary");JsonSerializer.Serialize(actual).ShouldNotContain("actual-projection-receipt");
        foreign=true;(await reader.ReadAsync(request,TestContext.Current.CancellationToken)).ShouldBeNull();foreign=false;
        missingNative=true;(await reader.ReadAsync(request,TestContext.Current.CancellationToken)).ShouldBeNull();missingNative=false;
        stale=true;(await reader.ReadAsync(request,TestContext.Current.CancellationToken)).ShouldBeNull();
        gateway.ReceivedCalls().ShouldAllBe(call=>new[]{nameof(IEventStoreGatewayClient.ReadWorkloadStreamAsync),nameof(IEventStoreGatewayClient.GetWorkloadCommandStatusAsync),nameof(IEventStoreGatewayClient.SubmitQueryAsync)}.Contains(call.GetMethodInfo().Name));
    }
}
