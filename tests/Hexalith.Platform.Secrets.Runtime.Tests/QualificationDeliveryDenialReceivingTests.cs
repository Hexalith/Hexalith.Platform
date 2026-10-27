using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Actors;
using Dapr.Actors.Runtime;
using Hexalith.Platform.Custody;
using Hexalith.EventStore.Contracts.Security;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class QualificationDeliveryDenialReceivingTests
{
    [Fact]
    public async Task CachedActualReceivingDenialRechecksCurrentDeliverAuthorityAfterSuspendedRead()
    {
        var identity=new ExportKeyDeliveryIdentity("tenant","delivery","export",1,"principal","key-use","audience",DateTimeOffset.UtcNow.AddHours(1),"v1","target",1);
        var request=new ExportKeyDeliveryReceivingRequest(identity,ExportKeyDeliveryRawIdentity.Capture(identity with { ExportId="changed" }));
        var original=new ExportKeyDeliveryReceivingDenialOriginal("ExportKeyDeliveryReceivingDenial.v2",identity,request.Supplied,new(identity,ExportKeyDeliveryState.Delivered),DateTimeOffset.UtcNow,"RetainedIdentityConflict");
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store=Substitute.For<IExportKeyDeliveryDenialOriginalStore>();store.LookupReceivedAsync(request,Arg.Any<CancellationToken>()).Returns(async _=> { entered.TrySetResult();await release.Task;return (ExportKeyDeliveryReceivingDenialOriginal?)original; });
        bool allowed=true;var authority=Substitute.For<IExportKeyDeliveryAuthority>();authority.AuthorizeOperationAsync(identity,"DeliverExportKey",Arg.Any<CancellationToken>()).Returns(_=>allowed);
        var provider=Substitute.For<IExportKeyDirectDeliveryProvider>();var actor=new ExportKeyDeliveryActor(ActorHost.CreateForTest<ExportKeyDeliveryActor>(new ActorTestOptions { ActorId=new(identity.ActorId) }),TimeProvider.System,authority,provider,store);
        var result=actor.DeliverReceivedAsync(request);await entered.Task.WaitAsync(TimeSpan.FromSeconds(2),TestContext.Current.CancellationToken);allowed=false;release.TrySetResult();
        (await result).State.ShouldBe(ExportKeyDeliveryState.Unavailable);provider.ReceivedCalls().ShouldBeEmpty();
        (await store.LookupReceivedAsync(request,TestContext.Current.CancellationToken)).ShouldBe(original);
    }
    [Theory]
    [InlineData("valid-original","export-id")]
    [InlineData("before-deliver","export-id")][InlineData("lost-conflict","export-id")][InlineData("lost-outer","export-id")][InlineData("legacy-uncertainty","export-id")]
    [InlineData("lost-conflict","tenant")][InlineData("lost-conflict","delivery-id")][InlineData("lost-conflict","ack-revision")][InlineData("lost-conflict","requester")]
    [InlineData("lost-conflict","purpose")][InlineData("lost-conflict","audience")][InlineData("lost-conflict","expiry")][InlineData("lost-conflict","lifecycle-version")][InlineData("lost-conflict","store-target")][InlineData("lost-conflict","contract-version")]
    public async Task ActualActorRetainsExactReceivingDenialAndRecoveryNeverInfersOneFromCurrentComparison(string boundary,string field)
    {
        var token=TestContext.Current.CancellationToken;
        using var independent=new IndependentAuthorityReceiverTests.Fixture();
        independent.State=independent.State with { Callers=independent.State.Callers.Select(value=>value with { Role="private-export-delivery-denial-v1",Methods=["qualification-begin-original","qualification-lookup-original"] }).ToArray() };
        var identity=new ExportKeyDeliveryIdentity("tenant","delivery","export",1,"principal","key-use","audience",DateTimeOffset.UtcNow.AddHours(1),"v1","target",1);
        var current=new ExportKeyDeliveryOutcome(identity,ExportKeyDeliveryState.Delivered,DateTimeOffset.UtcNow);
        var state=Substitute.For<IActorStateManager>();state.TryGetStateAsync<ExportKeyDeliveryOutcome>("export-key-delivery-v1",Arg.Any<CancellationToken>()).Returns(_=>new ConditionalValue<ExportKeyDeliveryOutcome>(true,current));
        state.TryGetStateAsync<AnchoredStateTransition>(Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(new ConditionalValue<AnchoredStateTransition>(false,null!));
        var authority=Substitute.For<IExportKeyDeliveryAuthority>();authority.AuthorizeOperationAsync(Arg.Any<ExportKeyDeliveryIdentity>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(true);
        authority.ValidateStateAsync(Arg.Any<ExportKeyDeliveryIdentity>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(call=>call.Arg<string>()==Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(current))));
        var native=Substitute.For<IExportKeyDirectDeliveryProvider>();
        var actors=new ActorRegistrationCollection();new PrivateExportKeyDeliveryOwner(actors,authority,native).RegisterInstalledPrivateExportKeyDeliveryOwner(independent.PrivateTransport(),independent.PublicBinding);
        using(var general=new ServiceCollection().BuildServiceProvider()) general.GetService<IExportKeyDeliveryDenialOriginalStore>().ShouldBeNull();
        ExportKeyDeliveryActor Actor()
        {
            var actor=(ExportKeyDeliveryActor)actors.Single().Activator!.CreateAsync(ActorHost.CreateForTest<ExportKeyDeliveryActor>(new ActorTestOptions { ActorId=new(identity.ActorId) })).GetAwaiter().GetResult().Actor;
            typeof(Dapr.Actors.Runtime.Actor).GetProperty("StateManager",BindingFlags.Public|BindingFlags.Instance)!.SetValue(actor,state);return actor;
        }
        if(boundary=="valid-original")
        {
            var received=new ExportKeyDeliveryReceivingRequest(identity,ExportKeyDeliveryRawIdentity.Capture(identity));
            var valid=await Actor().DeliverReceivedAsync(received);valid.State.ShouldBe(ExportKeyDeliveryState.Delivered);valid.NativeOutcome.ShouldBe(current);valid.Denial.ShouldBeNull();independent.Saves.ShouldBe(0);
            authority.AuthorizeOperationAsync(Arg.Any<ExportKeyDeliveryIdentity>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(false);
            (await Actor().DeliverReceivedAsync(received)).State.ShouldBe(ExportKeyDeliveryState.Unavailable);independent.Saves.ShouldBe(0);native.ReceivedCalls().ShouldBeEmpty();
            await Should.ThrowAsync<ArgumentException>(()=>Actor().DeliverReceivedAsync(received with { Supplied=received.Supplied with { ExportId=new string('x',2049) } }));return;
        }
        var scope=new JsonObject { ["tenantId"]="tenant" };var request=new JsonObject { ["caseId"]="S3.delivery-substitution."+field,["requestId"]="request",["enrolledScope"]=scope };
        var outer=Substitute.For<IQualificationOriginalStore>();var results=new Dictionary<string,JsonObject>();bool admitted=false;
        outer.BeginAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()).Returns(call=> { if((call.ArgAt<string>(1).EndsWith("v3",StringComparison.Ordinal) || call.ArgAt<string>(1).EndsWith("v4",StringComparison.Ordinal))) return true;bool first=!admitted;admitted=true;return first; });
        outer.LookupResultAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(call=>results.GetValueOrDefault(call.ArgAt<string>(1)));
        outer.RetainAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()).Returns(call=> { if(boundary=="lost-outer") throw new IOException("outer response lost before retention");results[call.ArgAt<string>(1)]=(JsonObject)call.ArgAt<JsonObject>(2).DeepClone();return Task.CompletedTask; });
        var router=Substitute.For<IExportKeyDeliveryActor>();int delivers=0;
        router.LookupAsync(identity).Returns(_=>Task.FromResult(current));
        router.LookupReceivingRejectionAsync(Arg.Any<ExportKeyDeliveryReceivingRequest>()).Returns(call=>Actor().LookupReceivingRejectionAsync(call.Arg<ExportKeyDeliveryReceivingRequest>()));
        router.DeliverReceivedAsync(Arg.Any<ExportKeyDeliveryReceivingRequest>()).Returns(async call=>
        { delivers++;if(boundary=="before-deliver") throw new IOException("interrupted after Begin before Deliver");var outcome=await Actor().DeliverReceivedAsync(call.Arg<ExportKeyDeliveryReceivingRequest>());if(boundary=="lost-conflict") throw new IOException("actual receiving conflict response lost");return outcome; });
        var workflow=new QualificationExportDeliveryWorkflow(outer,new Dictionary<string,QualificationExportDeliveryInput>{{"S3.delivery-substitution."+field,new(identity,QualificationProtocolSchema.Digest(scope))}},_=>router);
        if(boundary=="legacy-uncertainty") { admitted=true;results["request:case-result"]=new JsonObject { ["delivery"]=JsonSerializer.SerializeToNode(new ExportKeyDeliveryOutcome(identity,ExportKeyDeliveryState.Unknown),CustodyBindingContract.Json) }; }
        if(boundary!="legacy-uncertainty") { Exception? failure=null;try { await workflow.ExecuteAsync(request,token); }catch(Exception error) { failure=error; }failure.ShouldNotBeNull();failure.ShouldBeOfType<IOException>(failure.ToString()); }
        // Mutating current actor state cannot create an original for an uninvoked denial.
        var original=current;current=current with { Identity=identity with { ExportId="different-current-export" } };independent.Restart();
        if(boundary is "before-deliver" or "legacy-uncertainty")
        { (await workflow.LookupAsync(request,token)).ShouldBeNull();delivers.ShouldBe(boundary=="before-deliver"?1:0);workflow.MissingReadInputs.ShouldContain("InstalledExportKeyDeliveryReceivingDenialOriginalRoleCredentialAndIndependentStore"); }
        else
        {
            outer.RetainAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()).Returns(call=> { results[call.ArgAt<string>(1)]=(JsonObject)call.ArgAt<JsonObject>(2).DeepClone();return Task.CompletedTask; });
            var recovered=await workflow.LookupAsync(request,token);recovered.ShouldNotBeNull();recovered["delivery"]!["State"]!.GetValue<string>().ShouldBe("Conflict");
            var denial=recovered["receivingDenialOriginal"]!.Deserialize<ExportKeyDeliveryReceivingDenialOriginal>(CustodyBindingContract.Json)!;denial.RetainedOriginal.ShouldBe(original);denial.RejectionStage.ShouldBe(field is "tenant" or "delivery-id" or "contract-version"?"DeliverInputValidation":"RetainedIdentityConflict");
            JsonNode.DeepEquals(recovered,await workflow.LookupAsync(request,token)).ShouldBeTrue();delivers.ShouldBe(1);
            independent.RevokeCaller();(await Actor().LookupReceivingRejectionAsync(new(denial.Target,denial.Requested))).ShouldBeNull();
        }
        native.ReceivedCalls().ShouldBeEmpty();
        authority.ReceivedCalls().Where(call=>call.GetMethodInfo().Name==nameof(IExportKeyDeliveryAuthority.AuthorizeOperationAsync)).ShouldAllBe(call=>Equals(call.GetArguments()[0],identity));
    }
}
