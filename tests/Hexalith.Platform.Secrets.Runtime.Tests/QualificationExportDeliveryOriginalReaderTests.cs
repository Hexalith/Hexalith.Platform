using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Actors;
using Dapr.Actors.Runtime;
using Hexalith.Platform.Custody;
using Hexalith.EventStore.Contracts.Security;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class QualificationExportDeliveryOriginalReaderTests
{
    [Theory]
    [InlineData("agreement")][InlineData("execution")][InlineData("receiving")][InlineData("native")]
    public async Task ShippedReaderRequiresAllThreeIndependentlyRetainedDenialBodiesAndNeverRepeatsDelivery(string changed)
    {
        var token=TestContext.Current.CancellationToken;using var denialAuthority=new IndependentAuthorityReceiverTests.Fixture();using var executionAuthority=new IndependentAuthorityReceiverTests.Fixture();
        denialAuthority.State=denialAuthority.State with { Callers=denialAuthority.State.Callers.Select(value=>value with { Role="private-export-delivery-denial-v1",Methods=["qualification-begin-original","qualification-lookup-original"] }).ToArray() };
        executionAuthority.State=executionAuthority.State with { Callers=executionAuthority.State.Callers.Select(value=>value with { Role="executor",Methods=["qualification-begin-original","qualification-retain-result","qualification-lookup-original"] }).ToArray() };
        var retainedStore=new InstalledQualificationOriginalStore(executionAuthority.Client());bool armed=false;
        var returnedStore=Substitute.For<IQualificationOriginalStore>();
        returnedStore.BeginAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()).Returns(call=>retainedStore.BeginAsync(call.ArgAt<string>(0),call.ArgAt<string>(1),call.Arg<JsonObject>(),call.Arg<CancellationToken>()));
        returnedStore.RetainAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()).Returns(call=>retainedStore.RetainAsync(call.ArgAt<string>(0),call.ArgAt<string>(1),call.Arg<JsonObject>(),call.Arg<CancellationToken>()));
        returnedStore.LookupResultAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(async call=>
        {
            var value=await retainedStore.LookupResultAsync(call.ArgAt<string>(0),call.ArgAt<string>(1),call.Arg<CancellationToken>());
            if(armed && changed=="execution" && value?["receivingDenialOriginal"] is JsonObject denial)
            { value=(JsonObject)value.DeepClone();value["receivingDenialOriginal"]=JsonSerializer.SerializeToNode(denial.Deserialize<ExportKeyDeliveryReceivingDenialOriginal>(CustodyBindingContract.Json)! with { ReceivedAt=denial["ReceivedAt"]!.GetValue<DateTimeOffset>().AddMilliseconds(1) },CustodyBindingContract.Json); }
            return value;
        });
        var identity=new ExportKeyDeliveryIdentity("tenant","delivery","export",1,"principal","key-use","audience",DateTimeOffset.UtcNow.AddHours(1),"v1","target",1);
        var delivered=new ExportKeyDeliveryOutcome(identity,ExportKeyDeliveryState.Delivered,DateTimeOffset.UtcNow);
        var states=Substitute.For<IActorStateManager>();states.TryGetStateAsync<ExportKeyDeliveryOutcome>("export-key-delivery-v1",Arg.Any<CancellationToken>()).Returns(new ConditionalValue<ExportKeyDeliveryOutcome>(true,delivered));
        states.TryGetStateAsync<AnchoredStateTransition>(Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(new ConditionalValue<AnchoredStateTransition>(false,null!));
        var authority=Substitute.For<IExportKeyDeliveryAuthority>();authority.AuthorizeOperationAsync(Arg.Any<ExportKeyDeliveryIdentity>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(true);
        authority.ValidateStateAsync(Arg.Any<ExportKeyDeliveryIdentity>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(call=>call.Arg<string>()==Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(delivered))));
        var nativeProvider=Substitute.For<IExportKeyDirectDeliveryProvider>();var actors=new ActorRegistrationCollection();
        new PrivateExportKeyDeliveryOwner(actors,authority,nativeProvider).RegisterInstalledPrivateExportKeyDeliveryOwner(denialAuthority.PrivateTransport(),denialAuthority.PublicBinding);
        var actor=(ExportKeyDeliveryActor)(await actors.Single().Activator!.CreateAsync(ActorHost.CreateForTest<ExportKeyDeliveryActor>(new ActorTestOptions { ActorId=new(identity.ActorId) }))).Actor;
        typeof(Dapr.Actors.Runtime.Actor).GetProperty("StateManager",BindingFlags.Public|BindingFlags.Instance)!.SetValue(actor,states);
        var router=Substitute.For<IExportKeyDeliveryActor>();router.LookupAsync(identity).Returns(call=>actor.LookupAsync(identity));
        int deliveries=0;ExportKeyDeliveryReceivingRequest? received=null;
        router.DeliverReceivedAsync(Arg.Any<ExportKeyDeliveryReceivingRequest>()).Returns(call=> { deliveries++;received=call.Arg<ExportKeyDeliveryReceivingRequest>();return actor.DeliverReceivedAsync(received); });
        router.LookupReceivingRejectionAsync(Arg.Any<ExportKeyDeliveryReceivingRequest>()).Returns(async call=>
        { var denial=await actor.LookupReceivingRejectionAsync(call.Arg<ExportKeyDeliveryReceivingRequest>());return armed && changed=="receiving"?denial! with { ReceivedAt=denial.ReceivedAt.AddMilliseconds(1) }:denial; });
        const string caseId="S3.delivery-substitution.export-id";var request=QualificationHostTests.Request();request["caseId"]=caseId;request["requestId"]="request";var scope=request["enrolledScope"]!.AsObject();
        var workflow=new QualificationExportDeliveryWorkflow(returnedStore,new Dictionary<string,QualificationExportDeliveryInput>{{caseId,new(identity,QualificationProtocolSchema.Digest(scope))}},_=>router);
        var registration=workflow.Registrations().Single();var executed=await registration.Execute(request,token);executed.ShouldNotBeNull();deliveries.ShouldBe(1);
        var original=await actor.LookupReceivingRejectionAsync(received!);original.ShouldNotBeNull();string originalBytes=JsonSerializer.Serialize(original,CustodyBindingContract.Json),executionBytes=executed.ToJsonString();
        var native=original with { ReceivedAt=changed=="native"?original.ReceivedAt.AddMilliseconds(1):original.ReceivedAt };
        var audit=Substitute.For<IQualificationNativeCaseAuditReader>();audit.ReadAsync(request,Arg.Any<CancellationToken>()).Returns(new QualificationNativeCaseAudit(1,[],[],[],[],new JsonObject { ["principalDeliveryDenialOriginal"]=JsonSerializer.SerializeToNode(native,CustodyBindingContract.Json) }));
        IQualificationObservedOriginalReader reader=new QualificationExportDeliveryOriginalReader(returnedStore,audit,workflow);armed=true;
        for(int i=0;i<2;i++)
        {
            var observation=await reader.ReadAsync(request,token);
            if(changed=="agreement") observation.ShouldNotBeNull().Source["state"]!["outcome"]!.GetValue<string>().ShouldBe("Denied");else observation.ShouldBeNull();
        }
        JsonSerializer.Serialize(await actor.LookupReceivingRejectionAsync(received!),CustodyBindingContract.Json).ShouldBe(originalBytes);
        (await retainedStore.LookupResultAsync("executor","request:case-delivery-rejection-v4",token))!.ToJsonString().ShouldBe(executionBytes);
        deliveries.ShouldBe(1);nativeProvider.ReceivedCalls().ShouldBeEmpty();
    }
}
