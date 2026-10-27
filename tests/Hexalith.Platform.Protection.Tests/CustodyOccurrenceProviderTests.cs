using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

public sealed class CustodyOccurrenceProviderTests
{
    [Theory]
    [InlineData("")][InlineData(" ")][InlineData("0")][InlineData("current")][InlineData("latest")][InlineData("alias")]
    [InlineData("+1")][InlineData("-1")][InlineData("01")][InlineData("1.0")][InlineData("1e0")][InlineData(" 1")][InlineData("1 ")]
    [InlineData("18446744073709551616")][InlineData("١")]
    public async Task ActualOccurrenceDigestProviderRejectsUnpinnedNativeSelectorWithoutReadingSecretOrReleasingRoot(string nativeVersion)
    {
        using var fixture=new Fixture { DigestNativeVersion=nativeVersion };
        (await fixture.Provider().ResolveDigestAsync(fixture.Identity,"digest-v1",TestContext.Current.CancellationToken)).ShouldBeNull();
        fixture.SecretReads.ShouldBe(0);fixture.MaterialReleases.ShouldBe(0);fixture.InventoryReads.ShouldBe(0);fixture.InventoryAuthorizations.ShouldBe(0);
        await fixture.Dapr.DidNotReceive().GetSecretAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<IReadOnlyDictionary<string,string>>(),Arg.Any<CancellationToken>());
    }
    [Theory]
    [InlineData("7",false)][InlineData("7",true)][InlineData("18446744073709551615",false)]
    public async Task ActualOccurrenceDigestProviderPinsNativeVersionAndRetainsLogicalVersionCurrentChecksAndOwnership(string nativeVersion,bool revokeAfterRead)
    {
        using var fixture=new Fixture { DigestNativeVersion=nativeVersion,RevokeDigestDuringSecretRead=revokeAfterRead };
        var digest=await fixture.Provider().ResolveDigestAsync(fixture.Identity,"digest-v1",TestContext.Current.CancellationToken);
        fixture.SecretReads.ShouldBe(1);fixture.Response!.ShouldBeEmpty();fixture.InventoryAuthorizations.ShouldBe(2);fixture.MaterialReleases.ShouldBe(0);
        if(revokeAfterRead) { digest.ShouldBeNull();fixture.InventoryReads.ShouldBe(1); }
        else { digest.ShouldNotBeNull();fixture.InventoryReads.ShouldBe(2);digest.Bytes.ShouldAllBe(value=>value==71);digest.Version.ShouldBe("digest-v1");digest.Dispose();digest.Bytes.ShouldAllBe(value=>value==0); }
        await fixture.Dapr.Received(1).GetSecretAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<IReadOnlyDictionary<string,string>>(),Arg.Any<CancellationToken>());
    }
    [Fact]
    public async Task ActualLeaseAndCurrentChecksReleaseNoMaterialAndOneRootBufferTransfersToDisposableOwner()
    {
        using var fixture=new Fixture();var provider=fixture.Provider();
        (await provider.ResolveRootAsync(fixture.Identity,"reference",true,TestContext.Current.CancellationToken)).ShouldBeNull();
        (await provider.AcquireEncryptionLeaseAsync(fixture.Occurrence,TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await provider.AcquireEncryptionLeaseAsync(fixture.Occurrence,TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await provider.IsCurrentAsync(fixture.Identity,"Write",TestContext.Current.CancellationToken)).ShouldBeTrue();fixture.MaterialReleases.ShouldBe(0);
        var root=await provider.ResolveRootAsync(fixture.Identity,"reference",true,TestContext.Current.CancellationToken);root.ShouldNotBeNull();
        ReferenceEquals(root.Bytes,fixture.Root).ShouldBeTrue();fixture.MaterialReleases.ShouldBe(1);
        Should.Throw<ObjectDisposedException>(()=>fixture.Released!.Bytes);
        (await provider.CompleteDisposedUsesAsync(TestContext.Current.CancellationToken)).ShouldBe(0);fixture.CompletionCalls.ShouldBe(0);
        root.Dispose();fixture.Root.ShouldAllBe(value=>value==0);
        (await provider.CompleteDisposedUsesAsync(TestContext.Current.CancellationToken)).ShouldBe(0);fixture.CompletionCalls.ShouldBe(1);
        fixture.IndependentCompletion=true;
        (await provider.CompleteDisposedUsesAsync(TestContext.Current.CancellationToken)).ShouldBe(1);fixture.CompletionCalls.ShouldBe(2);
        (await provider.ResolveRootAsync(fixture.Identity,"reference",true,TestContext.Current.CancellationToken)).ShouldBeNull();fixture.MaterialReleases.ShouldBe(1);
        (await provider.ResolveRootAsync(fixture.Identity,"substituted",true,TestContext.Current.CancellationToken)).ShouldBeNull();fixture.MaterialReleases.ShouldBe(1);
        fixture.Revoked=true;
        (await provider.IsCurrentAsync(fixture.Identity,"Write",TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await provider.ResolveRootAsync(fixture.Identity,"reference",true,TestContext.Current.CancellationToken)).ShouldBeNull();fixture.MaterialReleases.ShouldBe(1);
    }
    [Fact]
    public async Task DigestUsesOnlyExactCurrentTenantPurposeVersionAndClearsOwnedCandidateOnDispose()
    {
        using var fixture=new Fixture();var provider=fixture.Provider();
        (await provider.ResolveDigestAsync(fixture.Identity,"foreign-version",TestContext.Current.CancellationToken)).ShouldBeNull();fixture.SecretReads.ShouldBe(0);
        var digest=await provider.ResolveDigestAsync(fixture.Identity,"digest-v1",TestContext.Current.CancellationToken);digest.ShouldNotBeNull();
        fixture.SecretReads.ShouldBe(1);fixture.Response!.ShouldBeEmpty();digest.Bytes.ShouldAllBe(value=>value==71);digest.Dispose();digest.Bytes.ShouldAllBe(value=>value==0);
        fixture.InventoryRevoked=true;
        (await provider.ResolveDigestAsync(fixture.Identity,"digest-v1",TestContext.Current.CancellationToken)).ShouldBeNull();fixture.SecretReads.ShouldBe(1);
    }
    [Fact]
    public async Task RestoredMissingBindingCannotAcquireLeaseOrCreateGenesisAndCompletionIsDurablyOnceOnly()
    {
        using var fixture=new Fixture();fixture.Missing=true;
        (await fixture.Source.TryAcquireEncryptionLeaseAsync(fixture.Occurrence,fixture.Registry,TestContext.Current.CancellationToken)).ShouldBeFalse();
        await fixture.Authority.DidNotReceive().AdmitTransitionAsync(Arg.Any<AnchoredStateTransition>(),Arg.Any<CancellationToken>());
        fixture.Missing=false;
        (await fixture.Source.TryAcquireCompletionLeaseAsync(fixture.Record.Binding,TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await fixture.Source.TryAcquireCompletionLeaseAsync(fixture.Record.Binding,TestContext.Current.CancellationToken)).ShouldBeFalse();
        var restored=fixture.Record with { CompletionLeaseId=null,Revision=1 };fixture.Stored=restored;
        (await fixture.Source.TryAcquireCompletionLeaseAsync(restored.Binding,TestContext.Current.CancellationToken)).ShouldBeFalse();
    }
    [Fact]
    public async Task LostLeaseAnchorAcknowledgementAndRestoredBothLeasesReconcileExactOriginalsWithoutAnotherLease()
    {
        using var fixture=new Fixture();fixture.LoseAnchorAcknowledgement=true;
        await Should.ThrowAsync<IOException>(()=>fixture.Source.TryAcquireEncryptionLeaseAsync(fixture.Occurrence,fixture.Registry,TestContext.Current.CancellationToken));
        fixture.LoseAnchorAcknowledgement=false;
        (await fixture.Source.FindRetainedAsync(fixture.Identity.Owner,new(1,fixture.Identity.Kind,"event"),TestContext.Current.CancellationToken)).ShouldBe(fixture.Record.Binding);
        fixture.Stored.EncryptionLeaseId.ShouldNotBeNull();
        (await fixture.Source.TryAcquireEncryptionLeaseAsync(fixture.Occurrence,fixture.Registry,TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await fixture.Source.TryAcquireCompletionLeaseAsync(fixture.Record.Binding,TestContext.Current.CancellationToken)).ShouldBeTrue();
        var exact=fixture.Stored;fixture.Stored=fixture.Record;
        (await fixture.Source.FindRetainedAsync(fixture.Identity.Owner,new(1,fixture.Identity.Kind,"event"),TestContext.Current.CancellationToken)).ShouldBe(fixture.Record.Binding);
        fixture.Stored.ShouldBe(exact);
        (await fixture.Source.TryAcquireCompletionLeaseAsync(fixture.Record.Binding,TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await fixture.Source.TryAcquireEncryptionLeaseAsync(fixture.Occurrence,fixture.Registry,TestContext.Current.CancellationToken)).ShouldBeFalse();
    }
    internal sealed class Fixture:IDisposable
    {
        internal readonly DaprClient Dapr=Substitute.For<DaprClient>();internal readonly ICustodyOwnerStateAuthority Authority=Substitute.For<ICustodyOwnerStateAuthority>();
        internal readonly IInteractionOccurrenceRegistry Registry=Substitute.For<IInteractionOccurrenceRegistry>();private readonly ICustodyOccurrenceMaterialClient _material=Substitute.For<ICustodyOccurrenceMaterialClient>();
        private readonly IPlatformKeyInventoryActor _inventory=Substitute.For<IPlatformKeyInventoryActor>();private readonly IPlatformKeyInventoryAuthority _inventoryAuthority=Substitute.For<IPlatformKeyInventoryAuthority>();
        internal readonly InteractionOccurrenceIdentity Identity=new(new("tenant","interaction","alias"),new AggregateIdentity("tenant","domain","aggregate"),PayloadProtectionPayloadKind.Event,1,"event","root-v1","profile-v1");
        internal CustodyInteractionBindingRecord Record,Stored;internal readonly CustodyInteractionProtectionBindingSource Source;internal readonly InteractionOccurrenceRecord Occurrence;
        internal byte[] Root=Enumerable.Repeat((byte)42,32).ToArray();internal CustodyReleasedKey? Released;internal int MaterialReleases,SecretReads,CompletionCalls,InventoryReads,InventoryAuthorizations;internal bool IndependentCompletion,LoseAnchorAcknowledgement;internal Dictionary<string,string>? Response;internal bool Revoked,Missing,InventoryRevoked,RevokeDigestDuringSecretRead;internal string DigestNativeVersion="7";
        private readonly Dictionary<string,AnchoredStateTransition> _pending=[],_admitted=[],_recorded=[];
        private readonly CustodyKeyRegistration _registration=new(new("tenant","interaction",PlatformKeyPurpose.InteractionRootDek,"alias","root-v1","lifecycle","store","contract"),"wrap",1,"kek-v1","opaque","wrap-receipt");private long _anchoredRevision=1;private string _anchor;
        internal Fixture()
        {
            Record=Stored=new(1,new(Identity,"digest-v1",1,"reference","source-operation",1));_anchor=Hash(Record);
            Occurrence=new(new(Identity,"digest-v1","opaque-hmac",1,"reference"),"reference",1,InteractionOccurrenceWriterState.Reserved,null,null);
            Registry.LookupAsync(Identity,Arg.Any<CancellationToken>()).Returns(new InteractionOccurrenceReservationResult(InteractionOccurrenceReservationStatus.Reserved,Occurrence));
            Dapr.GetStateAndETagAsync<CustodyInteractionBindingRecord>("independent","binding",Arg.Any<ConsistencyMode?>(),Arg.Any<IReadOnlyDictionary<string,string>>(),Arg.Any<CancellationToken>()).Returns(_=>Task.FromResult((Missing?null!:Stored,"etag")));
            Dapr.TrySaveStateAsync("independent","binding",Arg.Any<CustodyInteractionBindingRecord>(),"etag",Arg.Any<StateOptions>(),Arg.Any<IReadOnlyDictionary<string,string>>(),Arg.Any<CancellationToken>()).Returns(call=> { Stored=call.ArgAt<CustodyInteractionBindingRecord>(2);return true; });
            Dapr.GetStateAsync<AnchoredStateTransition>("independent",Arg.Any<string>(),Arg.Any<ConsistencyMode?>(),Arg.Any<IReadOnlyDictionary<string,string>>(),Arg.Any<CancellationToken>()).Returns(call=>Task.FromResult(_pending.GetValueOrDefault(call.ArgAt<string>(1))!));
            Dapr.GetStateAndETagAsync<AnchoredStateTransition>("independent",Arg.Any<string>(),Arg.Any<ConsistencyMode?>(),Arg.Any<IReadOnlyDictionary<string,string>>(),Arg.Any<CancellationToken>()).Returns(call=>Task.FromResult((_pending.GetValueOrDefault(call.ArgAt<string>(1))!,"pending-etag")));
            Dapr.TrySaveStateAsync("independent",Arg.Any<string>(),Arg.Any<AnchoredStateTransition>(),"pending-etag",Arg.Any<StateOptions>(),Arg.Any<IReadOnlyDictionary<string,string>>(),Arg.Any<CancellationToken>()).Returns(call=> { var key=call.ArgAt<string>(1);var value=call.ArgAt<AnchoredStateTransition>(2);if(_pending.TryGetValue(key,out var prior)) return JsonSerializer.Serialize(prior)==JsonSerializer.Serialize(value);_pending[key]=value;return true; });
            Authority.AuthorizeOperationAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(_=>!Revoked);
            Authority.ValidateStateAsync(Arg.Any<long>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(call=>!Revoked && call.ArgAt<long>(0)==_anchoredRevision && call.ArgAt<string>(1)==_anchor);
            Authority.AdmitTransitionAsync(Arg.Any<AnchoredStateTransition>(),Arg.Any<CancellationToken>()).Returns(call=> { var value=call.Arg<AnchoredStateTransition>();bool allowed=value.ExpectedRevision==_anchoredRevision && value.PredecessorDigest==_anchor;if(allowed) _admitted[value.TargetDigest]=value;return allowed; });
            Authority.RecordTransitionAsync(Arg.Any<AnchoredStateTransition>(),Arg.Any<CancellationToken>()).Returns(call=> { var next=call.Arg<AnchoredStateTransition>();_anchoredRevision=next.TargetRevision;_anchor=next.TargetDigest;_recorded[next.TargetDigest]=next;if(LoseAnchorAcknowledgement) throw new IOException("fixture lost anchor result");return true; });
            Authority.VerifyTransitionAsync(Arg.Any<AnchoredStateTransition>(),Arg.Any<CancellationToken>()).Returns(call=>_recorded.TryGetValue(call.Arg<AnchoredStateTransition>().TargetDigest,out var old) && JsonSerializer.Serialize(old)==JsonSerializer.Serialize(call.Arg<AnchoredStateTransition>()));
            Authority.RecoverTransitionAsync(Arg.Any<AnchoredStateTransition>(),Arg.Any<CancellationToken>()).Returns(call=> { var value=call.Arg<AnchoredStateTransition>();if(!_admitted.TryGetValue(value.TargetDigest,out var admitted) || JsonSerializer.Serialize(admitted)!=JsonSerializer.Serialize(value)) return false;_recorded[value.TargetDigest]=value;if(value.TargetRevision>=_anchoredRevision) { _anchoredRevision=value.TargetRevision;_anchor=value.TargetDigest; }return true; });
            Source=new(Dapr,new Dictionary<string,CustodyInteractionBindingResource>{{CustodyInteractionProtectionBindingSource.ResourceId(Identity.Owner,new(1,Identity.Kind,"event")),new("independent","binding","outside-scope",Authority)}});
            _material.LookupAsync(_registration,Arg.Any<CancellationToken>()).Returns(new CustodyKeyLifecycleOutcome(_registration.Identity,"wrap","digest",CustodyKeyLifecycleStatus.Wrapped,"wrap-receipt"));
            _material.UnwrapAsync(_registration,Arg.Any<string>(),Arg.Any<CustodyKeyUse>(),Arg.Any<CancellationToken>()).Returns(_=> { MaterialReleases++;return MaterialReleases==1?new CustodyKeyUseResult("Released",Released=new(Root)):new CustodyKeyUseResult("Unknown"); });
            _material.CompleteAsync(_registration,Arg.Any<string>(),Arg.Any<CustodyKeyUse>(),Arg.Any<CancellationToken>()).Returns(call=> { call.ArgAt<string>(1).ShouldBe("holder-completion:"+CustodyMaterialUseIdentity.Digest(call.ArgAt<CustodyKeyUse>(2)));CompletionCalls++;return new CustodyKeyUseResult(IndependentCompletion?"Closed":"Unknown",HoldersCompleted:IndependentCompletion); });
            _inventoryAuthority.AuthorizeOperationAsync(Arg.Any<PlatformKeyVersion>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(_=> { InventoryAuthorizations++;return !InventoryRevoked; });
            _inventoryAuthority.ValidateStateAsync(Arg.Any<PlatformKeyVersion>(),Arg.Any<long>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(_=>!InventoryRevoked);
            _inventory.ReadAsync(Arg.Any<PlatformKeyVersion>()).Returns(call=> { InventoryReads++;var key=call.Arg<PlatformKeyVersion>();key.Version.ShouldBe("digest-v1");return new PlatformKeyInventorySnapshot(key.TenantId,key.Purpose,key.KeyAlias,1,[new(key,PlatformHmacKeyState.Active,1)],[]); });
            Dapr.GetSecretAsync("digest-store","digest-key",Arg.Any<IReadOnlyDictionary<string,string>>(),Arg.Any<CancellationToken>()).Returns(call=> { call.ArgAt<IReadOnlyDictionary<string,string>>(2).ShouldBe(new Dictionary<string,string>{{"version_id",DigestNativeVersion}});SecretReads++;if(RevokeDigestDuringSecretRead) InventoryRevoked=true;return Response=new(){{"value",Convert.ToBase64String(Enumerable.Repeat((byte)71,32).ToArray())}}; });
        }
        internal CustodyInteractionOccurrenceKeyProvider Provider()
        {
            var profile=new CustodyInteractionMaterialProfile(_registration,new("lease","attempt","process","transport","opaque"),new("digest-store","digest-key",DigestNativeVersion,"value",new("tenant",PlatformKeyPurpose.DigestKey,"digest","digest-v1","native","authority",null,null)));
            return new(Dapr,_material,Source,Registry,_inventory,_inventoryAuthority,new Dictionary<string,CustodyInteractionMaterialProfile>{{CustodyInteractionOccurrenceKeyProvider.ProfileId(Identity,true),profile},{CustodyInteractionOccurrenceKeyProvider.ProfileId(Identity,false),profile}});
        }
        private static string Hash<T>(T value)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
        public void Dispose() { Dapr.Dispose();CryptographicOperations.ZeroMemory(Root); }
    }
}
