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
    [Theory]
    [InlineData("input-retention")][InlineData("seal-response")]
    public async Task ActualSafeCanaryInputAndSealResponseLossRecoverOneSubmissionWithoutRetainingPlaintext(string loss)
    {
        using var fixture=new Fixture();var token=TestContext.Current.CancellationToken;string canary=new string('4',64);byte[] plaintext="fixture-exact-canary-retention-loss"u8.ToArray();
        fixture.EngineAuthority.DropNextResponseForMethod=loss=="input-retention"?"qualification-begin-original":"fr34-seal-persist";
        var first=loss=="input-retention"?await fixture.CanaryEngine.SealAndPersistAsync(fixture.Target,canary,plaintext,token):await fixture.RemoteEngine.SealAndPersistAsync(fixture.Target,canary,plaintext,token);
        first.ShouldBeNull();fixture.AssertNoPlaintextArchives(plaintext);
        (await fixture.CanaryEngine.SealAndPersistAsync(fixture.Target,canary,"changed-canary"u8.ToArray(),token)).ShouldBeNull();
        var recovered=await fixture.CanaryEngine.SealAndPersistAsync(fixture.Target,canary,plaintext,token);recovered.ShouldNotBeNull();fixture.Submits.ShouldBe(1);fixture.AssertNoPlaintextArchives(plaintext);
        (await fixture.CanaryEngine.DestroyAsync(recovered,token)).ShouldBeTrue();int releases=fixture.Releases;
        (await fixture.CanaryEngine.ReplayAsync(recovered,token))!.PayloadBytes.ShouldBeNull();fixture.Releases.ShouldBe(releases);fixture.Submits.ShouldBe(1);fixture.AssertNoPlaintextArchives(plaintext);
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task ActualEngineAndReceivingArchivesContainNoReversibleCanaryBeforeOrAfterDestroy(bool loseDispatchReply)
    {
        using var fixture=new Fixture();string canary=new string('9',64);byte[] plaintext="fixture-plaintext-original-must-never-be-journaled"u8.ToArray();
        if(loseDispatchReply) fixture.EngineAuthority.DropNextResponseForMethod="recording-dispatch-admit";
        var reference=await fixture.CanaryEngine.SealAndPersistAsync(fixture.Target,canary,plaintext,TestContext.Current.CancellationToken);
        fixture.AssertNoPlaintextArchives(plaintext);
        if(loseDispatchReply)
        {
            reference.ShouldBeNull();fixture.Submits.ShouldBe(0);
            (await fixture.CanaryEngine.SealAndPersistAsync(fixture.Target,canary,plaintext,TestContext.Current.CancellationToken)).ShouldBeNull();fixture.Submits.ShouldBe(0);return;
        }
        reference.ShouldNotBeNull();fixture.Submits.ShouldBe(1);
        var opened=await fixture.RemoteEngine.UnsealAsync(reference,TestContext.Current.CancellationToken);opened.ShouldBe(plaintext);CryptographicOperations.ZeroMemory(opened!);
        (await fixture.CanaryEngine.DestroyAsync(reference,TestContext.Current.CancellationToken)).ShouldBeTrue();int releases=fixture.Releases;
        var erased=await fixture.CanaryEngine.ReplayAsync(reference,TestContext.Current.CancellationToken);erased.ShouldNotBeNull();erased.PayloadBytes.ShouldBeNull();
        fixture.Releases.ShouldBe(releases);fixture.Submits.ShouldBe(1);fixture.AssertNoPlaintextArchives(plaintext);
    }
    [Theory]
    [InlineData("tenant")][InlineData("domain")][InlineData("aggregate")][InlineData("type")][InlineData("payload")][InlineData("message")][InlineData("idempotency")][InlineData("extensions")][InlineData("correlation")]
    public async Task ActualFingerprintAuthorizerRejectsFullCommandSubstitutionBeforeAnyProtectedSubmission(string field)
    {
        using var fixture=new Fixture();var token=TestContext.Current.CancellationToken;string canary=new string('6',64);
        var command=await fixture.PrepareUnsentCommandAsync(canary,"fixture-original"u8.ToArray(),token);
        (await fixture.IssueProofAsync(command,token)).ShouldNotBeNull();
        var changed=field switch {
            "tenant"=>command with { Tenant="foreign" },"domain"=>command with { Domain="foreign" },"aggregate"=>command with { AggregateId=new string('5',64) },
            "type"=>command with { CommandType="Foreign.Command" },"payload"=>command with { Payload=JsonSerializer.SerializeToElement(new { changed=true }) },
            "message"=>command with { MessageId="01ARZ3NDEKTSV4RRFFQ69G5FAV" },"idempotency"=>command with { IdempotencyKey="changed" },
            "extensions"=>command with { Extensions=new(){{"changed","value"}} },_=>command with { CorrelationId="changed" } };
        (await fixture.IssueProofAsync(changed,token)).ShouldBeNull();fixture.Submits.ShouldBe(0);fixture.Releases.ShouldBe(0);
    }
    [Fact]
    public async Task ActualEngineExplicitlyRefusesAndPreservesUnsafeLegacyRawOriginal()
    {
        using var fixture=new Fixture();string canary=new string('8',64),id=EventStorePrivateRecordingPort.StableUlid("fr34","tenant",canary);
        var legacy=JsonSerializer.SerializeToElement(new { protectedContent=new { value=Convert.ToBase64String("legacy-canary"u8),target=new { tenantId="tenant",agentInteractionId=canary,targetProtectionKeyAlias="fr34-"+canary } } });
        // Pre-existing legacy body is preserved under its historical locator; new raw input admission is refused.
        fixture.EngineAuthority.State=fixture.EngineAuthority.State with { Originals=new(fixture.EngineAuthority.State.Originals){{"fr34-engine:"+id,new(legacy,null)}} };
        var unsafeNew=await fixture.EngineAuthority.Authority.InvokeAsync<JsonElement>("qualification-begin-original",new { role="fr34-engine",requestId=id+"-new",original=legacy },TestContext.Current.CancellationToken);unsafeNew.ValueKind.ShouldBe(JsonValueKind.Undefined);
        var before=await fixture.EngineAuthority.Authority.InvokeAsync<IndependentOriginal>("qualification-lookup-original",new { role="fr34-engine",requestId=id },TestContext.Current.CancellationToken);
        var exception=await Should.ThrowAsync<InvalidOperationException>(()=>fixture.CanaryEngine.SealAndPersistAsync(fixture.Target,canary,"legacy-canary"u8.ToArray(),TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("LegacyFr34RetainedPlaintextOriginalCleanupUnproven");fixture.Submits.ShouldBe(0);
        var after=await fixture.EngineAuthority.Authority.InvokeAsync<IndependentOriginal>("qualification-lookup-original",new { role="fr34-engine",requestId=id },TestContext.Current.CancellationToken);
        after!.Request.GetRawText().ShouldBe(before!.Request.GetRawText());
    }
    [Fact]
    public async Task ActualCanaryProofAcquisitionRefusesFifoBeforeNativeSubmit()
    {
        using var fixture=new Fixture();fixture.ReplaceProofWithFifo();
        (await fixture.CanaryEngine.SealAndPersistAsync(fixture.Target,new string('c',64),"fixture"u8.ToArray(),TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(3),TestContext.Current.CancellationToken)).ShouldBeNull();
        fixture.Submits.ShouldBe(0);fixture.DomainCalls.ShouldBe(0);fixture.Releases.ShouldBe(0);
    }
    [Theory]
    [InlineData("normal",true)][InlineData("lost-completion",true)]
    [InlineData("unknown-native",false)][InlineData("missing-witness",false)][InlineData("wrong-receipt",false)][InlineData("bad-signature",false)]
    [InlineData("outage",false)][InlineData("policy-change",false)][InlineData("trust-change",false)][InlineData("recipe-change",false)]
    [InlineData("changed-manifest",false)][InlineData("changed-target",false)][InlineData("claim-change",false)]
    public async Task CompleteGateUsesActualReceivingProofProducerDomainPersistedReaderNativeManifestAndReadOnlyErasure(string fault,bool expected)
    {
        using var fixture=new Fixture();fixture.Fault=fault;
        var gate=fixture.Gate();bool passed=await gate.EvaluateAsync(TestContext.Current.CancellationToken);
        await ((Task)typeof(Fr34ProtectionGate).GetProperty("CleanupCompletion",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(gate)!).WaitAsync(TestContext.Current.CancellationToken);
        passed.ShouldBe(expected,fixture.Diagnostics);
        fixture.Submits.ShouldBe(1);fixture.DomainCalls.ShouldBe(1);fixture.ProtectCalls.ShouldBe(1);
        if(expected) { fixture.NativeDestructions.ShouldBe(1);fixture.NativeErasureReads.ShouldBeGreaterThan(0); }
    }
    [Fact]
    public async Task LostUnsealAndReplayRecoveryNeverReleaseAnotherNativeUseEvenAfterCompletionAndErasure()
    {
        using var fixture=new Fixture();string canary=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));byte[] plain="fixture-only-canary"u8.ToArray();
        var reference=await fixture.CanaryEngine.SealAndPersistAsync(fixture.Target,canary,plain,TestContext.Current.CancellationToken);reference.ShouldNotBeNull();
        fixture.DropNextResponse("fr34-unseal");
        (await fixture.RemoteEngine.UnsealAsync(reference,TestContext.Current.CancellationToken)).ShouldBeNull();
        int releases=fixture.Releases;
        foreach(var value in await Task.WhenAll(Enumerable.Range(0,4).Select(_=>fixture.RemoteEngine.UnsealAsync(reference,TestContext.Current.CancellationToken)))) value.ShouldBeNull();
        fixture.Releases.ShouldBe(releases);
        fixture.DropNextResponse("fr34-replay");
        (await fixture.RemoteEngine.ReplayAsync(reference,TestContext.Current.CancellationToken)).ShouldBeNull();releases=fixture.Releases;
        (await fixture.CanaryEngine.DestroyAsync(reference,TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await fixture.RemoteEngine.ReplayAsync(reference,TestContext.Current.CancellationToken)).ShouldBeNull();fixture.Releases.ShouldBe(releases);
    }
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
    [Theory]
    [InlineData("fr34-unseal","none")][InlineData("fr34-replay","none")]
    [InlineData("fr34-unseal","revoke")][InlineData("fr34-replay","recipe")]
    [InlineData("fr34-read-persisted","target")][InlineData("fr34-read-persisted","policy")]
    [InlineData("fr34-unseal","expiry")][InlineData("fr34-unseal","journal")]
    [InlineData("fr34-unseal","signing-failure")][InlineData("fr34-unseal","cancel")][InlineData("fr34-unseal","transport-failure")][InlineData("fr34-replay","transport-failure")]
    public async Task ActualSensitiveReceivingResultsOrderSemanticPolicyAndClearTransferredServerArrays(string method,string mutation)
    {
        using var fixture=new Fixture();var token=TestContext.Current.CancellationToken;byte[] plaintext="fixture-server-owned-canary"u8.ToArray();
        var reference=await fixture.CanaryEngine.SealAndPersistAsync(fixture.Target,new string('d',64),plaintext,token);reference.ShouldNotBeNull();
        var capture=fixture.CaptureHandler();using var cancel=CancellationTokenSource.CreateLinkedTokenSource(token);
        bool read=method=="fr34-read-persisted";var authority=read?fixture.ObserverAuthority:fixture.EngineAuthority;
        var pending=authority.Authority.InvokeAsync<JsonElement>(method,new { reference },cancel.Token);
        await capture.Ready.Task.WaitAsync(TimeSpan.FromSeconds(3),token);int releases=fixture.Releases;
        if(mutation=="revoke") authority.RevokeWorkload("recording-owner");
        else if(mutation=="recipe") authority.State=authority.State with { Fr34RecipeSha256=new string('f',64) };
        else if(mutation=="target") authority.State=authority.State with { Fr34Target=fixture.Target with { CustodyTargetId="changed" } };
        else if(mutation=="policy") authority.State=authority.State with { Fr34AuthorizationRevision="changed-policy" };
        else if(mutation=="expiry") authority.State=authority.State with { Callers=authority.State.Callers.Select(value=>value with { ValidUntil=DateTimeOffset.UtcNow.AddSeconds(-1) }).ToArray() };
        else if(mutation=="journal") authority.State=authority.State with { Revision=authority.State.Revision+5,Originals=new(authority.State.Originals) { ["unrelated:journal"]=new(JsonSerializer.SerializeToElement(new { unrelated=true }),null) } };
        else if(mutation=="cancel") cancel.Cancel();
        else if(mutation=="transport-failure") authority.FailResponseTransport=true;
        else if(mutation=="signing-failure") authority.BreakSigner();
        capture.Release.TrySetResult();
        if(mutation=="cancel") await Should.ThrowAsync<OperationCanceledException>(()=>pending);
        else
        {
            var result=await pending;
            if(mutation is "none" or "journal")
            {
                result.ValueKind.ShouldNotBe(JsonValueKind.Undefined);
                if(method=="fr34-unseal") result.GetBytesFromBase64().ShouldBe(plaintext);
                if(method=="fr34-replay") result.Deserialize<PayloadUnprotectionOutcome>(CustodyBindingContract.Json)!.PayloadBytes.ShouldBe(capture.Copy);
            }
            else result.ValueKind.ShouldBe(JsonValueKind.Undefined);
        }
        await capture.Cleared.Task.WaitAsync(TimeSpan.FromSeconds(3),token);
        if(method is "fr34-unseal" or "fr34-replay") { capture.ServerBytes.ShouldNotBeNull();capture.ServerBytes.ShouldAllBe(value=>value==0); }
        fixture.Releases.ShouldBe(releases);
        if(method is "fr34-unseal" or "fr34-replay")
        {
            // An already admitted unknown cannot obtain another native material use on recovery.
            if(mutation is "none" or "journal") (await authority.Authority.InvokeAsync<JsonElement>(method,new { reference },token)).ValueKind.ShouldBe(JsonValueKind.Null);
            fixture.Releases.ShouldBe(releases);
        }
    }
    [Theory]
    [InlineData("command","outer")][InlineData("command","outer-expiry")][InlineData("command","nested-revoke")][InlineData("command","nested-expiry")][InlineData("command","nested-purpose")]
    [InlineData("command","target")][InlineData("command","policy")][InlineData("command","recipe")][InlineData("command","trust")][InlineData("command","journal")]
    [InlineData("occurrence","outer")][InlineData("occurrence","outer-expiry")][InlineData("occurrence","target")][InlineData("occurrence","policy")][InlineData("occurrence","recipe")][InlineData("occurrence","trust")][InlineData("occurrence","journal")]
    public async Task ActualCommandAndOccurrenceRepliesRereadExactCurrentAuthorityAfterAwaitedOriginalRead(string path,string mutation)
    {
        using var fixture=new Fixture();var token=TestContext.Current.CancellationToken;string canary=new string('3',64);
        var command=path=="command"?await fixture.PrepareUnsentCommandAsync(canary,"fixture-late-authority"u8.ToArray(),token):null;
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var authority=path=="command"?fixture._proofAuthority:fixture.Receiving;
        var originalReader=path=="command"?fixture._commands:fixture._claims;
        originalReader.BeforeRead=()=> { entered.TrySetResult();return release.Task; };
        Task<JsonElement> work=path=="command"
            ?authority.Authority.InvokeAsync<JsonElement>("fr34-command-authorize",new { caller="recording-owner",principal="private-worker",command },token)
            :authority.Authority.InvokeAsync<JsonElement>("fr34-resolve-occurrence",new { owner=new AggregateIdentity("tenant","fr34-canary",canary),occurrence=new PayloadProtectionOccurrenceContext(1,PayloadProtectionPayloadKind.Event,typeof(Fr34CanaryPersisted).FullName!) },token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3),token);
        if(mutation is "outer" or "outer-expiry") authority.State=authority.State with { Callers=authority.State.Callers.Select(value=>value.ScopeReference=="tenant|fr34"?value with { Revoked=mutation=="outer",ValidUntil=mutation=="outer-expiry"?DateTimeOffset.UtcNow.AddSeconds(-1):value.ValidUntil }:value).ToArray() };
        else if(mutation.StartsWith("nested-",StringComparison.Ordinal)) authority.State=authority.State with { Callers=authority.State.Callers.Select(value=>value.ScopeReference=="private-worker"?value with { Revoked=mutation=="nested-revoke",ValidUntil=mutation=="nested-expiry"?DateTimeOffset.UtcNow.AddSeconds(-1):value.ValidUntil,Methods=mutation=="nested-purpose"?["recording-source:foreign"]:value.Methods }:value).ToArray() };
        else if(mutation=="target") authority.State=authority.State with { Fr34Target=fixture.Target with { CustodyTargetId="changed" } };
        else if(mutation=="policy") authority.State=authority.State with { Fr34AuthorizationRevision="changed" };
        else if(mutation=="recipe") authority.State=authority.State with { Fr34RecipeSha256=new string('f',64) };
        else if(mutation=="trust") authority.State=authority.State with { TrustRevision="changed" };
        else authority.State=authority.State with { Revision=authority.State.Revision+7,Originals=new(authority.State.Originals){{"unrelated:journal",new(JsonSerializer.SerializeToElement(new { unrelated=true }),null)}} };
        release.TrySetResult();var result=await work;
        if(mutation=="journal") { result.ValueKind.ShouldNotBe(JsonValueKind.Undefined);if(path=="command") result.GetProperty("Allowed").GetBoolean().ShouldBeTrue(); }
        else result.ValueKind.ShouldBe(JsonValueKind.Undefined);
        fixture.Submits.ShouldBe(0);fixture.Releases.ShouldBe(0);
        if(path=="occurrence")
        {
            // Metadata admitted while a caller changed remains exact and recoverable.
            (await new InstalledQualificationOriginalStore(fixture._claims.Authority).LookupResultAsync("fr34-recipes","independently-enrolled-recipe:actual-root-slot",token)).ShouldNotBeNull();
            int count=fixture._claims.Methods.Count(value=>value=="qualification-begin-original");
            _=await authority.Authority.InvokeAsync<JsonElement>("fr34-resolve-occurrence",new { owner=new AggregateIdentity("tenant","fr34-canary",canary),occurrence=new PayloadProtectionOccurrenceContext(1,PayloadProtectionPayloadKind.Event,typeof(Fr34CanaryPersisted).FullName!) },token);
            fixture._claims.Methods.Count(value=>value=="qualification-begin-original").ShouldBe(count);fixture.Releases.ShouldBe(0);
        }
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task ActualCommandReplyRechecksNestedExpiryAfterAwaitedFinalConditionalAdmission(bool expire)
    {
        var clock=new ReplyClock(DateTimeOffset.UtcNow);using var fixture=new Fixture(clock);var token=TestContext.Current.CancellationToken;
        var command=await fixture.PrepareUnsentCommandAsync(new string('2',64),"fixture-post-cas-expiry"u8.ToArray(),token);
        var authority=fixture._proofAuthority;DateTimeOffset expiry=clock.GetUtcNow().Add(expire?TimeSpan.FromSeconds(1):TimeSpan.FromHours(1));
        authority.State=authority.State with { Callers=authority.State.Callers.Select(value=>value.ScopeReference=="private-worker"?value with { ValidUntil=expiry }:value).ToArray() };
        var original=await fixture._commands.Authority.InvokeAsync<IndependentOriginal>("qualification-lookup-original",new { role="fr34-engine",requestId=command.MessageId },token);
        original.ShouldNotBeNull();original.SubmissionState.ShouldBe("Unsent");bool saved=false;
        authority.BeforeConditionalSave=()=> { authority.BeforeConditionalSave=null;clock.Advance(TimeSpan.FromSeconds(2));saved=true; };
        var result=await authority.Authority.InvokeAsync<JsonElement>("fr34-command-authorize",new { caller="recording-owner",principal="private-worker",command },token);
        saved.ShouldBeTrue();authority.State.Callers.Single(value=>value.ScopeReference=="private-worker").ValidUntil.ShouldBe(expiry);
        if(expire) { result.ValueKind.ShouldBe(JsonValueKind.Undefined);authority.Methods.ShouldContain("status:403"); }
        else result.GetProperty("Allowed").GetBoolean().ShouldBeTrue();
        var retained=await fixture._commands.Authority.InvokeAsync<IndependentOriginal>("qualification-lookup-original",new { role="fr34-engine",requestId=command.MessageId },token);
        retained!.Request.GetRawText().ShouldBe(original.Request.GetRawText());retained.SubmissionState.ShouldBe("Unsent");
        fixture.Submits.ShouldBe(0);fixture.Releases.ShouldBe(0);fixture._commands.Methods.Count(value=>value=="qualification-begin-original").ShouldBe(1);
    }
    private sealed class ReplyClock(DateTimeOffset now):TimeProvider
    {
        private DateTimeOffset _now=now;
        public override DateTimeOffset GetUtcNow()=>_now;
        internal void Advance(TimeSpan duration)=>_now+=duration;
    }
    private sealed class CapturingHandler(IIndependentOwnerOperationHandler inner):IIndependentOwnerOperationHandler
    {
        internal readonly TaskCompletionSource Ready=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously),Cleared=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal byte[]? ServerBytes,Copy;
        public IReadOnlySet<string> Methods=>inner.Methods;
        public bool IsReadOnly(string method)=>inner.IsReadOnly(method);
        public bool RequiresCurrentResultAdmission(string method)=>inner.RequiresCurrentResultAdmission(method);
        public string? ResultPolicy(string method,JsonElement payload,IndependentAuthorityState current)=>inner.ResultPolicy(method,payload,current);
        public async Task<object?> HandleAsync(string method,JsonElement payload,IndependentCallerGrant caller,IndependentAuthorityState current,CancellationToken token)
        {
            var result=await inner.HandleAsync(method,payload,caller,current,token);ServerBytes=result as byte[]??(result as PayloadUnprotectionOutcome)?.PayloadBytes;Copy=ServerBytes?.ToArray();Ready.TrySetResult();await Release.Task;return result;
        }
        public Task<object?> RecoverAsync(string method,JsonElement payload,IndependentCallerGrant caller,IndependentAuthorityState current,CancellationToken token)=>inner.RecoverAsync(method,payload,caller,current,token);
        public void ReleaseResult(string method,object? result) { inner.ReleaseResult(method,result);Cleared.TrySetResult(); }
    }
    private sealed class Fixture:IDisposable
    {
        internal readonly ReceivingAuthorityFixture _claims=new("fr34-recipes"),_reads=new("occurrence-read-use"),_commands;
        private readonly List<IIndependentOwnerOperationHandler> _engineHandlers=[];private readonly ReceivingAuthorityFixture _gateObserver;
        private readonly Fr34ManifestCustodyFixture _manifest;internal string Fault { set=>_manifest.SetFault(value); }
        internal int NativeDestructions=>_manifest.Destroys;internal int NativeErasureReads=>_manifest.Reads;
        internal async Task<SubmitCommandRequest> PrepareUnsentCommandAsync(string canary,byte[] plaintext,CancellationToken token)
        {
            var claim=await Enrollment.ResolveAsync(new("tenant","fr34-canary",canary),new(1,PayloadProtectionPayloadKind.Event,typeof(Fr34CanaryPersisted).FullName!),token);claim.ShouldNotBeNull();
            string id=EventStorePrivateRecordingPort.StableUlid("fr34","tenant",canary);
            var payload=new PersistFr34Canary(new(Convert.ToBase64String(plaintext),claim.Binding.Identity.Target));
            var command=new SubmitCommandRequest(id,"tenant","fr34-canary",canary,typeof(PersistFr34Canary).FullName!,JsonSerializer.SerializeToElement(payload,new JsonSerializerOptions(JsonSerializerDefaults.Web)),IdempotencyKey:id);
            await _commands.Authority.InvokeAsync<JsonElement>("qualification-begin-original",new { role="fr34-engine",requestId=id,original=Fr34CommandFingerprint.Create(command,"private-worker") },token);return command;
        }
        internal ValueTask<string?> IssueProofAsync(SubmitCommandRequest command,CancellationToken token)=>_proofs.IssueAsync(new(command,"private-worker"),token);
        internal void AssertNoPlaintextArchives(byte[] plaintext)
        {
            foreach(var authority in new[]{_claims,_reads,_commands,_gateObserver,_proofAuthority,Receiving})
            foreach(string path in Directory.GetFiles(authority.ArchiveDirectory,"*",SearchOption.AllDirectories))
            {
                byte[] bytes=File.ReadAllBytes(path);bytes.AsSpan().IndexOf(plaintext).ShouldBe(-1,path);
                Encoding.UTF8.GetString(bytes).Contains(Convert.ToBase64String(plaintext),StringComparison.Ordinal).ShouldBeFalse(path);
            }
        }
        internal string Diagnostics=>"engine="+string.Join(',',_commands.Methods)+" observer="+string.Join(',',_gateObserver.Methods)+" enrollment="+string.Join(',',Receiving.Methods)+" custody="+string.Join(',',_manifest.Operations);
        internal readonly ReceivingAuthorityFixture Receiving;internal readonly DaprFr34OccurrenceEnrollment Enrollment;
        private readonly DaprClient _dapr=Substitute.For<DaprClient>();private readonly ICustodyOccurrenceMaterialClient _native=Substitute.For<ICustodyOccurrenceMaterialClient>();
        private readonly IInteractionOccurrenceRegistry _registry=Substitute.For<IInteractionOccurrenceRegistry>();private readonly IPlatformKeyInventoryActor _inventory=Substitute.For<IPlatformKeyInventoryActor>();private readonly IPlatformKeyInventoryAuthority _inventoryAuthority=Substitute.For<IPlatformKeyInventoryAuthority>();
        private readonly IEventStoreGatewayClient _gateway=Substitute.For<IEventStoreGatewayClient>();private readonly IEventStoreGatewayBoundCommandProofSource _proofs;
        private readonly RSA _proofKey=RSA.Create(2048);private readonly string _proofFile=Path.GetTempFileName();internal readonly ReceivingAuthorityFixture _proofAuthority;
        private Fr34ProductionPorts _production=null!;internal Fr34EventStoreCanaryReader Reader=null!;internal EventStoreFr34CanaryEngine CanaryEngine=null!;internal Fr34ProtectionTarget Target;
        internal readonly CustodyKeyRegistration Registration=new(new("tenant","installed-original-interaction",PlatformKeyPurpose.InteractionRootDek,"installed-original-alias","root-v1","lifecycle","store","contract"),"original-wrap",1,"kek-v1","original-opaque-root","original-wrap-receipt");
        private readonly Dictionary<InteractionOccurrenceIdentity,InteractionOccurrenceRecord> _records=[];private StreamReadEvent? _event;private AggregateIdentity? _owner;
        internal InteractionProtectionBindingProvider Bindings=>_production.Bindings;
        internal int Releases,Submits,DomainCalls,ProtectCalls;internal bool AllowCompletion=true;
        internal Fixture(TimeProvider? proofClock=null)
        {
            proofClock??=TimeProvider.System;
            _commands=new("fr34-engine",handlers:_engineHandlers);_gateObserver=new("fr34-observer",handlers:_engineHandlers);
            _manifest=new(new(Registration.Identity.TenantId,Registration.Identity.ObjectId,Registration.Identity.KeyAlias));
            var emptyEngine=new InteractionPayloadProtectionService(_registry,Substitute.For<IInteractionOccurrenceKeyProvider>(),Substitute.For<IInteractionProtectionBindingProvider>(),TimeProvider.System).GetIdentity();Target=new(emptyEngine.Name,emptyEngine.SourceRevision,"custody","tenant");
            CustodyInteractionMaterialProfile Profile(string use)=>new(Registration,new(use,"delivery-"+use,"actual-process","actual-transport",Registration.OpaqueWrappedKeyReference),new("digest","key","1","value",new("tenant",PlatformKeyPurpose.DigestKey,"digest","digest-v1","native","authority",null,null)));
            var recipe=new Fr34OccurrenceRecipe("independently-enrolled-recipe",Target,typeof(Fr34CanaryPersisted).FullName!,"candidate-hkdf-sha256-v1","digest-v1",1,[new("actual-root-slot","01ARZ3NDEKTSV4RRFFQ69G5FAV",Profile("write"),[Profile("read-1"),Profile("read-2"),Profile("read-3")],_manifest.Binding)]);
            var producer=new Fr34OccurrenceProducer(recipe,new InstalledQualificationOriginalStore(_claims.Authority),new InstalledQualificationOriginalStore(_reads.Authority),_native,_manifest.Client,_manifest.Locators);
            DateTimeOffset validity=DateTimeOffset.UtcNow.AddHours(1);string recipeDigest=QualificationProtocolSchema.Digest(JsonSerializer.SerializeToNode(recipe,CustodyBindingContract.Json)!);
            _commands.State=_commands.State with { Fr34Target=Target,Fr34AuthorizationRevision="common-policy-1",Fr34RecipeSha256=recipeDigest,ValidUntil=validity,Callers=_commands.State.Callers.Select(value=>value with { ScopeReference="tenant|fr34",Methods=value.Methods.Concat(["fr34-seal-persist","fr34-unseal","fr34-destroy","fr34-replay"]).ToArray() }).ToArray() };
            var observationCaller=_commands.State.Callers[0] with { BearerSha256=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("fixture-observer"))),Role="fr34-observer",Methods=["qualification-lookup-original","qualification-read-original:fr34-engine","qualification-begin-original","qualification-retain-result"] };
            _commands.State=_commands.State with { Callers=_commands.State.Callers.Append(observationCaller).ToArray() };
            _gateObserver.State=_gateObserver.State with { Revision=900,Fr34Target=Target,Fr34AuthorizationRevision="common-policy-1",Fr34RecipeSha256=recipeDigest,ValidUntil=validity,Callers=_gateObserver.State.Callers.Select(value=>value with { ScopeReference="tenant|fr34",Methods=["fr34-observe-current","fr34-verify-ownership","fr34-verify-carrier","fr34-read-persisted","fr34-confirm-destroyed"] }).ToArray() };
            _proofAuthority=new("fr34-engine",handlers:[new Fr34CommandAuthorizationHandler(recipe,producer,_commands.Authority,proofClock)],clock:proofClock);
            var proofCaller=_proofAuthority.State.Callers[0];
            _proofAuthority.State=_proofAuthority.State with { Fr34Target=Target,Fr34AuthorizationRevision="common-policy-1",Fr34RecipeSha256=QualificationProtocolSchema.Digest(JsonSerializer.SerializeToNode(recipe,CustodyBindingContract.Json)!),Callers=[proofCaller with { ScopeReference="tenant|fr34" },proofCaller with { BearerSha256=new string('f',64) }] };
            File.WriteAllText(_proofFile,Convert.ToBase64String(_proofKey.ExportPkcs8PrivateKey()));
            _commands.State=_commands.State with { Callers=_commands.State.Callers.Select(value=>value with { CommandProofPublicKeySpkiBase64=Convert.ToBase64String(_proofKey.ExportSubjectPublicKeyInfo()) }).ToArray() };
            _commands.State=_commands.State with { Callers=_commands.State.Callers.Append(_commands.State.Callers[0] with { BearerSha256=new string('e',64),ScopeReference="private-worker",Role="workload-source",Methods=["recording-source:fr34-canary"] }).ToArray() };
            _proofs=new Fr34CanaryCommandProofSource("recording-owner","private-worker",_proofFile,_proofAuthority.Authority,TimeProvider.System);
            Receiving=new("fr34-recipes",handlers:[new Fr34OccurrenceOperationHandler(recipe,producer,TimeProvider.System)]);
            Receiving.State=Receiving.State with { Fr34Target=Target,Fr34AuthorizationRevision="common-policy-1",Fr34RecipeSha256=QualificationProtocolSchema.Digest(JsonSerializer.SerializeToNode(recipe,CustodyBindingContract.Json)!),Callers=Receiving.State.Callers.Select(value=>value with { ScopeReference="tenant|fr34",Methods=["fr34-resolve-occurrence","fr34-occurrence-lease","fr34-material-use","fr34-material-release","fr34-pending-material","fr34-read-erasure"] }).ToArray() };
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
            _gateway.SubmitBoundWorkloadCommandAsync(Arg.Any<BoundWorkloadCommandRequest>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(async call=>
            {
                BoundWorkloadCommandProof.Verify(call.Arg<BoundWorkloadCommandRequest>(),call.Arg<string>(),"recording-owner",Convert.ToBase64String(_proofKey.ExportSubjectPublicKeyInfo()),DateTimeOffset.UtcNow).ShouldBeTrue();
                Submits++;var command=call.Arg<BoundWorkloadCommandRequest>().Command;var payload=command.Payload.Deserialize<PersistFr34Canary>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
                DomainCalls++;var eventPayload=(Fr34CanaryPersisted)Fr34CanaryAggregate.Handle(payload,null).Events.Single();_owner=new(command.Tenant,command.Domain,command.AggregateId);
                ProtectCalls++;var resolved=await _production.Bindings.ResolveAsync(_owner,new(1,PayloadProtectionPayloadKind.Event,typeof(Fr34CanaryPersisted).FullName!),payload.ProtectedContent.Target,true,call.Arg<CancellationToken>());resolved.ShouldNotBeNull();
                using(var digest=await _production.Keys.ResolveDigestAsync(resolved.Identity,resolved.DigestKeyVersion,call.Arg<CancellationToken>())) digest.ShouldNotBeNull();
                var write=await _production.Engine.ProtectEventPayloadAsync(_owner,eventPayload,typeof(Fr34CanaryPersisted).FullName!,JsonSerializer.SerializeToUtf8Bytes(eventPayload),"json",new(1,PayloadProtectionPayloadKind.Event,typeof(Fr34CanaryPersisted).FullName!),call.Arg<CancellationToken>());
                await _production.Engine.AcquirePayloadProtectionCompletionLeaseAsync(write.CompletionContext!,call.Arg<CancellationToken>());
                _event=new(1,typeof(Fr34CanaryPersisted).FullName!,write.ProtectionResult.PayloadBytes,write.ProtectionResult.SerializationFormat,1,command.MessageId,null,null,DateTimeOffset.UtcNow,"fixture-writer",write.ProtectionResult.Metadata) { NativeEventDigest=new string('b',64) };
                await _production.Engine.CompletePayloadProtectionAsync(write.CompletionContext!,PayloadProtectionPersistenceOutcome.Persisted,call.Arg<CancellationToken>());
                if(_manifest.Fault=="policy-change") _gateObserver.State=_gateObserver.State with { Fr34AuthorizationRevision="common-policy-2" };
                if(_manifest.Fault=="trust-change") _gateObserver.State=_gateObserver.State with { TrustRevision="revoked-trust-2" };
                if(_manifest.Fault=="recipe-change") _gateObserver.State=_gateObserver.State with { Fr34RecipeSha256=new string('f',64) };
                if(_manifest.Fault=="claim-change") Receiving.State=Receiving.State with { Fr34RecipeSha256=new string('f',64) };
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
        [System.Runtime.InteropServices.DllImport("libc",EntryPoint="mkfifo",SetLastError=true)] private static extern int Mkfifo(string path,int mode);
        internal void ReplaceProofWithFifo() { File.Delete(_proofFile);Mkfifo(_proofFile,0x180).ShouldBe(0); }
        internal void RestartProvider()
        {
            var source=new CustodyInteractionProtectionBindingSource(_dapr,new Dictionary<string,CustodyInteractionBindingResource>(),Enrollment);var binding=new InteractionProtectionBindingProvider(source);
            var keys=new CustodyInteractionOccurrenceKeyProvider(_dapr,_native,source,_registry,_inventory,_inventoryAuthority,new Dictionary<string,CustodyInteractionMaterialProfile>(),Enrollment);
            _production=new(new(_registry,keys,binding,TimeProvider.System),binding,keys,source,Enrollment);Reader=new(_gateway,TimeProvider.System,Enrollment);CanaryEngine=new(_production.Engine,Reader,_gateway,_proofs,_commands.Authority,"private-worker",_manifest.Client,_manifest.Locators,Enrollment);
            _engineHandlers.Clear();_engineHandlers.Add(new Fr34OwnerOperationHandler(CanaryEngine,Reader,binding,TimeProvider.System,new Fr34IndependentOriginalCollector(CanaryEngine,Reader,_commands.AuthorityForBearer("fixture-observer"))));
        }
        internal ReceivingAuthorityFixture EngineAuthority=>_commands;
        internal ReceivingAuthorityFixture ObserverAuthority=>_gateObserver;
        internal CapturingHandler CaptureHandler() { var handler=new CapturingHandler(_engineHandlers.Single());_engineHandlers.Clear();_engineHandlers.Add(handler);return handler; }
        internal DaprFr34ProtectionEngine RemoteEngine=>new(_commands.Authority);
        internal void DropNextResponse(string method)=>_commands.DropNextResponseForMethod=method;
        internal Fr34ProtectionGate Gate()=>new(Target,new DaprFr34ProtectionEngine(_commands.Authority),new DaprFr34PersistedReader(_gateObserver.Authority),new DaprFr34CanaryAuthority(_gateObserver.Authority),TimeProvider.System);
        public void Dispose() { Receiving.Dispose();_claims.Dispose();_reads.Dispose();_commands.Dispose();_gateObserver.Dispose();_manifest.Dispose();_proofAuthority.Dispose();_proofKey.Dispose();File.Delete(_proofFile);_dapr.Dispose(); }
    }
}
