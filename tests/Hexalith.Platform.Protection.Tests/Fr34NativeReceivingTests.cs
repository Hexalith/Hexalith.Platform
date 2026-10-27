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

public sealed class Fr34NativeReceivingTests
{
    private static JsonElement Wire<T>(T value)=>JsonSerializer.SerializeToElement(value,CustodyBindingContract.Json);
    [Fact]
    public async Task ActualReceivingReaderUsesNativePersistedBytesAndIndependentBindingForOwnershipCarrierAndErasure()
    {
        var gateway=Substitute.For<IEventStoreGatewayClient>();
        var bindings=Substitute.For<IInteractionProtectionBindingProvider>();
        var actualEngine=new InteractionPayloadProtectionService(Substitute.For<IInteractionOccurrenceRegistry>(),Substitute.For<IInteractionOccurrenceKeyProvider>(),bindings,TimeProvider.System);
        var identity=actualEngine.GetIdentity(); identity.SourceRevision.Length.ShouldBe(40);
        var target=new Fr34ProtectionTarget(identity.Name,identity.SourceRevision,"isolated-custody","tenant-a");
        string canaryId=new string('a',64),keyReference="01ARZ3NDEKTSV4RRFFQ69G5FAV";
        var owner=new AggregateIdentity("tenant-a","fr34-canary",canaryId);
        var reference=new Fr34CanaryReference(target,canaryId,owner.ActorId,keyReference);
        var metadata=new EventStorePayloadProtectionMetadata(PayloadProtectionState.Protected,1,"hexalith-pdenc-v2",keyReference,null,null);
        bool missingNative=false,erased=false;
        var item=new StreamReadEvent(1,typeof(Fr34CanaryPersisted).FullName!,Encoding.UTF8.GetBytes("{\"ProtectedContent\":{\"Value\":\"independently-persisted-fixture-ciphertext\"}}"),
            "json+pdenc-v2",1,keyReference,null,null,DateTimeOffset.UtcNow,"fixture-worker",metadata) { NativeEventDigest=new string('b',64) };
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(),Arg.Any<CancellationToken>()).Returns(call=>
        {
            var request=call.ArgAt<StreamReadRequest>(0);
            var proof=NativeSourceProofCodec.ExtendDigest(NativeSourceProofCodec.CreateSeed(owner,keyReference),owner,1,item.MessageId,item.NativeEventDigest!);
            var events=request.FromSequence==0 && request.ToSequence!=0?new[]{item}:Array.Empty<StreamReadEvent>();
            return Task.FromResult(new StreamReadPage(owner.TenantId,owner.Domain,owner.AggregateId,events,new(request.FromSequence,request.ToSequence,
                events.Length==0?null:1,1,events.Length,false,null) { NativeProof=missingNative?null:proof }));
        });
        var occurrence=new InteractionOccurrenceIdentity(new(target.TenantId,canaryId,"fr34-"+canaryId),owner,PayloadProtectionPayloadKind.Event,1,typeof(Fr34CanaryPersisted).FullName!,"root-v1","v1");
        var binding=new InteractionProtectionBinding(occurrence,"digest-v1",1,keyReference,keyReference,1);
        bindings.ResolveAsync(owner,Arg.Any<PayloadProtectionOccurrenceContext>(),null,false,Arg.Any<CancellationToken>()).Returns(binding);
        bindings.IsIrreversiblyErasedAsync(binding,Arg.Any<CancellationToken>()).Returns(_=>erased);
        var reader=new Fr34EventStoreCanaryReader(gateway,TimeProvider.System);
        using var authority=new ReceivingAuthorityFixture("fr34-engine");
        var engine=new EventStoreFr34CanaryEngine(actualEngine,reader,gateway,Substitute.For<IEventStoreGatewayBoundCommandProofSource>(),authority.Authority,"private-worker",null!,Substitute.For<ICustodyReservedManifestSource>());
        var handler=new Fr34OwnerOperationHandler(engine,reader,bindings,TimeProvider.System);
        var state=authority.State with { Fr34Target=target,Fr34AuthorizationRevision="independent-policy-1" };
        var caller=state.Callers[0] with { Role="fr34-observer",ScopeReference="tenant-a|fr34" };
        var current=(Fr34CanaryAuthorization?)await handler.HandleAsync("fr34-observe-current",Wire(new { expected=target }),caller,state,TestContext.Current.CancellationToken);
        current.ShouldNotBeNull();
        var actual=(Fr34PersistedCanary?)await handler.HandleAsync("fr34-read-persisted",Wire(new { reference }),caller,state,TestContext.Current.CancellationToken);
        actual.ShouldNotBeNull(); actual.Bytes.ShouldBe(item.Payload);
        bool Allowed(object? result)=>Wire(result).GetProperty("Allowed").GetBoolean();
        Allowed(await handler.HandleAsync("fr34-verify-ownership",Wire(new { expected=target,originalCanaryId=canaryId,reference,admitted=current }),caller,state,TestContext.Current.CancellationToken)).ShouldBeTrue();
        Allowed(await handler.HandleAsync("fr34-verify-carrier",Wire(new { expected=target,originalCanaryId=canaryId,reference,observation=actual,admitted=current }),caller,state,TestContext.Current.CancellationToken)).ShouldBeTrue();
        Allowed(await handler.HandleAsync("fr34-verify-carrier",Wire(new { expected=target,originalCanaryId=canaryId,reference,observation=actual with { Bytes=[1,2] },admitted=current }),caller,state,TestContext.Current.CancellationToken)).ShouldBeFalse();
        Allowed(await handler.HandleAsync("fr34-confirm-destroyed",Wire(new { reference }),caller,state,TestContext.Current.CancellationToken)).ShouldBeFalse();
        erased=true;
        Allowed(await handler.HandleAsync("fr34-confirm-destroyed",Wire(new { reference }),caller,state,TestContext.Current.CancellationToken)).ShouldBeTrue();
        missingNative=true;
        (await handler.HandleAsync("fr34-read-persisted",Wire(new { reference }),caller,state,TestContext.Current.CancellationToken)).ShouldBeNull();
        await Should.ThrowAsync<InvalidOperationException>(()=>handler.HandleAsync("fr34-read-persisted",Wire(new { reference }),caller with { ScopeReference="tenant-b|fr34" },state,TestContext.Current.CancellationToken));
        gateway.ReceivedCalls().ShouldAllBe(call=>call.GetMethodInfo().Name==nameof(IEventStoreGatewayClient.ReadStreamAsync));
    }
}
