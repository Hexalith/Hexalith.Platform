using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.PayloadProtection;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

// Actual receiving producer, production key/binding adapters, domain handler and native proof codec in a disposable fixture.
public sealed class Fr34DynamicOccurrenceTests
{
    [Fact]
    public async Task FreshChallengeUsesUnchangedNativeRootAndRealSealBoundSubmitDomainPersistReadRoundTripAndFreshCompletedReads()
    {
        using var fixture=new Fixture();string canary=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));byte[] plain=Encoding.UTF8.GetBytes("fixture-sensitive-plaintext");
        var result=await fixture.CanaryEngine.SealAndPersistAsync(fixture.Target,canary,plain,TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();fixture.Submits.ShouldBe(1);fixture.DomainCalls.ShouldBe(1);fixture.ProtectCalls.ShouldBe(1);
        var persisted=await fixture.Reader.ReadOriginalAsync(result,TestContext.Current.CancellationToken);persisted.ShouldNotBeNull();
        persisted.ProtectionTarget.ShouldBe(new("tenant","installed-original-interaction","installed-original-alias"));persisted.ProtectionTarget.AgentInteractionId.ShouldNotBe(canary);
        fixture.Registration.Identity.ObjectId.ShouldBe("installed-original-interaction");persisted.Owner.AggregateId.ShouldBe(canary);
        Encoding.UTF8.GetString(persisted.Protected.PayloadBytes).ShouldNotContain(Convert.ToBase64String(plain));
        var opened=await fixture.CanaryEngine.UnsealAsync(result,TestContext.Current.CancellationToken);opened.ShouldBe(plain);CryptographicOperations.ZeroMemory(opened!);
        // Owned native root buffers are disposed by the actual hierarchy. Completion remains independently checked by the fixture native port.
        fixture.AllowCompletion=false;
        (await fixture.CanaryEngine.UnsealAsync(result,TestContext.Current.CancellationToken)).ShouldBeNull();fixture.Releases.ShouldBe(2);
        fixture.AllowCompletion=true;
        fixture.RestartProvider();
        var again=await fixture.CanaryEngine.UnsealAsync(result,TestContext.Current.CancellationToken);again.ShouldBe(plain);CryptographicOperations.ZeroMemory(again!);fixture.Releases.ShouldBe(3);
        (await fixture.CanaryEngine.SealAndPersistAsync(fixture.Target,canary,plain,TestContext.Current.CancellationToken)).ShouldBe(result);fixture.Submits.ShouldBe(1);
        fixture.Receiving.State=fixture.Receiving.State with { Fr34RecipeSha256=new string('0',64) };
        (await fixture.Enrollment.ResolveAsync(persisted.Owner,persisted.Occurrence,TestContext.Current.CancellationToken)).ShouldBeNull();
    }
    [Fact]
    public async Task UnknownReleaseAfterRestartKeepsExactPendingUseAndNeverAdmitsAnotherReleaseUntilIndependentCompletion()
    {
        using var fixture=new Fixture();string canary=new string('a',64);var owner=new AggregateIdentity("tenant","fr34-canary",canary);var occurrence=new PayloadProtectionOccurrenceContext(1,PayloadProtectionPayloadKind.Event,typeof(Fr34CanaryPersisted).FullName!);
        var claim=await fixture.Enrollment.ResolveAsync(owner,occurrence,TestContext.Current.CancellationToken);claim.ShouldNotBeNull();
        var first=await fixture.Enrollment.MaterialAsync(claim.Binding.Identity,false,TestContext.Current.CancellationToken);first.ShouldNotBeNull();
        (await fixture.Enrollment.AdmitReleaseAsync(claim.Binding.Identity,first,TestContext.Current.CancellationToken)).ShouldBeTrue();
        fixture.AllowCompletion=false;fixture.RestartProvider();
        var pending=await fixture.Enrollment.MaterialAsync(claim.Binding.Identity,false,TestContext.Current.CancellationToken);pending.ShouldBe(first);
        (await fixture.Enrollment.AdmitReleaseAsync(claim.Binding.Identity,pending!,TestContext.Current.CancellationToken)).ShouldBeFalse();fixture.Releases.ShouldBe(0);
        fixture.AllowCompletion=true;
        var next=await fixture.Enrollment.MaterialAsync(claim.Binding.Identity,false,TestContext.Current.CancellationToken);next.ShouldNotBeNull();next.KeyUse.ShouldNotBe(first.KeyUse);
        (await fixture.Enrollment.AdmitReleaseAsync(claim.Binding.Identity,next,TestContext.Current.CancellationToken)).ShouldBeTrue();
        var changed=claim.Binding.Identity with { Target=new("tenant","foreign","installed-original-alias") };
        (await fixture.Enrollment.MaterialAsync(changed,false,TestContext.Current.CancellationToken)).ShouldBeNull();
        (await fixture.Enrollment.ResolveAsync(new("tenant","fr34-canary",new string('b',64)),occurrence,TestContext.Current.CancellationToken)).ShouldBeNull();
    }
    [Fact]
    public async Task LostClaimPublicationRecoversForeignExactMetadataBeforeAllocatingNextDistinctNativeSlot()
    {
        using var claims=new ReceivingAuthorityFixture("fr34-recipes");using var uses=new ReceivingAuthorityFixture("occurrence-read-use");var native=Substitute.For<ICustodyOccurrenceMaterialClient>();
        var target=new Fr34ProtectionTarget("engine",new string('a',40),"custody","tenant");
        Fr34OccurrenceSlot Slot(string id)
        {
            var registration=new CustodyKeyRegistration(new("tenant","native-object-"+id,PlatformKeyPurpose.InteractionRootDek,"native-alias-"+id,"v1","life","store","contract"),"wrap-"+id,1,"kek","opaque-"+id,"receipt-"+id);
            CustodyInteractionMaterialProfile Profile(string use)=>new(registration,new(id+use,id+"delivery"+use,"process","transport",registration.OpaqueWrappedKeyReference),new("digest","key","1","value",new("tenant",PlatformKeyPurpose.DigestKey,"digest","v1","native","authority",null,null)));
            native.LookupAsync(registration,Arg.Any<CancellationToken>()).Returns(new CustodyKeyLifecycleOutcome(registration.Identity,registration.OperationId,"digest",CustodyKeyLifecycleStatus.Wrapped,registration.WrapReceiptId));
            return new(id,EventStorePrivateRecordingPort.StableUlid("key",id),Profile("write"),[Profile("read")]);
        }
        var recipe=new Fr34OccurrenceRecipe("enrolled-parent",target,typeof(Fr34CanaryPersisted).FullName!,"candidate-hkdf-sha256-v1","v1",1,[Slot("one"),Slot("two")]);
        var store=new InstalledQualificationOriginalStore(claims.Authority);var occurrence=new PayloadProtectionOccurrenceContext(1,PayloadProtectionPayloadKind.Event,typeof(Fr34CanaryPersisted).FullName!);
        var oldOwner=new AggregateIdentity("tenant","fr34-canary",new string('a',64));
        await store.BeginAsync("fr34-recipes",recipe.RecipeId+":one",(System.Text.Json.Nodes.JsonObject)JsonSerializer.SerializeToNode(new { recipe.RecipeId,owner=oldOwner,occurrence },CustodyBindingContract.Json)!,TestContext.Current.CancellationToken);
        // Crash before Retain, then a different challenge arrives at the restarted producer.
        var producer=new Fr34OccurrenceProducer(recipe,store,new InstalledQualificationOriginalStore(uses.Authority),native);
        var newOwner=new AggregateIdentity("tenant","fr34-canary",new string('b',64));var next=await producer.ResolveAsync(newOwner,occurrence,TestContext.Current.CancellationToken);next.ShouldNotBeNull();next.SlotId.ShouldBe("two");
        var recovered=await producer.ResolveAsync(oldOwner,occurrence,TestContext.Current.CancellationToken);recovered.ShouldNotBeNull();recovered.SlotId.ShouldBe("one");recovered.Binding.Identity.Target.AgentInteractionId.ShouldBe("native-object-one");
        native.ReceivedCalls().ShouldAllBe(call=>call.GetMethodInfo().Name==nameof(ICustodyOccurrenceMaterialClient.LookupAsync));
    }
    [Fact]
    public async Task ExplicitExistingOccurrenceUsesFreshEnrolledReadAdmissionsWithoutChangingItsBindingOrNativeRegistration()
    {
        using var claims=new ReceivingAuthorityFixture("fr34-recipes");using var uses=new ReceivingAuthorityFixture("occurrence-read-use");var native=Substitute.For<ICustodyOccurrenceMaterialClient>();
        var registration=new CustodyKeyRegistration(new("tenant","original-interaction",PlatformKeyPurpose.InteractionRootDek,"original-alias","v1","life","store","contract"),"wrap",1,"kek","opaque","receipt");
        CustodyInteractionMaterialProfile Profile(string id)=>new(registration,new(id,"delivery-"+id,"process","transport","opaque"),new("digest","key","1","value",new("tenant",PlatformKeyPurpose.DigestKey,"digest","v1","native","authority",null,null)));
        var owner=new AggregateIdentity("tenant","interaction","existing-source");var occurrence=new PayloadProtectionOccurrenceContext(17,PayloadProtectionPayloadKind.Event,"Existing.Event");
        var identity=new InteractionOccurrenceIdentity(new("tenant","original-interaction","original-alias"),owner,occurrence.PayloadKind,occurrence.RecordSequence,occurrence.PayloadTypeId,"v1","candidate-hkdf-sha256-v1");
        var binding=new InteractionProtectionBinding(identity,"v1",1,"01ARZ3NDEKTSV4RRFFQ69G5FAV","01ARZ3NDEKTSV4RRFFQ69G5FAW",1);
        var recipe=new Fr34OccurrenceRecipe("explicit-occurrence-parent",new("engine",new string('a',40),"custody","tenant"),occurrence.PayloadTypeId,identity.DerivationProfileVersion,"v1",1,[new("original-slot",binding.KeyReference,Profile("write"),[Profile("read-one"),Profile("read-two")])],new Dictionary<string,InteractionProtectionBinding>{{CustodyInteractionProtectionBindingSource.ResourceId(owner,occurrence),binding}});
        native.LookupAsync(registration,Arg.Any<CancellationToken>()).Returns(new CustodyKeyLifecycleOutcome(registration.Identity,"wrap","digest",CustodyKeyLifecycleStatus.Wrapped,"receipt"));
        bool completed=false;native.CompleteAsync(registration,Arg.Any<string>(),Arg.Any<CustodyKeyUse>(),Arg.Any<CancellationToken>()).Returns(_=>new CustodyKeyUseResult(completed?"Completed":"Unknown",HoldersCompleted:completed));
        var producer=new Fr34OccurrenceProducer(recipe,new InstalledQualificationOriginalStore(claims.Authority),new InstalledQualificationOriginalStore(uses.Authority),native);
        (await producer.ResolveAsync(owner,occurrence,TestContext.Current.CancellationToken))!.Binding.ShouldBe(binding);
        var first=await producer.MaterialAsync(identity,false,TestContext.Current.CancellationToken);first.ShouldNotBeNull();
        (await producer.AdmitReleaseAsync(identity,first,TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await producer.MaterialAsync(identity,false,TestContext.Current.CancellationToken)).ShouldBe(first);
        (await producer.AdmitReleaseAsync(identity,first,TestContext.Current.CancellationToken)).ShouldBeFalse();
        completed=true;var next=await producer.MaterialAsync(identity,false,TestContext.Current.CancellationToken);next.ShouldNotBeNull();next.KeyUse.ShouldNotBe(first.KeyUse);
        (await producer.ResolveAsync(owner,occurrence,TestContext.Current.CancellationToken))!.Binding.ShouldBe(binding);
        (await producer.ResolveAsync(owner,new(18,occurrence.PayloadKind,occurrence.PayloadTypeId),TestContext.Current.CancellationToken)).ShouldBeNull();
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task ActualUnsealReceiverClearsItsRetainedOpenedArrayWhenObservationThrowsOrCancels(bool cancel)
    {
        using var fixture=new Fixture();var token=TestContext.Current.CancellationToken;
        var reference=await fixture.CanaryEngine.SealAndPersistAsync(fixture.Target,new string('a',64),"fixture-secret"u8.ToArray(),token);reference.ShouldNotBeNull();
        using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(token);byte[]? owned=null;
        var observer=Substitute.For<IFr34UnsealMeasurement>();
        observer.RecordUnsealAsync(reference,Arg.Any<ReadOnlyMemory<byte>>(),Arg.Any<CancellationToken>()).Returns((NSubstitute.Core.CallInfo call)=>
        {
            System.Runtime.InteropServices.MemoryMarshal.TryGetArray(call.Arg<ReadOnlyMemory<byte>>(),out var segment).ShouldBeTrue();owned=segment.Array;
            if(cancel) { cancellation.Cancel();throw new OperationCanceledException(cancellation.Token); }
            return Task.FromException<Fr34MeasuredCanaryOriginal?>(new IOException("isolated observation loss"));
        });
        var handler=new Fr34OwnerOperationHandler(fixture.CanaryEngine,fixture.Reader,fixture.Bindings,TimeProvider.System,observer);
        var current=fixture.Receiving.State;var caller=current.Callers[0] with { Role="fr34-engine" };
        if(cancel) await Should.ThrowAsync<OperationCanceledException>(()=>handler.HandleAsync("fr34-unseal",JsonSerializer.SerializeToElement(new { reference },CustodyBindingContract.Json),caller,current,cancellation.Token));
        else await Should.ThrowAsync<IOException>(()=>handler.HandleAsync("fr34-unseal",JsonSerializer.SerializeToElement(new { reference },CustodyBindingContract.Json),caller,current,cancellation.Token));
        owned.ShouldNotBeNull();owned.ShouldAllBe(value=>value==0);
    }
    private sealed class Fixture:IDisposable
    {
        private readonly ReceivingAuthorityFixture _claims=new("fr34-recipes"),_reads=new("occurrence-read-use"),_commands=new("fr34-engine");
        internal readonly ReceivingAuthorityFixture Receiving;internal readonly DaprFr34OccurrenceEnrollment Enrollment;
        private readonly DaprClient _dapr=Substitute.For<DaprClient>();private readonly ICustodyOccurrenceMaterialClient _native=Substitute.For<ICustodyOccurrenceMaterialClient>();
        private readonly IInteractionOccurrenceRegistry _registry=Substitute.For<IInteractionOccurrenceRegistry>();private readonly IPlatformKeyInventoryActor _inventory=Substitute.For<IPlatformKeyInventoryActor>();private readonly IPlatformKeyInventoryAuthority _inventoryAuthority=Substitute.For<IPlatformKeyInventoryAuthority>();
        private readonly IEventStoreGatewayClient _gateway=Substitute.For<IEventStoreGatewayClient>();private readonly IEventStoreGatewayBoundCommandProofSource _proofs=Substitute.For<IEventStoreGatewayBoundCommandProofSource>();
        private Fr34ProductionPorts _production=null!;internal Fr34EventStoreCanaryReader Reader=null!;internal EventStoreFr34CanaryEngine CanaryEngine=null!;internal Fr34ProtectionTarget Target;
        internal readonly CustodyKeyRegistration Registration=new(new("tenant","installed-original-interaction",PlatformKeyPurpose.InteractionRootDek,"installed-original-alias","root-v1","lifecycle","store","contract"),"original-wrap",1,"kek-v1","original-opaque-root","original-wrap-receipt");
        private readonly Dictionary<InteractionOccurrenceIdentity,InteractionOccurrenceRecord> _records=[];private StreamReadEvent? _event;private AggregateIdentity? _owner;
        internal InteractionProtectionBindingProvider Bindings=>_production.Bindings;
        internal int Releases,Submits,DomainCalls,ProtectCalls;internal bool AllowCompletion=true;
        internal Fixture()
        {
            var emptyEngine=new InteractionPayloadProtectionService(_registry,Substitute.For<IInteractionOccurrenceKeyProvider>(),Substitute.For<IInteractionProtectionBindingProvider>(),TimeProvider.System).GetIdentity();Target=new(emptyEngine.Name,emptyEngine.SourceRevision,"custody","tenant");
            CustodyInteractionMaterialProfile Profile(string use)=>new(Registration,new(use,"delivery-"+use,"actual-process","actual-transport",Registration.OpaqueWrappedKeyReference),new("digest","key","1","value",new("tenant",PlatformKeyPurpose.DigestKey,"digest","digest-v1","native","authority",null,null)));
            var recipe=new Fr34OccurrenceRecipe("independently-enrolled-recipe",Target,typeof(Fr34CanaryPersisted).FullName!,"candidate-hkdf-sha256-v1","digest-v1",1,[new("actual-root-slot","01ARZ3NDEKTSV4RRFFQ69G5FAV",Profile("write"),[Profile("read-1"),Profile("read-2"),Profile("read-3")])]);
            var producer=new Fr34OccurrenceProducer(recipe,new InstalledQualificationOriginalStore(_claims.Authority),new InstalledQualificationOriginalStore(_reads.Authority),_native);
            Receiving=new("fr34-recipes",handlers:[new Fr34OccurrenceOperationHandler(recipe,producer,TimeProvider.System)]);
            Receiving.State=Receiving.State with { Fr34Target=Target,Fr34RecipeSha256=QualificationProtocolSchema.Digest(JsonSerializer.SerializeToNode(recipe,CustodyBindingContract.Json)!),Callers=Receiving.State.Callers.Select(value=>value with { ScopeReference="tenant|fr34",Methods=["fr34-resolve-occurrence","fr34-occurrence-lease","fr34-material-use","fr34-material-release","fr34-pending-material"] }).ToArray() };
            Enrollment=new(Receiving.Authority);
            _native.LookupAsync(Registration,Arg.Any<CancellationToken>()).Returns(new CustodyKeyLifecycleOutcome(Registration.Identity,Registration.OperationId,"digest",CustodyKeyLifecycleStatus.Wrapped,Registration.WrapReceiptId));
            _native.UnwrapAsync(Registration,Arg.Any<string>(),Arg.Any<CustodyKeyUse>(),Arg.Any<CancellationToken>()).Returns(_=> { Releases++;return new CustodyKeyUseResult("Released",new(Enumerable.Repeat((byte)42,32).ToArray())); });
            _native.CompleteAsync(Registration,Arg.Any<string>(),Arg.Any<CustodyKeyUse>(),Arg.Any<CancellationToken>()).Returns(_=>new CustodyKeyUseResult(AllowCompletion?"Completed":"Unknown",HoldersCompleted:AllowCompletion));
            _inventoryAuthority.AuthorizeOperationAsync(Arg.Any<PlatformKeyVersion>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(true);_inventoryAuthority.ValidateStateAsync(Arg.Any<PlatformKeyVersion>(),Arg.Any<long>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(true);
            _inventory.ReadAsync(Arg.Any<PlatformKeyVersion>()).Returns(call=> { var key=call.Arg<PlatformKeyVersion>();return new PlatformKeyInventorySnapshot(key.TenantId,key.Purpose,key.KeyAlias,1,[new(key,PlatformHmacKeyState.Active,1)],[]); });
            _dapr.GetSecretAsync("digest","key",Arg.Any<IReadOnlyDictionary<string,string>>(),Arg.Any<CancellationToken>()).Returns(_=>new Dictionary<string,string>{{"value",Convert.ToBase64String(Enumerable.Repeat((byte)7,32).ToArray())}});
            _registry.ReserveAsync(Arg.Any<InteractionOccurrenceRequest>(),Arg.Any<CancellationToken>()).Returns(call=> { var request=call.Arg<InteractionOccurrenceRequest>();var record=new InteractionOccurrenceRecord(request,request.ProposedKeyReference,1,InteractionOccurrenceWriterState.Reserved,null,null);_records.TryAdd(request.Identity,record);return new InteractionOccurrenceReservationResult(InteractionOccurrenceReservationStatus.Reserved,_records[request.Identity]); });
            _registry.LookupAsync(Arg.Any<InteractionOccurrenceIdentity>(),Arg.Any<CancellationToken>()).Returns(call=> { var record=_records.GetValueOrDefault(call.Arg<InteractionOccurrenceIdentity>());return new InteractionOccurrenceReservationResult(record?.WriterState==InteractionOccurrenceWriterState.Active?InteractionOccurrenceReservationStatus.Sealed:InteractionOccurrenceReservationStatus.Reserved,record); });
            _registry.RetainSealedAsync(Arg.Any<InteractionOccurrenceIdentity>(),Arg.Any<InteractionOccurrenceSealedResult>(),Arg.Any<CancellationToken>()).Returns(call=> { var identity=call.Arg<InteractionOccurrenceIdentity>();_records[identity]=_records[identity] with { Sealed=call.Arg<InteractionOccurrenceSealedResult>() };return new InteractionOccurrenceReservationResult(InteractionOccurrenceReservationStatus.Sealed,_records[identity]); });
            _registry.CompleteWriterAsync(Arg.Any<InteractionOccurrenceIdentity>(),Arg.Any<string>(),Arg.Any<string>(),true,Arg.Any<CancellationToken>()).Returns(call=> { var identity=call.Arg<InteractionOccurrenceIdentity>();_records[identity]=_records[identity] with { WriterState=InteractionOccurrenceWriterState.Active,WriterProofId=call.ArgAt<string>(2) };return new InteractionOccurrenceReservationResult(InteractionOccurrenceReservationStatus.Sealed,_records[identity]); });
            _proofs.IssueAsync(Arg.Any<BoundWorkloadCommandRequest>(),Arg.Any<CancellationToken>()).Returns(_=>ValueTask.FromResult<string?>("fixture-bound-proof"));
            _gateway.SubmitBoundWorkloadCommandAsync(Arg.Any<BoundWorkloadCommandRequest>(),"fixture-bound-proof",Arg.Any<CancellationToken>()).Returns(async call=>
            {
                Submits++;var command=call.Arg<BoundWorkloadCommandRequest>().Command;var payload=command.Payload.Deserialize<PersistFr34Canary>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
                DomainCalls++;var eventPayload=(Fr34CanaryPersisted)Fr34CanaryAggregate.Handle(payload,null).Events.Single();_owner=new(command.Tenant,command.Domain,command.AggregateId);
                ProtectCalls++;var resolved=await _production.Bindings.ResolveAsync(_owner,new(1,PayloadProtectionPayloadKind.Event,typeof(Fr34CanaryPersisted).FullName!),payload.ProtectedContent.Target,true,call.Arg<CancellationToken>());resolved.ShouldNotBeNull();
                using(var digest=await _production.Keys.ResolveDigestAsync(resolved.Identity,resolved.DigestKeyVersion,call.Arg<CancellationToken>())) digest.ShouldNotBeNull();
                var write=await _production.Engine.ProtectEventPayloadAsync(_owner,eventPayload,typeof(Fr34CanaryPersisted).FullName!,JsonSerializer.SerializeToUtf8Bytes(eventPayload),"json",new(1,PayloadProtectionPayloadKind.Event,typeof(Fr34CanaryPersisted).FullName!),call.Arg<CancellationToken>());
                await _production.Engine.AcquirePayloadProtectionCompletionLeaseAsync(write.CompletionContext!,call.Arg<CancellationToken>());
                _event=new(1,typeof(Fr34CanaryPersisted).FullName!,write.ProtectionResult.PayloadBytes,write.ProtectionResult.SerializationFormat,1,command.MessageId,null,null,DateTimeOffset.UtcNow,"fixture-writer",write.ProtectionResult.Metadata) { NativeEventDigest=new string('b',64) };
                await _production.Engine.CompletePayloadProtectionAsync(write.CompletionContext!,PayloadProtectionPersistenceOutcome.Persisted,call.Arg<CancellationToken>());
                return new SubmitCommandResponse(command.MessageId,null);
            });
            _gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(),Arg.Any<CancellationToken>()).Returns(call=>
            {
                var read=call.Arg<StreamReadRequest>();var identity=new AggregateIdentity(read.Tenant,read.Domain,read.AggregateId!);var proof=NativeSourceProofCodec.CreateSeed(identity,"01ARZ3NDEKTSV4RRFFQ69G5FAW");
                if(_event is not null && _owner==identity) proof=NativeSourceProofCodec.ExtendDigest(proof,identity,1,_event.MessageId,_event.NativeEventDigest!);
                var events=_event is not null && _owner==identity && read.FromSequence==0 && read.ToSequence!=0?new[]{_event}:Array.Empty<StreamReadEvent>();
                return new StreamReadPage(identity.TenantId,identity.Domain,identity.AggregateId,events,new(read.FromSequence,read.ToSequence,events.Length==0?null:1,_event is null?0:1,events.Length,false,null) { NativeProof=proof });
            });
            RestartProvider();
        }
        internal void RestartProvider()
        {
            var source=new CustodyInteractionProtectionBindingSource(_dapr,new Dictionary<string,CustodyInteractionBindingResource>(),Enrollment);var binding=new InteractionProtectionBindingProvider(source);
            var keys=new CustodyInteractionOccurrenceKeyProvider(_dapr,_native,source,_registry,_inventory,_inventoryAuthority,new Dictionary<string,CustodyInteractionMaterialProfile>(),Enrollment);
            _production=new(new(_registry,keys,binding,TimeProvider.System),binding,keys,source,Enrollment);Reader=new(_gateway,TimeProvider.System,Enrollment);CanaryEngine=new(_production.Engine,Reader,_gateway,_proofs,_commands.Authority,"private-worker",null!,Substitute.For<ICustodyReservedManifestSource>(),Enrollment);
        }
        public void Dispose() { Receiving.Dispose();_claims.Dispose();_reads.Dispose();_commands.Dispose();_dapr.Dispose(); }
    }
}
