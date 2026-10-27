using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class ExportQualificationAdmissionTests
{
    [Theory]
    [InlineData("null")][InlineData("malformed")][InlineData("trailing")][InlineData("rsa")][InlineData("p384")][InlineData("same-key")]
    [InlineData("empty-manifest")][InlineData("oversized-manifest")][InlineData("tenant")][InlineData("export")][InlineData("kek")][InlineData("signing")][InlineData("version")][InlineData("utf16")][InlineData("unsupported")]
    public async Task ActualWorkflowPreflightAndDirectExecutionRejectCaseInputsBeforePrivateResolution(string defect)
    {
        using var signer=ECDsa.Create(ECCurve.NamedCurves.nistP256);using var foreign=ECDsa.Create(ECCurve.NamedCurves.nistP256);using var rsa=RSA.Create(2048);using var p384=ECDsa.Create(ECCurve.NamedCurves.nistP384);
        var material=new Materials(signer);var trust=new Trust(signer.ExportSubjectPublicKeyInfo());var engine=new ExportQualificationEngine(material,trust);
        var input=new ExportQualificationInput("tenant","export","kek","signing",1,"manifest"u8.ToArray(),foreign.ExportSubjectPublicKeyInfo());string id="S3.crypto-substitution.public-anchor";
        input=defect switch { "null"=>input with { ForeignPublicAnchor=null },"malformed"=>input with { ForeignPublicAnchor=[1,2,3] },"trailing"=>input with { ForeignPublicAnchor=[..foreign.ExportSubjectPublicKeyInfo(),0] },
            "rsa"=>input with { ForeignPublicAnchor=rsa.ExportSubjectPublicKeyInfo() },"p384"=>input with { ForeignPublicAnchor=p384.ExportSubjectPublicKeyInfo() },"same-key"=>input with { ForeignPublicAnchor=signer.ExportSubjectPublicKeyInfo() },
            "empty-manifest"=>input with { Manifest=[] },"oversized-manifest"=>input with { Manifest=new byte[1048577] },"tenant"=>input with { TenantId="" },"export"=>input with { ExportId=" " },
            "kek"=>input with { KekVersion="" },"signing"=>input with { SigningKeyVersion="" },"version"=>input with { ManifestVersion=0 },"utf16"=>input with { ExportId="\ud800" },_=>input };
        if(defect=="unsupported") id="S3.unsupported";
        var store=NSubstitute.Substitute.For<IQualificationOriginalStore>();var workflow=new QualificationExportCaseWorkflow(engine,store,new Dictionary<string,ExportQualificationInput>{{id,input}}).Registrations().Single();
        var request=new JsonObject { ["caseId"]=id,["requestId"]=new string('a',64),["enrolledScope"]=new JsonObject { ["tenantId"]=input.TenantId,["logicalId"]=input.ExportId } };
        (await workflow.Preflight(request,TestContext.Current.CancellationToken)).ShouldNotBeEmpty();
        (await engine.ExecuteAsync(id,input,TestContext.Current.CancellationToken)).ShouldBeNull();material.Preflights.ShouldBe(0);material.Resolutions.ShouldBe(0);
    }
    [Theory]
    [InlineData("none",true)][InlineData("id",false)][InlineData("version",false)][InlineData("key",false)][InlineData("legacy",false)]
    public async Task OriginalRetainsExactAnchorIdentityVersionAndBytesAndLegacyIsExplicitlyIncomplete(string change,bool retained)
    {
        using var signer=ECDsa.Create(ECCurve.NamedCurves.nistP256);using var foreign=ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var material=new Materials(signer);var trust=new Trust(signer.ExportSubjectPublicKeyInfo());var engine=new ExportQualificationEngine(material,trust);
        var input=new ExportQualificationInput("tenant","export","kek","signing",1,"manifest"u8.ToArray(),foreign.ExportSubjectPublicKeyInfo());
        var original=await engine.ExecuteAsync("S3.es256-offline-anchor",input,TestContext.Current.CancellationToken);original.ShouldNotBeNull();
        string bytes=JsonSerializer.Serialize(original);original.PublicAnchorId.ShouldBe("independent-anchor");original.PublicAnchorVersion.ShouldBe("v1");
        if(change=="id") trust.Id="republished";if(change=="version") trust.Version="v2";if(change=="key") trust.Spki=foreign.ExportSubjectPublicKeyInfo();
        if(change=="legacy") original=original with { AnchorBindingVersion=null,PublicAnchorId=null,PublicAnchorVersion=null };
        var observation=await engine.ObserveAsync(original,input,TestContext.Current.CancellationToken);observation.ShouldNotBeNull();if(change=="legacy") observation.PublicAnchorRetained.ShouldBeNull();else observation.PublicAnchorRetained.ShouldBe(retained);material.Resolutions.ShouldBe(1);
        if(change!="legacy") JsonSerializer.Serialize(original).ShouldBe(bytes);else { JsonSerializer.Serialize(original).ShouldNotContain("AnchorBindingVersion");JsonSerializer.Serialize(original).ShouldNotContain("PublicAnchorId"); }
        trust.Spki=signer.ExportSubjectPublicKeyInfo();
        var substituted=await engine.ExecuteAsync("S3.crypto-substitution.public-anchor",input,TestContext.Current.CancellationToken);
        substituted.ShouldNotBeNull();(await engine.ObserveAsync(substituted,input,TestContext.Current.CancellationToken))!.DetachedEs256.ShouldBe(false);
    }
    private sealed class Trust(byte[] spki):IExportQualificationTrustSource
    {
        internal string Id="independent-anchor",Version="v1";internal byte[] Spki=spki;
        public Task<ExportQualificationTrust?> ResolveAsync(ExportQualificationInput input,CancellationToken token)=>Task.FromResult<ExportQualificationTrust?>(new(input.TenantId,input.SigningKeyVersion,Id,Version,Spki));
    }
    private sealed class Materials(ECDsa signer):IExportQualificationMaterialSource
    {
        internal int Preflights,Resolutions;
        public Task<IReadOnlyList<string>> PreflightAsync(ExportQualificationInput input,ExportQualificationMaterialUse use,CancellationToken token) { Preflights++;return Task.FromResult<IReadOnlyList<string>>([]); }
        public Task<ExportQualificationMaterial?> ResolveAsync(ExportQualificationInput input,ExportQualificationMaterialUse use,CancellationToken token) { Resolutions++;return Task.FromResult<ExportQualificationMaterial?>(new(null,null,ECDsa.Create(signer.ExportParameters(true)))); }
    }
}
