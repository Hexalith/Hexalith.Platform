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
        actor.LookupAsync(Arg.Any<ExportKeyDeliveryIdentity>()).Returns(call=>call.Arg<ExportKeyDeliveryIdentity>()==identity?terminal:new ExportKeyDeliveryOutcome(call.Arg<ExportKeyDeliveryIdentity>(),ExportKeyDeliveryState.Conflict));
        actor.DeliverReceivedAsync(Arg.Any<ExportKeyDeliveryReceivingRequest>()).Returns(call=>
        { var received=call.Arg<ExportKeyDeliveryReceivingRequest>();System.Text.Json.JsonSerializer.Serialize(received,CustodyBindingContract.Json).ShouldNotBeNullOrWhiteSpace();return new ExportKeyDeliveryReceivingResult(received.Supplied,ExportKeyDeliveryState.Conflict,new("ExportKeyDeliveryReceivingDenial.v2",received.Target,received.Supplied,terminal,DateTimeOffset.UtcNow,"DeliverInputValidation")); });
        var originals=Substitute.For<IQualificationOriginalStore>();originals.BeginAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()).Returns(true);
        string id="S3.delivery-substitution."+field;var scope=new JsonObject { ["tenantId"]="tenant" };var request=new JsonObject { ["caseId"]=id,["requestId"]="request",["enrolledScope"]=scope };
        var workflow=new QualificationExportDeliveryWorkflow(originals,new Dictionary<string,QualificationExportDeliveryInput>{{id,new(identity,QualificationProtocolSchema.Digest(scope))}},arg=> { arg.ShouldBe(identity);return actor; });
        var result=await workflow.ExecuteAsync(request,TestContext.Current.CancellationToken);
        result.ShouldNotBeNull()["delivery"]!["State"]!.GetValue<string>().ShouldBe("Conflict");
        (await workflow.PreflightAsync(request,TestContext.Current.CancellationToken)).ShouldBeEmpty();
        await actor.Received(1).DeliverReceivedAsync(Arg.Is<ExportKeyDeliveryReceivingRequest>(received=>received.Target==identity));
        await actor.DidNotReceive().DeliverAsync(Arg.Any<ExportKeyDeliveryIdentity>());
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
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task UnsettledDeliveryKeepsAdmissionPendingAndLegacyUncertaintySettlesAtVersionedOriginal(bool legacy)
    {
        var identity=new ExportKeyDeliveryIdentity("tenant","delivery","export",1,"principal","key-use","audience",DateTimeOffset.UtcNow.AddHours(1),"v1","target",1);
        var actor=Substitute.For<IExportKeyDeliveryActor>();var unknown=new ExportKeyDeliveryOutcome(identity,ExportKeyDeliveryState.Unknown);var terminal=new ExportKeyDeliveryOutcome(identity,ExportKeyDeliveryState.Delivered,DateTimeOffset.UtcNow);
        actor.DeliverAsync(identity).Returns(unknown);actor.LookupAsync(identity).Returns(unknown);
        var originals=Substitute.For<IQualificationOriginalStore>();var bodies=new Dictionary<string,JsonObject>();bool admitted=legacy;
        originals.BeginAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()).Returns(call=> { if(call.ArgAt<string>(1).EndsWith(":case-terminal-v2",StringComparison.Ordinal)) return true;bool first=!admitted;admitted=true;return first; });
        originals.LookupResultAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(call=>bodies.GetValueOrDefault(call.ArgAt<string>(1)));
        originals.RetainAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()).Returns(call=> { string id=call.ArgAt<string>(1);var value=call.ArgAt<JsonObject>(2);if(bodies.TryGetValue(id,out var old)) JsonNode.DeepEquals(old,value).ShouldBeTrue();bodies[id]=(JsonObject)value.DeepClone();return Task.CompletedTask; });
        var scope=new JsonObject { ["tenantId"]="tenant" };var request=new JsonObject { ["caseId"]="S3.principal-direct-delivery",["requestId"]="request",["enrolledScope"]=scope };
        var workflow=new QualificationExportDeliveryWorkflow(originals,new Dictionary<string,QualificationExportDeliveryInput>{{"S3.principal-direct-delivery",new(identity,QualificationProtocolSchema.Digest(scope))}},_=>actor);
        JsonObject? historical=null;
        if(legacy) { historical=new JsonObject { ["caseId"]="S3.principal-direct-delivery",["requestId"]="request",["delivery"]=System.Text.Json.JsonSerializer.SerializeToNode(unknown,CustodyBindingContract.Json) };bodies["request:case-result"]=historical; }
        (await workflow.ExecuteAsync(request,TestContext.Current.CancellationToken)).ShouldBeNull();
        if(!legacy) bodies.ShouldBeEmpty();
        actor.LookupAsync(identity).Returns(Task.FromException<ExportKeyDeliveryOutcome>(new IOException("fixture lookup outage")));
        await Should.ThrowAsync<IOException>(()=>workflow.LookupAsync(request,TestContext.Current.CancellationToken));
        actor.LookupAsync(identity).Returns(terminal);
        var settled=await workflow.LookupAsync(request,TestContext.Current.CancellationToken);settled!["delivery"]!["State"]!.GetValue<string>().ShouldBe("Delivered");
        JsonNode.DeepEquals(settled,await workflow.ExecuteAsync(request,TestContext.Current.CancellationToken)).ShouldBeTrue();
        if(legacy) { JsonNode.DeepEquals(bodies["request:case-result"],historical).ShouldBeTrue();bodies.ShouldContainKey("request:case-terminal-v2");await actor.DidNotReceive().DeliverAsync(identity); }
        else await actor.Received(1).DeliverAsync(identity);
    }

}
