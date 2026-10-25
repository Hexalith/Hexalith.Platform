using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class QualificationExportDeliverySourceTests
{
    [Theory]
    [InlineData("tenant")][InlineData("delivery-id")][InlineData("export-id")][InlineData("ack-revision")][InlineData("requester")][InlineData("purpose")]
    [InlineData("audience")][InlineData("expiry")][InlineData("lifecycle-version")][InlineData("store-target")][InlineData("contract-version")]
    public async Task ClosedSubstitutionUsesActualOriginalActorAndCannotRenewIt(string field)
    {
        var identity=new ExportKeyDeliveryIdentity("tenant","delivery","export",1,"principal","key-use","audience",DateTimeOffset.UtcNow.AddHours(1),"v1","target",1);
        var actor=Substitute.For<IExportKeyDeliveryActor>();var terminal=new ExportKeyDeliveryOutcome(identity,ExportKeyDeliveryState.Delivered,DateTimeOffset.UtcNow.AddSeconds(-1));
        actor.LookupAsync(identity).Returns(terminal);
        actor.DeliverAsync(Arg.Any<ExportKeyDeliveryIdentity>()).Returns(call=>new ExportKeyDeliveryOutcome(call.Arg<ExportKeyDeliveryIdentity>(),ExportKeyDeliveryState.Conflict));
        var originals=Substitute.For<IQualificationOriginalStore>();originals.BeginAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()).Returns(true);
        string id="S3.delivery-substitution."+field;var scope=new JsonObject { ["tenantId"]="tenant" };var request=new JsonObject { ["caseId"]=id,["requestId"]="request",["enrolledScope"]=scope };
        var workflow=new QualificationExportDeliveryWorkflow(originals,new Dictionary<string,QualificationExportDeliveryInput>{{id,new(identity,QualificationProtocolSchema.Digest(scope))}},arg=> { arg.ShouldBe(identity);return actor; });
        var result=await workflow.ExecuteAsync(request,TestContext.Current.CancellationToken);result!["delivery"]!["State"]!.GetValue<string>().ShouldBe("Conflict");
        (await workflow.ObserveAsync(request,TestContext.Current.CancellationToken)).ShouldBe(terminal);
        await actor.DidNotReceive().DeliverAsync(identity);
        if(field is "tenant" or "delivery-id" or "contract-version") actor.ReceivedCalls().Count(call=>call.GetMethodInfo().Name=="DeliverAsync").ShouldBe(0);
        else actor.ReceivedCalls().Count(call=>call.GetMethodInfo().Name=="DeliverAsync").ShouldBe(1);
    }
    [Fact]
    public async Task LostOriginalIsLookupOnlyAndDoesNotRetryPrincipalTransport()
    {
        var identity=new ExportKeyDeliveryIdentity("tenant","delivery","export",1,"principal","key-use","audience",DateTimeOffset.UtcNow.AddHours(1),"v1","target",1);
        var actor=Substitute.For<IExportKeyDeliveryActor>();actor.LookupAsync(identity).Returns(new ExportKeyDeliveryOutcome(identity,ExportKeyDeliveryState.Unknown));
        var originals=Substitute.For<IQualificationOriginalStore>();originals.BeginAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()).Returns(false);
        var scope=new JsonObject { ["tenantId"]="tenant" };var request=new JsonObject { ["caseId"]="Recovery.delivery.lost-result",["requestId"]="request",["enrolledScope"]=scope };
        var workflow=new QualificationExportDeliveryWorkflow(originals,new Dictionary<string,QualificationExportDeliveryInput>{{"Recovery.delivery.lost-result",new(identity,QualificationProtocolSchema.Digest(scope))}},_=>actor);
        (await workflow.ExecuteAsync(request,TestContext.Current.CancellationToken)).ShouldBeNull();(await workflow.ExecuteAsync(request,TestContext.Current.CancellationToken)).ShouldBeNull();
        await actor.DidNotReceive().DeliverAsync(Arg.Any<ExportKeyDeliveryIdentity>());await actor.Received(2).LookupAsync(identity);
    }
}
