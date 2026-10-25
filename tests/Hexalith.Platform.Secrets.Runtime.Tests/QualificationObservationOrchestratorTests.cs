using System.Text.Json.Nodes;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class QualificationObservationOrchestratorTests
{
    [Fact]
    public async Task ActualControlReceiptsPrecedeEveryStageAndRestartUsesExactLookupWithoutNewEffect()
    {
        var request=QualificationHostTests.Request(); string id=request["requestId"]!.GetValue<string>();
        var originals=Substitute.For<IQualificationOriginalStore>(); var retained=new HashSet<string>();
        originals.BeginAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()).Returns(call=>retained.Add(call.ArgAt<string>(1)));
        var control=Substitute.For<IQualificationRecoveryController>(); var reads=Substitute.For<IQualificationObservedOriginalReader>();
        control.PreflightAsync(Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()).Returns(Array.Empty<string>());
        QualificationRecoveryControlOriginal Receipt(string operation)=>new(id,id+":"+operation,operation,new string('a',40),"isolated","before-"+operation,"after-"+operation,new string('b',64),new string('c',64));
        control.ExecuteAsync(Arg.Any<string>(),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()).Returns(call=>Receipt(call.ArgAt<string>(0)));
        control.LookupAsync(Arg.Any<string>(),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()).Returns(call=>Receipt(call.ArgAt<string>(0)));
        control.Verify(Arg.Any<QualificationRecoveryControlOriginal>(),Arg.Any<JsonObject>()).Returns(true);
        reads.ReadAsync(Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()).Returns(new QualificationObservedOriginal(id,new string('b',64),"original-1",new JsonObject { ["nativeReceipt"]="actual-isolated-owner-original" }));
        var calls=new List<bool>();
        Task Effect(bool lookup,CancellationToken token) { calls.Add(lookup); return Task.CompletedTask; }
        var first=await new QualificationObservationOrchestrator(originals,control,reads).ObserveAsync(request,Effect,TestContext.Current.CancellationToken);
        first.ShouldNotBeNull(); first["snapshots"]!.AsArray().Select(value=>value!["stage"]!.GetValue<string>()).ShouldBe(["AfterEffect","AfterRestart","AfterRestore","AfterLookup"]);
        var recovered=await new QualificationObservationOrchestrator(originals,control,reads).ObserveAsync(request,Effect,TestContext.Current.CancellationToken);
        recovered.ShouldNotBeNull(); calls.ShouldBe([false,true,true,true]);
        await control.Received(2).ExecuteAsync(Arg.Any<string>(),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>());
        await control.Received(2).LookupAsync(Arg.Any<string>(),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MissingRecoveryProtocolDeniesBeforeAnyEffectAndUnknownControlCannotCreateStage()
    {
        var request=QualificationHostTests.Request(); string id=request["requestId"]!.GetValue<string>();
        var originals=Substitute.For<IQualificationOriginalStore>(); var control=Substitute.For<IQualificationRecoveryController>(); var reader=Substitute.For<IQualificationObservedOriginalReader>();
        int effects=0;
        Task Effect(bool lookup,CancellationToken token) { if(!lookup) effects++; return Task.CompletedTask; }
        control.PreflightAsync(Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()).Returns(new[]{"SelectedIsolatedRecoveryProtocol"});
        (await new QualificationObservationOrchestrator(originals,control,reader).ObserveAsync(request,Effect,TestContext.Current.CancellationToken)).ShouldBeNull(); effects.ShouldBe(0); originals.ReceivedCalls().ShouldBeEmpty();
        control.PreflightAsync(Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()).Returns(Array.Empty<string>());
        originals.BeginAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()).Returns(true);
        reader.ReadAsync(Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()).Returns(new QualificationObservedOriginal(id,new string('b',64),"original-1",new JsonObject { ["nativeReceipt"]="original" }));
        (await new QualificationObservationOrchestrator(originals,control,reader).ObserveAsync(request,Effect,TestContext.Current.CancellationToken)).ShouldBeNull(); effects.ShouldBe(1);
        await reader.Received(1).ReadAsync(Arg.Any<JsonObject>(),Arg.Any<CancellationToken>());
    }
}
