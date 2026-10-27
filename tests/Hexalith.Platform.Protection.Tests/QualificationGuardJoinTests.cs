using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

public sealed class QualificationGuardJoinTests
{
    [Fact]
    public async Task ReceivingWorkflowJoinsFaultedEffectAndGenuinelyRunningTriggerBeforeReleasingOwnership()
    {
        var token=TestContext.Current.CancellationToken;using var originalsAuthority=new ReceivingAuthorityFixture("executor");var originals=new InstalledQualificationOriginalStore(originalsAuthority.Authority);
        var scope=new JsonObject { ["tenantId"]="tenant",["batchId"]="batch" };
        var batch=new DeletionBatchConsumptionRequest(new("issuer","audience","tenant","deletion","seal","Accepted",0,"batch",new string('a',64),"guard",1,1,1,"v1"),"detached",2,"dispatch",3,[new("tenant","interaction","alias")]);
        var effect=new QualificationGuardStep(QualificationGuardStepKind.Register,Batch:batch);
        var trigger=new QualificationGuardStep(QualificationGuardStepKind.Block,Block:new("tenant","batch","block","integrity"));
        var definition=new QualificationGuardCaseDefinition("v23.no-false-replacement","tenant","batch",QualificationProtocolSchema.Digest(scope),[],effect,trigger,"at",[]);
        var protection=Substitute.For<IDeletionProtectionOwner>();var consumption=Substitute.For<IDeletionConsumptionActor>();
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource<DeletionConsumptionOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        protection.RegisterAsync(batch,Arg.Any<CancellationToken>()).Returns(Task.FromException<DeletionConsumptionOutcome>(new IOException("fixture-effect-failure")));
        consumption.BlockAsync(trigger.Block!).Returns(_=> { entered.TrySetResult();return release.Task; });
        var subscriber=new DeletionCapabilityRevocationSubscriber(new("issuer","audience","tenant"),Substitute.For<IDeletionCapabilityRevocationAuthenticator>(),Substitute.For<IDeletionCapabilityCompromiseRegistrar>(),Substitute.For<IDeletionCapabilityGuardRevocationMirror>(),TimeProvider.System);
        var workflow=new QualificationGuardCaseWorkflow(Substitute.For<IGovernanceScopeGuard>(),protection,consumption,Substitute.For<IDeletionCapabilitySigningActor>(),new SerializedStore(originals),new Dictionary<string,QualificationGuardCaseDefinition>{{definition.CaseId,definition}},subscriber);
        var request=new JsonObject { ["requestId"]=new string('c',64),["caseId"]=definition.CaseId,["enrolledScope"]=scope };
        using var receiving=new ReceivingAuthorityFixture("executor",handlers:[new Handler(workflow)]);
        receiving.State=receiving.State with { Callers=receiving.State.Callers.Select(value=>value with { Methods=["fixture-guard"] }).ToArray() };
        var pending=receiving.Authority.InvokeAsync<JsonElement>("fixture-guard",request,token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3),token);await Task.Delay(100,token);pending.IsCompleted.ShouldBeFalse();
        var step1=await originals.LookupRequestAsync("executor",new string('c',64)+":1",token);var step2=await originals.LookupRequestAsync("executor",new string('c',64)+":2",token);
        step1.ShouldNotBeNull();step2.ShouldNotBeNull();step1["step"]!["Kind"]!.GetValue<string>().ShouldBe("Register");step2["step"]!["Kind"]!.GetValue<string>().ShouldBe("Block");
        release.TrySetResult(new("tenant","batch",DeletionConsumptionStatus.ConsumptionBlocked,1,0,"block",null,null,null,[]));
        (await pending).ValueKind.ShouldBe(JsonValueKind.Undefined);
        (await originals.LookupResultAsync("executor",new string('c',64)+":2",token)).ShouldNotBeNull();
        await protection.Received(1).RegisterAsync(batch,Arg.Any<CancellationToken>());await consumption.Received(1).BlockAsync(trigger.Block!);
        // Both native step originals survive; the failed effect has no invented result.
        (await originals.LookupResultAsync("executor",new string('c',64)+":1",token)).ShouldBeNull();
    }
    private sealed class SerializedStore(IQualificationOriginalStore inner):IQualificationOriginalStore
    {
        private readonly SemaphoreSlim _gate=new(1,1);
        public async Task<bool> BeginAsync(string role,string id,JsonObject input,CancellationToken token)
        { await _gate.WaitAsync(token);try { return await inner.BeginAsync(role,id,input,token); }finally { _gate.Release(); } }
        public Task<JsonObject?> LookupResultAsync(string role,string id,CancellationToken token)=>inner.LookupResultAsync(role,id,token);
        public async Task RetainAsync(string role,string id,JsonObject output,CancellationToken token)
        { await _gate.WaitAsync(token);try { await inner.RetainAsync(role,id,output,token); }finally { _gate.Release(); } }
    }
    private sealed class Handler(QualificationGuardCaseWorkflow workflow):IIndependentOwnerOperationHandler
    {
        public IReadOnlySet<string> Methods { get; }=new HashSet<string>(["fixture-guard"]);
        public async Task<object?> HandleAsync(string method,JsonElement payload,IndependentCallerGrant caller,IndependentAuthorityState current,CancellationToken token)
            =>await workflow.ExecuteAsync(JsonNode.Parse(payload.GetRawText())!.AsObject(),token);
    }
}
