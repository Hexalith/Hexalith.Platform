using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class BoundedRoleStartupTests
{
    [Fact]
    public async Task ActualPublicRuntimeConfigurationLoaderRefusesFifoParentSymlinkAndOversize()
    {
        string root=Path.Combine(Path.GetTempPath(),"runtime-config-bounded-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);string path=Path.Combine(root,"config.json");
        try
        {
            await File.WriteAllTextAsync(path,"{}",TestContext.Current.CancellationToken);SecretsRuntimeConfiguration.Load(path).ShouldNotBeNull();
            File.Delete(path);Mkfifo(path,0x180).ShouldBe(0);SecretsRuntimeConfiguration.Load(path).ShouldBeNull();File.Delete(path);
            await File.WriteAllTextAsync(path,new string('x',1024*1024+1),TestContext.Current.CancellationToken);SecretsRuntimeConfiguration.Load(path).ShouldBeNull();
            await File.WriteAllTextAsync(path,"{}",TestContext.Current.CancellationToken);string moved=root+"-parked";Directory.Move(root,moved);Directory.CreateSymbolicLink(root,moved);
            try { SecretsRuntimeConfiguration.Load(path).ShouldBeNull(); }finally { Directory.Delete(root);Directory.Move(moved,root); }
        }
        finally { Directory.Delete(root,true); }
    }
    [Theory]
    [InlineData("fifo")][InlineData("symlink")][InlineData("oversize")][InlineData("cancelled")]
    public async Task ActualRoleCompositionAndAuthorityKeyStartupRefuseBoundedInvalidFiles(string kind)
    {
        string directory=Path.Combine(Path.GetTempPath(),"bounded-role-startup-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);var token=TestContext.Current.CancellationToken;
        try
        {
            string path=Path.Combine(directory,"input"),key=Path.Combine(directory,"key");using var pair=ECDsa.Create(ECCurve.NamedCurves.nistP256);await File.WriteAllTextAsync(key,pair.ExportECPrivateKeyPem(),token);
            using(var loaded=await IndependentAuthorityHttpApplication.LoadSignerAsync(key,token)) loaded.ExportSubjectPublicKeyInfo().ShouldBe(pair.ExportSubjectPublicKeyInfo());
            var profile=new IndependentAuthorityReceiverInstallation("fixture-enrollment","fixture-scope","trust","store","head",path,pair.ExportSubjectPublicKeyInfoPem(),"file:/unused");
            string profilePath=Path.Combine(directory,"profile.json");await File.WriteAllBytesAsync(profilePath,JsonSerializer.SerializeToUtf8Bytes(profile,CustodyBindingContract.Json),token);
            if(kind=="fifo") Mkfifo(path,0x180).ShouldBe(0);
            else if(kind=="symlink") File.CreateSymbolicLink(path,key);
            else { await using var file=File.Create(path);file.SetLength(kind=="oversize"?1024*1024+1:8); }
            using var cancelled=CancellationTokenSource.CreateLinkedTokenSource(token);if(kind=="cancelled") cancelled.Cancel();
            foreach(string role in new[]{"executor","observer","enrollment"})
            {
                var services=new ServiceCollection();
                if(kind=="cancelled") Should.Throw<OperationCanceledException>(()=>services.AddInstalledQualificationRole(role,path,cancelled.Token));
                else { string? missing=services.AddInstalledQualificationRole(role,path,cancelled.Token);missing.ShouldNotBeNull(); }
                services.ShouldBeEmpty();
            }
            (await IndependentAuthorityHttpApplication.RunAsync(profilePath,cancellationToken:cancelled.Token).WaitAsync(TimeSpan.FromSeconds(2),token)).ShouldBe(2);
            // A private key whose length exceeds its tighter bound is rejected before parsing/allocation too.
            if(kind=="oversize") { await using var file=new FileStream(path,FileMode.Open,FileAccess.Write);file.SetLength(65537); }
            (await IndependentAuthorityHttpApplication.RunAsync(profilePath,cancellationToken:cancelled.Token).WaitAsync(TimeSpan.FromSeconds(2),token)).ShouldBe(2);
        }
        finally { Directory.Delete(directory,true); }
    }
    [Fact]
    public async Task ExistingVolatileTmpfsCannotBackDurableArchiveFencingOrNativeCapacity()
    {
        if(!OperatingSystem.IsLinux() || !Directory.Exists("/dev/shm")) return;
        string directory=Path.Combine("/dev/shm","story54-tmpfs-refusal-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try
        {
            Should.Throw<IOException>(()=>ArchiveFileLease.FilesystemIdentity(directory)).Message.ShouldContain("PersistentArchiveBacking");
            await Should.ThrowAsync<IOException>(()=>ArchiveFileLease.AcquireAsync(Path.Combine(directory,"lock"),TestContext.Current.CancellationToken));
        }
        finally { Directory.Delete(directory,true); }
    }
    [DllImport("libc",EntryPoint="mkfifo",SetLastError=true)] private static extern int Mkfifo(string path,int mode);
}
