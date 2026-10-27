using System.Text.Json;
using System.Text.Json.Nodes;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class QualificationObservationOrchestratorTests
{
    [Theory]
    [InlineData("AfterEffect",false)][InlineData("AfterEffect",true)]
    [InlineData("AfterRestart",false)][InlineData("AfterRestart",true)]
    [InlineData("AfterRestore",false)][InlineData("AfterRestore",true)]
    [InlineData("AfterLookup",false)][InlineData("AfterLookup",true)]
    [InlineData("restart",false)][InlineData("restart",true)]
    [InlineData("restore",false)][InlineData("restore",true)]
    public async Task IndependentSignedStagesRecoverBeforeLaterControlsWithoutRemeasuringRetainedStage(string boundary,bool after)
    {
        var request=QualificationHostTests.Request();string id=request["requestId"]!.GetValue<string>();var store=new Store();
        string failedId=id+(boundary is "restart" or "restore"?":"+boundary:":observation-stage-v2:"+boundary);
        store.Fail=failedId;store.After=after;
        var recovery=Substitute.For<IQualificationRecoveryController>();var reader=Substitute.For<IQualificationObservedOriginalReader>();
        recovery.PreflightAsync(Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()).Returns(Array.Empty<string>());
        var remoteStages=new Dictionary<string,QualificationObservedOriginal>();
        reader.LookupStageAsync(Arg.Any<JsonObject>(),Arg.Any<QualificationObservationStageRequest>(),Arg.Any<CancellationToken>()).Returns(call=>remoteStages.GetValueOrDefault(call.ArgAt<QualificationObservationStageRequest>(1).Stage));
        string incarnation="effect";int effects=0,lookups=0,reads=0,controls=0;var originals=new Dictionary<string,QualificationRecoveryControlOriginal>();
        recovery.ExecuteAsync(Arg.Any<string>(),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()).Returns(call=>
        { string operation=call.ArgAt<string>(0);controls++;var receipt=new QualificationRecoveryControlOriginal(id,id+":"+operation,operation,new string('a',40),"isolated",incarnation,operation,new string('b',64),new string('c',64));incarnation=operation;originals.Add(operation,receipt);return receipt; });
        recovery.LookupAsync(Arg.Any<string>(),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()).Returns(call=>originals.GetValueOrDefault(call.ArgAt<string>(0)));
        recovery.Verify(Arg.Any<QualificationRecoveryControlOriginal>(),Arg.Any<JsonObject>()).Returns(true);
        reader.ReadStageAsync(Arg.Any<JsonObject>(),Arg.Any<QualificationObservationStageRequest>(),Arg.Any<CancellationToken>()).Returns(_=>
        { reads++;var stage=_.ArgAt<QualificationObservationStageRequest>(1);var native=new QualificationNativeStageBoundary(stage.Stage,incarnation,"native-"+reads,stage.ControlOperationId,stage.Stage=="AfterEffect"?null:stage.ControlOriginalSha256??new string('d',64));
            var measured=new QualificationObservedOriginal(id,new string('b',64),"original-1",new JsonObject { ["state"]=new JsonObject { ["nativeReceipt"]="immutable" },["incarnation"]=incarnation,["nativeStageBoundary"]=JsonSerializer.SerializeToNode(native,CustodyBindingContract.Json) },StageEvidence:new(stage,native));remoteStages.Add(stage.Stage,measured);return measured; });
        Task Effect(bool lookup,CancellationToken token) { if(lookup)lookups++;else effects++;return Task.CompletedTask; }
        await Should.ThrowAsync<IOException>(()=>new QualificationObservationOrchestrator(store,recovery,reader).ObserveAsync(request,Effect,TestContext.Current.CancellationToken));
        incarnation="changed-after-failure";store.Fail=null;
        var recovered=await new QualificationObservationOrchestrator(store,recovery,reader).ObserveAsync(request,Effect,TestContext.Current.CancellationToken);
        recovered.ShouldNotBeNull();effects.ShouldBe(1);controls.ShouldBe(2);reads.ShouldBe(4);
        var stages=recovered["snapshots"]!.AsArray();stages.Select(value=>value!["stage"]!.GetValue<string>()).ShouldBe(["AfterEffect","AfterRestart","AfterRestore","AfterLookup"]);
        stages[0]!["source"]!["incarnation"]!.GetValue<string>().ShouldBe("effect");
        // Complete restart/restore uses the retained metadata; neither current reads nor controls run again.
        string bytes=recovered.ToJsonString();incarnation="foreign-current-state";
        (await new QualificationObservationOrchestrator(store,recovery,reader).ObserveAsync(request,Effect,TestContext.Current.CancellationToken))!.ToJsonString().ShouldBe(bytes);
        reads.ShouldBe(4);controls.ShouldBe(2);
    }
    [Fact]
    public async Task MissingRecoveryProtocolDeniesBeforeEffect()
    {
        var recovery=Substitute.For<IQualificationRecoveryController>();recovery.PreflightAsync(Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()).Returns(new[]{"SelectedIsolatedRecoveryProtocol"});
        int effects=0;Task Effect(bool lookup,CancellationToken token) { effects++;return Task.CompletedTask; }
        (await new QualificationObservationOrchestrator(new Store(),recovery,Substitute.For<IQualificationObservedOriginalReader>()).ObserveAsync(QualificationHostTests.Request(),Effect,TestContext.Current.CancellationToken)).ShouldBeNull();effects.ShouldBe(0);
    }
    private sealed class Store:IQualificationOriginalStore
    {
        private readonly Dictionary<string,JsonObject> _requests=[],_results=[];internal string? Fail;internal bool After;
        public Task<bool> BeginAsync(string role,string id,JsonObject body,CancellationToken token)
        { bool first=!_requests.TryGetValue(id,out var prior);if(!first && !JsonNode.DeepEquals(body,prior)) throw new InvalidOperationException("Changed request");_requests.TryAdd(id,(JsonObject)body.DeepClone());return Task.FromResult(first); }
        public Task RetainAsync(string role,string id,JsonObject body,CancellationToken token)
        { if(id==Fail && !After) throw new IOException("Lost before durable retention");if(_results.TryGetValue(id,out var prior) && !JsonNode.DeepEquals(body,prior)) throw new InvalidOperationException("Changed result");_results.TryAdd(id,(JsonObject)body.DeepClone());if(id==Fail) throw new IOException("Lost after durable retention");return Task.CompletedTask; }
        public Task<JsonObject?> LookupResultAsync(string role,string id,CancellationToken token)=>Task.FromResult(_results.TryGetValue(id,out var body)?(JsonObject)body.DeepClone():null);
        public Task<QualificationOriginalAdmissionState> ReadAdmissionAsync(string role,string id,JsonObject exact,CancellationToken token)=>Task.FromResult(!_requests.ContainsKey(id)?QualificationOriginalAdmissionState.Absent:QualificationOriginalAdmissionState.Admitted);
    }
}
