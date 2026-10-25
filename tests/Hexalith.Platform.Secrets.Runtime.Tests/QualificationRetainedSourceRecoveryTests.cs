using System.Text.Json.Nodes;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class QualificationRetainedSourceRecoveryTests
{
    [Fact]
    public async Task MissingCaseAcknowledgementUsesSourceSpecificOriginalLookupWithoutRepeatingUncertainEffect()
    {
        var store=Substitute.For<IQualificationOriginalStore>();var request=new JsonObject { ["requestId"]="exact",["caseId"]="S4.capability-canonical-14-fields" };
        var sourceOriginal=new JsonObject { ["caseId"]=request["caseId"]!.DeepClone(),["requestId"]="exact",["sourceReceipt"]="original" };
        JsonObject? retained=null;int executes=0,lookups=0;
        store.LookupResultAsync("executor","exact:case-result",Arg.Any<CancellationToken>()).Returns(_=>Task.FromResult(retained));
        store.BeginAsync("executor","exact:case-result",request,Arg.Any<CancellationToken>()).Returns(false);
        store.ReadAdmissionAsync("executor","exact:case-result",request,Arg.Any<CancellationToken>()).Returns(QualificationOriginalAdmissionState.Admitted);
        store.RetainAsync("executor","exact:case-result",Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()).Returns(call=> { retained=(JsonObject)call.ArgAt<JsonObject>(2).DeepClone();return Task.CompletedTask; });
        var workflow=QualificationRuntimeWorkflowCatalog.Retained(request["caseId"]!.GetValue<string>(),store,(_,_)=> { executes++;throw new InvalidOperationException("Uncertain source effect cannot repeat."); },(_,_)=> { lookups++;return Task.FromResult<JsonObject?>((JsonObject)sourceOriginal.DeepClone()); });
        JsonNode.DeepEquals(await workflow.Lookup(request,TestContext.Current.CancellationToken),sourceOriginal).ShouldBeTrue();
        JsonNode.DeepEquals(await workflow.Execute(request,TestContext.Current.CancellationToken),sourceOriginal).ShouldBeTrue();executes.ShouldBe(0);lookups.ShouldBe(1);
        retained=null;store.ReadAdmissionAsync("executor","exact:case-result",request,Arg.Any<CancellationToken>()).Returns(QualificationOriginalAdmissionState.Unknown);
        (await workflow.Lookup(request,TestContext.Current.CancellationToken)).ShouldBeNull();lookups.ShouldBe(1);
    }
}
