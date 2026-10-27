using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Client;
using Hexalith.Platform.Custody;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class DaprExportQualificationResolverTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExactProductionDaprReferencesRoundtripAndOfflineVerificationNeverReadsPrivateMaterial(bool pem)
    {
        using var fixture=new Fixture(pem);var input=fixture.Input;
        var engine=new ExportQualificationEngine(fixture.Material(),fixture.Trust());
        var wrapped=await engine.ExecuteAsync("S3.aes256gcm",input,TestContext.Current.CancellationToken);wrapped.ShouldNotBeNull();
        (await engine.ObserveAsync(wrapped,input,TestContext.Current.CancellationToken))!.AuthenticatedWrap.ShouldBe(true);
        var signed=await engine.ExecuteAsync("S3.es256-offline-anchor",input,TestContext.Current.CancellationToken);signed.ShouldNotBeNull();
        int privateReads=fixture.PrivateReads;
        var offline=new ExportQualificationEngine(new NoPrivateMaterial(),fixture.Trust());
        (await offline.ObserveAsync(signed,input,TestContext.Current.CancellationToken))!.DetachedEs256.ShouldBe(true);
        fixture.PrivateReads.ShouldBe(privateReads);fixture.Responses.ShouldAllBe(response=>response.Count==0);
        int reads=fixture.Responses.Count;
        (await fixture.Material().ResolveAsync(input with { TenantId="foreign" },ExportQualificationMaterialUse.Wrap,TestContext.Current.CancellationToken)).ShouldBeNull();
        fixture.Responses.Count.ShouldBe(reads);
        fixture.Retained=true;
        (await fixture.Material().ResolveAsync(input,ExportQualificationMaterialUse.Wrap,TestContext.Current.CancellationToken)).ShouldBeNull();
        (await engine.ObserveAsync(wrapped,input,TestContext.Current.CancellationToken))!.Outcome.ShouldBe("Verified");
    }
    [Theory]
    [InlineData("wrong-purpose")]
    [InlineData("wrong-codec")]
    [InlineData("wrong-anchor")]
    [InlineData("changed-current-authority")]
    [InlineData("extra-field")]
    public async Task SubstitutedPurposeCodecPublicAnchorAuthorityOrNativeFieldIsRefused(string defect)
    {
        using var fixture=new Fixture();var profile=fixture.Profile;
        if(defect=="wrong-purpose") profile=profile with { Kek=profile.Kek! with { InventoryKey=profile.Kek.InventoryKey with { Purpose=PlatformKeyPurpose.DigestKey } } };
        if(defect=="wrong-codec") profile=profile with { Signer=profile.Signer! with { Codec="Pkcs8Pem" } };
        fixture.WrongAnchor=defect=="wrong-anchor";fixture.ChangeAuthorityAfterRead=defect=="changed-current-authority";fixture.ExtraField=defect=="extra-field";
        var use=defect is "wrong-codec" or "wrong-anchor"?ExportQualificationMaterialUse.Sign:ExportQualificationMaterialUse.Wrap;
        (await fixture.Material(profile).ResolveAsync(fixture.Input,use,TestContext.Current.CancellationToken)).ShouldBeNull();
        fixture.Responses.ShouldAllBe(response=>response.Count==0);
        if(defect=="wrong-purpose") fixture.Responses.ShouldBeEmpty();
    }
    [Theory]
    [InlineData("authorization",false)][InlineData("authorization",true)][InlineData("inventory",false)][InlineData("inventory",true)]
    [InlineData("post-decode-validation",false)][InlineData("post-decode-validation",true)][InlineData("trust",false)][InlineData("trust",true)][InlineData("native",false)][InlineData("native",true)]
    public async Task ActualResolversAndEngineReturnWithinOneBudgetWhileIgnoredCollaboratorsRemainOwned(string boundary,bool timeout)
    {
        using var fixture=new Fixture();using var caller=CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task Delay() { entered.TrySetResult();await release.Task; }
        var trust=fixture.Trust();
        if(boundary=="authorization") fixture._authority.AuthorizeOperationAsync(Arg.Any<PlatformKeyVersion>(),"ResolveExportQualificationMaterial",Arg.Any<CancellationToken>()).Returns(async _=> { await Delay();return true; });
        if(boundary=="inventory") fixture._actor.ReadAsync(Arg.Any<PlatformKeyVersion>()).Returns(async call=> { await Delay();var key=call.Arg<PlatformKeyVersion>();return new PlatformKeyInventorySnapshot(key.TenantId,key.Purpose,key.KeyAlias,9,[new(key,PlatformHmacKeyState.Active,9)],[]); });
        if(boundary=="post-decode-validation") fixture._authority.ValidateStateAsync(Arg.Any<PlatformKeyVersion>(),Arg.Any<long>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(async _=> { if(fixture.Responses.Count>0) await Delay();return true; });
        if(boundary=="trust") { trust=Substitute.For<IExportQualificationTrustSource>();trust.ResolveAsync(Arg.Any<ExportQualificationInput>(),Arg.Any<CancellationToken>()).Returns(async _=> { await Delay();return (ExportQualificationTrust?)new("tenant","sign-v1","anchor","anchor-v1",[]); }); }
        if(boundary=="native") fixture._client.GetSecretAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<IReadOnlyDictionary<string,string>>(),Arg.Any<CancellationToken>()).Returns(async _=> { await Delay();var value=new Dictionary<string,string>{{"value",Convert.ToBase64String(new byte[32])}};fixture.Responses.Add(value);return value; });
        DaprExportQualificationOperation.BudgetOverride=timeout?TimeSpan.FromMilliseconds(300):TimeSpan.FromSeconds(10);
        try
        {
            var material=fixture.Material(trust:trust);var use=boundary=="trust"?ExportQualificationMaterialUse.Sign:ExportQualificationMaterialUse.Wrap;
            Task pending;object? actual=null;
            if(boundary=="trust") pending=RunEngine();else pending=RunResolver();
            async Task RunEngine() { actual=await new ExportQualificationEngine(material,fixture.Trust()).ExecuteAsync("S3.es256-offline-anchor",fixture.Input,caller.Token); }
            async Task RunResolver() { actual=await material.ResolveAsync(fixture.Input,use,caller.Token); }await entered.Task.WaitAsync(TimeSpan.FromSeconds(5),TestContext.Current.CancellationToken);
            if(!timeout) caller.Cancel();
            if(timeout) { await pending.WaitAsync(TimeSpan.FromSeconds(2),TestContext.Current.CancellationToken);actual.ShouldBeNull(); }else await Should.ThrowAsync<OperationCanceledException>(()=>pending.WaitAsync(TimeSpan.FromSeconds(2),TestContext.Current.CancellationToken));
            release.TrySetResult();
            var drained=DateTime.UtcNow.AddSeconds(5);while(DaprExportQualificationOperation.ActiveOwners!=0 && DateTime.UtcNow<drained) await Task.Delay(10,TestContext.Current.CancellationToken);
            DaprExportQualificationOperation.ActiveOwners.ShouldBe(0);fixture.Responses.ShouldAllBe(value=>value.Count==0);
        }
        finally { release.TrySetResult();DaprExportQualificationOperation.BudgetOverride=null; }
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task ActualPublicTrustResolverOwnsCancellationIgnoringAuthorizationUntilCleanup(bool timeout)
    {
        using var fixture=new Fixture();using var caller=CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture._authority.AuthorizeOperationAsync(Arg.Any<PlatformKeyVersion>(),"ResolveExportQualificationMaterial",Arg.Any<CancellationToken>()).Returns(async _=> { entered.TrySetResult();await release.Task;return true; });
        DaprExportQualificationOperation.BudgetOverride=timeout?TimeSpan.FromMilliseconds(300):TimeSpan.FromSeconds(10);
        try
        {
            var pending=fixture.Trust().ResolveAsync(fixture.Input,caller.Token);await entered.Task.WaitAsync(TimeSpan.FromSeconds(5),TestContext.Current.CancellationToken);if(!timeout) caller.Cancel();
            if(timeout) (await pending.WaitAsync(TimeSpan.FromSeconds(2),TestContext.Current.CancellationToken)).ShouldBeNull();else await Should.ThrowAsync<OperationCanceledException>(()=>pending);
            release.TrySetResult();var end=DateTime.UtcNow.AddSeconds(5);while(DaprExportQualificationOperation.ActiveOwners!=0 && DateTime.UtcNow<end) await Task.Delay(10,TestContext.Current.CancellationToken);DaprExportQualificationOperation.ActiveOwners.ShouldBe(0);fixture.PrivateReads.ShouldBe(0);
        }
        finally { release.TrySetResult();DaprExportQualificationOperation.BudgetOverride=null; }
    }
    [Fact]
    public async Task BoundedExportOwnershipKeepsFourLatePrivateResultsChargedUntilTheyAreCleared()
    {
        using var caller=CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);int started=0;
        var allEntered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var owned=new List<ExportQualificationMaterial>();
        async Task<ExportQualificationMaterial?> Ignore(CancellationToken _) { if(Interlocked.Increment(ref started)==4) allEntered.TrySetResult();await release.Task;var value=new ExportQualificationMaterial(Enumerable.Repeat((byte)84,32).ToArray(),Enumerable.Repeat((byte)97,32).ToArray());lock(owned) owned.Add(value);return value; }
        var pending=Enumerable.Range(0,4).Select(_=>DaprExportQualificationOperation.RunAsync(Ignore,value=>value.Dispose(),caller.Token)).ToArray();
        await allEntered.Task.WaitAsync(TimeSpan.FromSeconds(5),TestContext.Current.CancellationToken);caller.Cancel();foreach(var task in pending) await Should.ThrowAsync<OperationCanceledException>(()=>task);
        (await DaprExportQualificationOperation.RunAsync(Ignore,value=>value.Dispose(),TestContext.Current.CancellationToken)).ShouldBeNull();started.ShouldBe(4);release.TrySetResult();
        var end=DateTime.UtcNow.AddSeconds(5);while(DateTime.UtcNow<end) { lock(owned) { if(owned.Count==4 && owned.All(value=>value.Cleared)) return; }await Task.Delay(10,TestContext.Current.CancellationToken); }
        throw new InvalidOperationException("Late private export results were not all cleared.");
    }
    private sealed class NoPrivateMaterial:IExportQualificationMaterialSource
    {
        public Task<IReadOnlyList<string>> PreflightAsync(ExportQualificationInput input,ExportQualificationMaterialUse use,CancellationToken token)=>throw new InvalidOperationException("Offline verifier reached private preflight.");
        public Task<ExportQualificationMaterial?> ResolveAsync(ExportQualificationInput input,ExportQualificationMaterialUse use,CancellationToken token)=>throw new InvalidOperationException("Offline verifier read private material.");
    }
    private sealed class Fixture:IDisposable
    {
        internal readonly DaprClient _client=Substitute.For<DaprClient>();internal readonly IPlatformKeyInventoryActor _actor=Substitute.For<IPlatformKeyInventoryActor>();internal readonly IPlatformKeyInventoryAuthority _authority=Substitute.For<IPlatformKeyInventoryAuthority>();
        private readonly ECDsa _signer=ECDsa.Create(ECCurve.NamedCurves.nistP256),_foreign=ECDsa.Create(ECCurve.NamedCurves.nistP256);
        private readonly byte[] _root=RandomNumberGenerator.GetBytes(32),_kek=RandomNumberGenerator.GetBytes(32);
        internal List<Dictionary<string,string>> Responses { get; }=[];internal bool Retained,WrongAnchor,ChangeAuthorityAfterRead,ExtraField;internal int PrivateReads;
        internal ExportQualificationInput Input=>new(Profile.TenantId,Profile.ExportId,"kek-v1","sign-v1",1,"fixture-manifest"u8.ToArray());
        internal DaprExportQualificationProfile Profile { get; }
        internal Fixture(bool pem=false)
        {
            PlatformKeyVersion Key(PlatformKeyPurpose purpose,string alias,string version)=>new("tenant",purpose,alias,version,"native-target-"+alias,"current-outside-authority",purpose==PlatformKeyPurpose.ExportManifestSigningKey?"anchor":null,purpose==PlatformKeyPurpose.ExportManifestSigningKey?"anchor-v1":null);
            DaprExportQualificationReference Ref(string key,string codec,PlatformKeyVersion inventory)=>new("exact-export-store",key,"7","value",codec,inventory);
            var signing=Key(PlatformKeyPurpose.ExportManifestSigningKey,"manifest","sign-v1");
            Profile=new("tenant","export",DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddDays(1),Ref("exports/export/key","Aes256Base64",Key(PlatformKeyPurpose.ExportEnvelopeKey,"export","export-v1")),
                Ref("tenants/tenant/kek","Aes256Base64",Key(PlatformKeyPurpose.TenantKek,"kek","kek-v1")),Ref("manifests/tenant/private",pem?"Pkcs8Pem":"Pkcs8DerBase64",signing),Ref("manifests/tenant/public",pem?"SpkiPem":"SpkiDerBase64",signing),"anchor","anchor-v1");
            _actor.ReadAsync(Arg.Any<PlatformKeyVersion>()).Returns(call=>Snapshot(call.ArgAt<PlatformKeyVersion>(0)));
            _authority.AuthorizeOperationAsync(Arg.Any<PlatformKeyVersion>(),"ResolveExportQualificationMaterial",Arg.Any<CancellationToken>()).Returns(call=>Allowed(call.ArgAt<PlatformKeyVersion>(0)));
            _authority.ValidateStateAsync(Arg.Any<PlatformKeyVersion>(),Arg.Any<long>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(call=>Allowed(call.ArgAt<PlatformKeyVersion>(0)) && (!ChangeAuthorityAfterRead || Responses.Count==0)
                && call.ArgAt<long>(1)==9 && call.ArgAt<string>(2)==Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(Snapshot(call.ArgAt<PlatformKeyVersion>(0))))));
            _client.GetSecretAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<IReadOnlyDictionary<string,string>>(),Arg.Any<CancellationToken>()).Returns(call=>
            {
                call.ArgAt<string>(0).ShouldBe("exact-export-store");call.ArgAt<IReadOnlyDictionary<string,string>>(2).ShouldBe(new Dictionary<string,string>{{"version_id","7"}});
                string key=call.ArgAt<string>(1);string value;
                if(key=="exports/export/key") { PrivateReads++;value=Convert.ToBase64String(_root); }
                else if(key=="tenants/tenant/kek") { PrivateReads++;value=Convert.ToBase64String(_kek); }
                else if(key=="manifests/tenant/private") { PrivateReads++;if(pem) value=_signer.ExportPkcs8PrivateKeyPem();else { byte[] encoded=_signer.ExportPkcs8PrivateKey();try { value=Convert.ToBase64String(encoded); } finally { CryptographicOperations.ZeroMemory(encoded); } } }
                else if(key=="manifests/tenant/public") value=pem?(WrongAnchor?_foreign:_signer).ExportSubjectPublicKeyInfoPem():Convert.ToBase64String((WrongAnchor?_foreign:_signer).ExportSubjectPublicKeyInfo());
                else throw new InvalidOperationException("Unenrolled GetSecret key.");
                var response=new Dictionary<string,string>{{"value",value}};if(ExtraField) response["unexpected"]="fixture";Responses.Add(response);return response;
            });
        }
        private bool Allowed(PlatformKeyVersion key)=>new[]{Profile.ExportKey!,Profile.Kek!,Profile.Signer!,Profile.PublicAnchor}.Any(reference=>reference.InventoryKey==key);
        private PlatformKeyInventorySnapshot Snapshot(PlatformKeyVersion key)=>new(key.TenantId,key.Purpose,key.KeyAlias,9,[new(key,Retained?PlatformHmacKeyState.Retained:PlatformHmacKeyState.Active,9)],[]);
        internal IExportQualificationTrustSource Trust()=>new DaprExportQualificationTrustSource(_client,_actor,_authority,TimeProvider.System,new Dictionary<string,DaprExportQualificationProfile>{{"export",Profile}});
        internal IExportQualificationMaterialSource Material(DaprExportQualificationProfile? profile=null,IExportQualificationTrustSource? trust=null)=>new DaprExportQualificationMaterialSource(_client,_actor,_authority,trust??Trust(),TimeProvider.System,new Dictionary<string,DaprExportQualificationProfile>{{"export",profile??Profile}});
        public void Dispose() { _client.Dispose();_signer.Dispose();_foreign.Dispose();CryptographicOperations.ZeroMemory(_root);CryptographicOperations.ZeroMemory(_kek); }
    }
}
