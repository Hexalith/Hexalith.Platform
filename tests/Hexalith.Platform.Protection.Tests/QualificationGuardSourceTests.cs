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
    [Theory]
    [InlineData(QualificationGuardStepKind.Register)][InlineData(QualificationGuardStepKind.Reserve)][InlineData(QualificationGuardStepKind.Block)][InlineData(QualificationGuardStepKind.Activate)]
    public async Task LostNativeStepNeverSubstitutesMutableBatchOutcomeOrDispatchesLaterStep(QualificationGuardStepKind kind)
    {
        var scope=new JsonObject { ["tenantId"]="tenant",["batchId"]="batch" };string hash=QualificationProtocolSchema.Digest(scope);var batch=Batch();
        var lost=kind switch { QualificationGuardStepKind.Block=>new QualificationGuardStep(kind,Block:new("tenant","batch","block","integrity")),QualificationGuardStepKind.Activate=>new QualificationGuardStep(kind,Activation:new("activate","block",1,"replacement",batch)),_=>new QualificationGuardStep(kind,Batch:batch) };
        var after=new QualificationGuardStep(QualificationGuardStepKind.Register,Batch:batch);
        var definition=new QualificationGuardCaseDefinition("v23.no-false-replacement","tenant","batch",hash,[],lost,null,"none",[after]);
        var guard=Substitute.For<IGovernanceScopeGuard>();var protection=Substitute.For<IDeletionProtectionOwner>();var consumption=Substitute.For<IDeletionConsumptionActor>();var signer=Substitute.For<IDeletionCapabilitySigningActor>();var originals=Substitute.For<IQualificationOriginalStore>();
        originals.BeginAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()).Returns(false);
        protection.LookupAsync("tenant","batch",Arg.Any<CancellationToken>()).Returns(new DeletionConsumptionOutcome("tenant","batch",DeletionConsumptionStatus.ConsumptionBlocked,10,1,"different-current-batch",null,null,null,[]));
        var subscriber=new DeletionCapabilityRevocationSubscriber(new("issuer","audience","tenant"),Substitute.For<IDeletionCapabilityRevocationAuthenticator>(),Substitute.For<IDeletionCapabilityCompromiseRegistrar>(),Substitute.For<IDeletionCapabilityGuardRevocationMirror>(),TimeProvider.System);
        var workflow=new QualificationGuardCaseWorkflow(guard,protection,consumption,signer,originals,new Dictionary<string,QualificationGuardCaseDefinition>{{definition.CaseId,definition}},subscriber);
        var request=new JsonObject { ["requestId"]=new string('c',64),["caseId"]=definition.CaseId,["enrolledScope"]=scope };
        var error=await Should.ThrowAsync<InvalidOperationException>(()=>workflow.ExecuteAsync(request,TestContext.Current.CancellationToken));error.Message.ShouldContain("ExactNativeOperationOriginalProtocol."+kind);
        protection.ReceivedCalls().ShouldBeEmpty();consumption.ReceivedCalls().ShouldBeEmpty();guard.ReceivedCalls().ShouldBeEmpty();signer.ReceivedCalls().ShouldBeEmpty();
    }
    [Fact]
    public void TypedProjectionUsesActualTargetVectorsAndDoesNotInventMissingPhysicalOrHistoryMeasurements()
    {
        var batch=new GovernanceBatchState("batch","Accepted",0,[new("tenant","interaction","alias")],new string('a',64),1,"v1","digest","issue","dispatch","Consumed","receipt",["target-receipt"],0,[]);
        var deletion=new GovernanceDeletionState("deletion",new("tenant","ExactInteraction","interaction",""),new string('b',64),1,[],[],[],[],[],"seal",[batch],false,false);
        var guard=new TenantGovernanceGuardState("tenant","installation",1,"epoch","legacy","writers",null,[],[deletion],[],[],[],[]);
        var outcome=new DeletionConsumptionOutcome("tenant","batch",DeletionConsumptionStatus.Consumed,1,0,"receipt",null,null,null,[new(new("tenant","other","alias"),"batch","target-receipt")]);
        var changed=QualificationGuardInvariantProjection.Project("tenant","deletion","batch",guard,outcome,[],["atomicAllOrNone","globalKeyBlock","leakCount"]);
        changed.Metrics["atomicAllOrNone"].ShouldBe(0); changed.Metrics["globalKeyBlock"].ShouldBe(0);
        changed.MissingProtocols.ShouldContain("IndependentMeasuredOwnerHistoryOrBoundaryProtocol.leakCount");
        var correct=outcome with { TargetReceipts=[new(new("tenant","interaction","alias"),"batch","target-receipt")] };
        QualificationGuardInvariantProjection.Project("tenant","deletion","batch",guard,correct,[],["atomicAllOrNone"]).Metrics["atomicAllOrNone"].ShouldBe(1);
    }
    [Theory]
    [InlineData(2,1)][InlineData(3,0)]
    public void SuccessorOrdinalComesFromActualRetainedIssueCapabilitiesAndSubstitutedIssueCannotSupplyIt(long ordinal,long expected)
    {
        var predecessor=Batch().Capability;var successor=predecessor with { AttestationOrdinal=ordinal,CapabilityKeyVersion="v2",IntendedIssuedGuardRevision=4 };
        var batch=new GovernanceBatchState("batch","Accepted",0,[new("tenant","interaction","alias")],predecessor.ManifestDigest,ordinal,"v2","digest","new-issue","dispatch","Consumed","receipt",["target-receipt"],1,[]) {
            Capability=successor,IssuedGuardRevision=5,SigningRequestId="successor-signing" };
        var deletion=new GovernanceDeletionState("deletion",new("tenant","ExactInteraction","interaction",""),new string('b',64),1,[],[],[],[],[],"seal",[batch],false,false);
        var receipts=new[] { new GovernanceProtocolReceipt("old",new string('a',64),"Committed",2,0,"batch","old-issue") { Capability=predecessor },
            new GovernanceProtocolReceipt("new",new string('b',64),"Committed",5,0,"batch","new-issue") { Capability=successor,SigningRequestId="successor-signing" } };
        var guard=new TenantGovernanceGuardState("tenant","installation",6,"epoch","legacy","writers",null,[],[deletion],[],[],receipts,["v1"]);
        var outcome=new DeletionConsumptionOutcome("tenant","batch",DeletionConsumptionStatus.Consumed,3,1,"receipt",null,null,null,[]);
        QualificationGuardInvariantProjection.Project("tenant","deletion","batch",guard,outcome,[],["nextAttestationOrdinal"]).Metrics["nextAttestationOrdinal"].ShouldBe(expected);
        var substituted=guard with { Receipts=[receipts[0],receipts[1] with { Capability=successor with { TenantId="foreign" } }] };
        var denied=QualificationGuardInvariantProjection.Project("tenant","deletion","batch",substituted,outcome,[],["nextAttestationOrdinal"]);
        denied.Metrics.ShouldNotContainKey("nextAttestationOrdinal");denied.MissingProtocols.ShouldContain("IndependentMeasuredOwnerHistoryOrBoundaryProtocol.nextAttestationOrdinal");
    }
}
