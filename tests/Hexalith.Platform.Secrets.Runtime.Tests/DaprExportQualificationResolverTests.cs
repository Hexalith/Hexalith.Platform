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
    private sealed class NoPrivateMaterial:IExportQualificationMaterialSource
    {
        public Task<IReadOnlyList<string>> PreflightAsync(ExportQualificationInput input,ExportQualificationMaterialUse use,CancellationToken token)=>throw new InvalidOperationException("Offline verifier reached private preflight.");
        public Task<ExportQualificationMaterial?> ResolveAsync(ExportQualificationInput input,ExportQualificationMaterialUse use,CancellationToken token)=>throw new InvalidOperationException("Offline verifier read private material.");
    }
    private sealed class Fixture:IDisposable
    {
        private readonly DaprClient _client=Substitute.For<DaprClient>();private readonly IPlatformKeyInventoryActor _actor=Substitute.For<IPlatformKeyInventoryActor>();private readonly IPlatformKeyInventoryAuthority _authority=Substitute.For<IPlatformKeyInventoryAuthority>();
        private readonly ECDsa _signer=ECDsa.Create(ECCurve.NamedCurves.nistP256),_foreign=ECDsa.Create(ECCurve.NamedCurves.nistP256);
        private readonly byte[] _root=RandomNumberGenerator.GetBytes(32),_kek=RandomNumberGenerator.GetBytes(32);
        internal List<Dictionary<string,string>> Responses { get; }=[];internal bool Retained,WrongAnchor,ChangeAuthorityAfterRead,ExtraField;internal int PrivateReads;
        internal ExportQualificationInput Input=>new("tenant","export","kek-v1","sign-v1",1,"fixture-manifest"u8.ToArray());
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
        internal IExportQualificationMaterialSource Material(DaprExportQualificationProfile? profile=null)=>new DaprExportQualificationMaterialSource(_client,_actor,_authority,Trust(),TimeProvider.System,new Dictionary<string,DaprExportQualificationProfile>{{"export",profile??Profile}});
        public void Dispose() { _client.Dispose();_signer.Dispose();_foreign.Dispose();CryptographicOperations.ZeroMemory(_root);CryptographicOperations.ZeroMemory(_kek); }
    }
}
