using System.Security.Cryptography;
using System.Runtime.InteropServices;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

public sealed class BoundedPhaseSignerTests
{
    [Fact]
    public async Task ActualPhaseSignerUsesBoundedNoFollowInputsAndPreservesValidSigning()
    {
        string root=Path.Combine(Path.GetTempPath(),"phase-signer-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            using var enrolled=ECDsa.Create(ECCurve.NamedCurves.nistP256);string key=Path.Combine(root,"private.pem"),anchor=Path.Combine(root,"public.pem");
            await File.WriteAllTextAsync(key,enrolled.ExportECPrivateKeyPem(),TestContext.Current.CancellationToken);await File.WriteAllTextAsync(anchor,enrolled.ExportSubjectPublicKeyInfoPem(),TestContext.Current.CancellationToken);
            using(var signer=await JointPhaseCommandExecutable.OpenSignerAsync(key,anchor,TestContext.Current.CancellationToken)) enrolled.VerifyData("fixture"u8,signer.SignData("fixture"u8,HashAlgorithmName.SHA256),HashAlgorithmName.SHA256).ShouldBeTrue();
            foreach(string path in new[]{key,anchor})
            {
                string saved=await File.ReadAllTextAsync(path,TestContext.Current.CancellationToken);File.Delete(path);Mkfifo(path,0x180).ShouldBe(0);
                await Should.ThrowAsync<IOException>(()=>JointPhaseCommandExecutable.OpenSignerAsync(key,anchor,TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(1),TestContext.Current.CancellationToken));
                File.Delete(path);await File.WriteAllTextAsync(path,new string('x',16385),TestContext.Current.CancellationToken);await Should.ThrowAsync<IOException>(()=>JointPhaseCommandExecutable.OpenSignerAsync(key,anchor,TestContext.Current.CancellationToken));
                await File.WriteAllTextAsync(path,saved,TestContext.Current.CancellationToken);
            }
            string moved=root+"-parked";Directory.Move(root,moved);Directory.CreateSymbolicLink(root,moved);
            try { await Should.ThrowAsync<IOException>(()=>JointPhaseCommandExecutable.OpenSignerAsync(key,anchor,TestContext.Current.CancellationToken)); }
            finally { Directory.Delete(root);Directory.Move(moved,root); }
        }
        finally { Directory.Delete(root,true); }
    }
    [DllImport("libc",EntryPoint="mkfifo",SetLastError=true)] private static extern int Mkfifo(string path,int mode);
}
