using System.Text;
using System.Text.Json;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.PayloadProtection;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

// Collector/transport fixture only; the compared return buffer is fixture supplied. It establishes no installed engine, physical erasure or complete H3 qualification.
public sealed class Fr34IndependentCollectorTests
{
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task ExactOriginalNativeBytesAndConcreteReturnComparisonAreRetainedWithoutSecondUnseal(bool changedReturn)
    {
        using var authority=new ReceivingAuthorityFixture("fr34-observer");var gateway=Substitute.For<IEventStoreGatewayClient>();
        var bindings=Substitute.For<IInteractionProtectionBindingProvider>();var actual=new InteractionPayloadProtectionService(Substitute.For<IInteractionOccurrenceRegistry>(),Substitute.For<IInteractionOccurrenceKeyProvider>(),bindings,TimeProvider.System);
        var actualIdentity=actual.GetIdentity();var target=new Fr34ProtectionTarget(actualIdentity.Name,actualIdentity.SourceRevision,"custody","tenant");string canary=new string('a',64),message="01ARZ3NDEKTSV4RRFFQ69G5FAV";
        var identity=new AggregateIdentity("tenant","fr34-canary",canary);var reference=new Fr34CanaryReference(target,canary,identity.ActorId,message);
        byte[] plain=Encoding.UTF8.GetBytes("fixture-private-random-canary");var command=new PersistFr34Canary(new(Convert.ToBase64String(plain),new("tenant",canary,"fr34-"+canary)));
        string originalId=EventStorePrivateRecordingPort.StableUlid("fr34","tenant",canary);
        authority.State=authority.State with { Originals=new(){{"fr34-engine:"+originalId,new(JsonSerializer.SerializeToElement(command,new JsonSerializerOptions(JsonSerializerDefaults.Web)),null,"Dispatched",RetainedRevision:1)}},
            Callers=authority.State.Callers.Select(value=>value with { Methods=value.Methods.Concat(["qualification-retain-result","qualification-read-original:fr34-engine"]).ToArray() }).ToArray() };
        var metadata=new EventStorePayloadProtectionMetadata(PayloadProtectionState.Protected,1,"hexalith-pdenc-v2",message,null,null);
        var item=new StreamReadEvent(1,typeof(Fr34CanaryPersisted).FullName!,"fixture-ciphertext-native-bytes"u8.ToArray(),"json+pdenc-v2",1,message,null,null,DateTimeOffset.UtcNow,"writer",metadata) { NativeEventDigest=new string('b',64) };
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(),Arg.Any<CancellationToken>()).Returns(call=>
        {
            var read=call.Arg<StreamReadRequest>();var proof=NativeSourceProofCodec.ExtendDigest(NativeSourceProofCodec.CreateSeed(identity,message),identity,1,message,item.NativeEventDigest!);
            var events=read.FromSequence==0 && read.ToSequence!=0?new[]{item}:Array.Empty<StreamReadEvent>();
            return new StreamReadPage(identity.TenantId,identity.Domain,identity.AggregateId,events,new(read.FromSequence,read.ToSequence,events.Length==0?null:1,1,events.Length,false,null) { NativeProof=proof });
        });
        var reader=new Fr34EventStoreCanaryReader(gateway,TimeProvider.System);var engine=new EventStoreFr34CanaryEngine(actual,reader,gateway,Substitute.For<IEventStoreGatewayBoundCommandProofSource>(),authority.Authority,"principal",null!,Substitute.For<ICustodyReservedManifestSource>());
        var collector=new Fr34IndependentOriginalCollector(engine,reader,authority.Authority);
        var measured=await collector.RecordUnsealAsync(reference,changedReturn?"changed"u8.ToArray():plain,TestContext.Current.CancellationToken);
        measured.ShouldNotBeNull();measured.PersistedPlaintextAbsent.ShouldBeTrue();measured.UnsealEqual.ShouldBe(!changedReturn);measured.EngineRevision.ShouldBe(actualIdentity.SourceRevision);
        JsonSerializer.Serialize(measured).ShouldNotContain("fixture-private-random-canary");
        (await collector.LookupAsync(reference,TestContext.Current.CancellationToken)).ShouldBe(measured);
        (await collector.RecordUnsealAsync(reference,"another"u8.ToArray(),TestContext.Current.CancellationToken)).ShouldBe(measured);
        authority.State.Originals.Keys.ShouldNotContain(key=>key.StartsWith("fr34-observer:",StringComparison.Ordinal));authority.State.PrivateOriginalsCount.ShouldBe(2);
        gateway.ReceivedCalls().ShouldAllBe(call=>call.GetMethodInfo().Name==nameof(IEventStoreGatewayClient.ReadStreamAsync));
        System.Security.Cryptography.CryptographicOperations.ZeroMemory(plain);
    }
}
