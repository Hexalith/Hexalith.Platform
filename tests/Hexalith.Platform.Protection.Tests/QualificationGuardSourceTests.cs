using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

public sealed class QualificationGuardSourceTests
{
    private static DeletionBatchConsumptionRequest Batch()=>new(new("issuer","audience","tenant","deletion","seal","Accepted",0,"batch",new string('a',64),"guard",1,1,1,"v1"),"detached",2,"dispatch",3,[new("tenant","interaction","alias")]);
    private static Dictionary<string,JsonElement> Arguments()
    {
        var batch=Batch();
        return new() {
            ["1:Register"]=JsonSerializer.SerializeToElement(new QualificationGuardStep(QualificationGuardStepKind.Register,Batch:batch),CustodyBindingContract.Json),
            ["2:Block"]=JsonSerializer.SerializeToElement(new QualificationGuardStep(QualificationGuardStepKind.Block,Block:new("tenant","batch","block","integrity")),CustodyBindingContract.Json),
            ["3:Activate"]=JsonSerializer.SerializeToElement(new QualificationGuardStep(QualificationGuardStepKind.Activate,Activation:new("activate","block",1,"replacement",batch)),CustodyBindingContract.Json) };
    }
    [Fact]
    public void ClosedRecipeRejectsChangedOperationOrderingAndUnknownArguments()
    {
        var inputs=Arguments();
        var recipe=QualificationGuardRecipeCatalog.Compile("v23.no-false-replacement","tenant","batch",new string('b',64),inputs);
        recipe.Before.Select(value=>value.Kind).ShouldBe([QualificationGuardStepKind.Register,QualificationGuardStepKind.Block]);
        recipe.Effect.Kind.ShouldBe(QualificationGuardStepKind.Activate);
        inputs["3:Activate"]=JsonSerializer.SerializeToElement(new QualificationGuardStep(QualificationGuardStepKind.Reserve,Batch:Batch()),CustodyBindingContract.Json);
        Should.Throw<InvalidOperationException>(()=>QualificationGuardRecipeCatalog.Compile("v23.no-false-replacement","tenant","batch",new string('b',64),inputs));
        Should.Throw<InvalidOperationException>(()=>QualificationGuardRecipeCatalog.Compile("S4.migration.content-append.restored","tenant","batch",new string('b',64),new Dictionary<string,JsonElement>()));
    }
    [Fact]
    public async Task UnknownActualOwnerOutcomeRetainsOriginalAndPreventsAllLaterSteps()
    {
        var scope=new JsonObject { ["tenantId"]="tenant",["batchId"]="batch" };
        string scopeHash=Convert.ToHexStringLower(SHA256.HashData(QualificationProofVerifier.CanonicalBytes(scope)));
        var definition=QualificationGuardRecipeCatalog.Compile("v23.no-false-replacement","tenant","batch",scopeHash,Arguments());
        var guard=Substitute.For<IGovernanceScopeGuard>(); var protection=Substitute.For<IDeletionProtectionOwner>(); var consumption=Substitute.For<IDeletionConsumptionActor>();
        var signer=Substitute.For<IDeletionCapabilitySigningActor>(); var originals=Substitute.For<IQualificationOriginalStore>();
        originals.BeginAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()).Returns(true);
        protection.RegisterAsync(Arg.Any<DeletionBatchConsumptionRequest>(),Arg.Any<CancellationToken>()).Returns(new DeletionConsumptionOutcome("tenant","batch",DeletionConsumptionStatus.Unavailable,0,0,null,null,null,null,[]));
        var subscriber=new DeletionCapabilityRevocationSubscriber(new("issuer","audience","tenant"),Substitute.For<IDeletionCapabilityRevocationAuthenticator>(),Substitute.For<IDeletionCapabilityCompromiseRegistrar>(),Substitute.For<IDeletionCapabilityGuardRevocationMirror>(),TimeProvider.System);
        var workflow=new QualificationGuardCaseWorkflow(guard,protection,consumption,signer,originals,new Dictionary<string,QualificationGuardCaseDefinition>{{definition.CaseId,definition}},subscriber);
        var request=new JsonObject { ["caseId"]=definition.CaseId,["requestId"]=new string('c',64),["enrolledScope"]=scope };
        await Should.ThrowAsync<InvalidOperationException>(()=>workflow.ExecuteAsync(request,TestContext.Current.CancellationToken));
        consumption.ReceivedCalls().ShouldBeEmpty(); signer.ReceivedCalls().ShouldBeEmpty(); guard.ReceivedCalls().ShouldBeEmpty();
        protection.ReceivedCalls().Count().ShouldBe(1);
    }
    [Fact]
    public void TypedProjectionUsesActualTargetVectorsAndDoesNotInventMissingPhysicalOrHistoryMeasurements()
    {
        var batch=new GovernanceBatchState("batch","Accepted",0,[new("tenant","interaction","alias")],new string('a',64),1,"v1","digest","issue","dispatch","Consumed","receipt",["target-receipt"],0,[]);
        var deletion=new GovernanceDeletionState("deletion",new("tenant","ExactInteraction","interaction",""),new string('b',64),1,[],[],[],[],[],"seal",[batch],false,false);
        var guard=new TenantGovernanceGuardState("tenant","installation",1,"epoch","legacy","writers",null,[],[deletion],[],[],[],[]);
        var outcome=new DeletionConsumptionOutcome("tenant","batch",DeletionConsumptionStatus.Consumed,1,0,"receipt",null,null,null,[new(new("tenant","other","alias"),"batch","target-receipt")]);
        var changed=QualificationGuardInvariantProjection.Project("tenant","deletion","batch",guard,outcome,[],["atomicAllOrNone","globalKeyBlock","leakCount"]);
        changed.Metrics["atomicAllOrNone"].ShouldBe(0); changed.Metrics.ContainsKey("globalKeyBlock").ShouldBeFalse();
        changed.MissingProtocols.ShouldContain("IndependentMeasuredOwnerHistoryOrBoundaryProtocol.leakCount");
        var correct=outcome with { TargetReceipts=[new(new("tenant","interaction","alias"),"batch","target-receipt")] };
        QualificationGuardInvariantProjection.Project("tenant","deletion","batch",guard,correct,[],["atomicAllOrNone"]).Metrics["atomicAllOrNone"].ShouldBe(1);
    }
}
