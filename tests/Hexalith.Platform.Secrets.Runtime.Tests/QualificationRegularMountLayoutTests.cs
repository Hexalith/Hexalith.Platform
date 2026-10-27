using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class QualificationRegularMountLayoutTests
{
    [Theory]
    [InlineData("executor")][InlineData("observer")][InlineData("enrollment")]
    public async Task ActualSelectedRuntimeInstallationAndPrivateSignerRejectAtomicWriterKeysAndAcceptRegularMounts(string role)
    {
        string root=Path.Combine(Path.GetTempPath(),"role-file-layout-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        using var executor=ECDsa.Create(ECCurve.NamedCurves.nistP256);using var observer=ECDsa.Create(ECCurve.NamedCurves.nistP256);using var enrollment=ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            var anchors=new[]{("executor",executor),("observer",observer),("enrollment",enrollment)}.ToDictionary(value=>value.Item1,value=>
            { string pem=value.Item2.ExportSubjectPublicKeyInfoPem();return new QualificationInstalledAnchor("fixture-"+value.Item1,"v1",pem,Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(pem)))); });
            IndependentDaprAuthorityBinding Binding(string id)=>new(id,"file:"+Path.Combine(root,id+".bearer"),executor.ExportSubjectPublicKeyInfoPem(),"fixture-enrollment","fixture-scope","v1");
            var installed=new QualificationInstallation(role,Binding("scope-authority"),Binding("original-authority"),Binding("owner"),anchors);
            var files=new Dictionary<string,byte[]> { ["runtime.json"]="{\"runtime\":{\"providerBinding\":\"fixture-provider\"}}"u8.ToArray(),
                ["qualification-installation.json"]=JsonSerializer.SerializeToUtf8Bytes(installed,CustodyBindingContract.Json),["signer.pem"]=Encoding.UTF8.GetBytes(executor.ExportECPrivateKeyPem()) };
            string version=Path.Combine(root,"..fixture-version");Directory.CreateDirectory(version);Directory.CreateSymbolicLink(Path.Combine(root,"..data"),version);
            foreach(var pair in files) { File.WriteAllBytes(Path.Combine(version,pair.Key),pair.Value);File.CreateSymbolicLink(Path.Combine(root,pair.Key),"..data/"+pair.Key); }
            SecretsRuntimeConfiguration.Load(Path.Combine(root,"runtime.json")).ShouldBeNull();
            new ServiceCollection().AddInstalledQualificationRole(role,Path.Combine(root,"qualification-installation.json"),TestContext.Current.CancellationToken).ShouldNotBeNull();
            await Should.ThrowAsync<IOException>(()=>IndependentAuthorityHttpApplication.LoadSignerAsync(Path.Combine(root,"signer.pem"),TestContext.Current.CancellationToken));
            // A subPath file bind exposes the same bytes as a regular selected file, without changing its locator.
            foreach(var pair in files) { File.Delete(Path.Combine(root,pair.Key));File.WriteAllBytes(Path.Combine(root,pair.Key),pair.Value); }
            SecretsRuntimeConfiguration.Load(Path.Combine(root,"runtime.json"))!.ProviderBinding.ShouldBe("fixture-provider");
            new ServiceCollection().AddInstalledQualificationRole(role,Path.Combine(root,"qualification-installation.json"),TestContext.Current.CancellationToken).ShouldBeNull();
            using var loaded=await IndependentAuthorityHttpApplication.LoadSignerAsync(Path.Combine(root,"signer.pem"),TestContext.Current.CancellationToken);
            loaded.ExportSubjectPublicKeyInfo().ShouldBe(executor.ExportSubjectPublicKeyInfo());
        }
        finally { Directory.Delete(root,true); }
    }
}
