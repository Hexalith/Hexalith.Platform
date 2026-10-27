using System.Security.Cryptography;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class PrivateAcquisitionBufferTests
{
    [Fact]
    public async Task ActualDirectPrivateReadHasNoAdditionalLargeReadAheadAllocation()
    {
        string path=Path.GetTempFileName();using var signer=ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            await File.WriteAllTextAsync(path,signer.ExportPkcs8PrivateKeyPem(),TestContext.Current.CancellationToken);
            // Warm documented RandomAccess/JIT paths, then measure in the same
            // actual acquisition worker. A disposed 64KiB stream read-ahead copy
            // would exceed this bound even for this small private credential.
            await BoundedRegularFile.RunAcquisitionAsync(async token=>
            {
                for(int i=0;i<2;i++) CryptographicOperations.ZeroMemory(await BoundedRegularFile.ReadLiteralInOwnedWorkerAsync(path,8192,token));
                long before=GC.GetAllocatedBytesForCurrentThread();
                byte[] bytes=await BoundedRegularFile.ReadLiteralInOwnedWorkerAsync(path,8192,token);
                long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
                char[] chars=System.Text.Encoding.UTF8.GetChars(bytes);
                try { using var parsed=ECDsa.Create();parsed.ImportFromPem(chars);allocated.ShouldBeLessThan(bytes.Length+32768); }
                finally { CryptographicOperations.ZeroMemory(bytes);Array.Clear(chars); }
                return Array.Empty<byte>();
            },TestContext.Current.CancellationToken);
        }
        finally { File.Delete(path); }
    }
    [Theory]
    [InlineData("valid")][InlineData("parse-failure")][InlineData("growth")][InlineData("cancel")][InlineData("late")]
    public async Task ActualPrivateSignerUsesOnlyOwnedReadRegionsAndClearsSentinelAndMaterial(string fault)
    {
        string path=Path.GetTempFileName();using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[]? owned=null,sentinel=null;char[]? chars=null;int consumers=0;
        using var cancel=new CancellationTokenSource();using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        try
        {
            await File.WriteAllTextAsync(path,key.ExportPkcs8PrivateKeyPem(),TestContext.Current.CancellationToken);
            var work=BoundedRegularFile.UseUtf8SpanWithInspectionAsync(path,8192,material=>
            {
                consumers++;using var parsed=ECDsa.Create();parsed.ImportFromPem(material);
                parsed.ExportSubjectPublicKeyInfo().ShouldBe(key.ExportSubjectPublicKeyInfo());
                if(fault=="parse-failure") throw new CryptographicException("controlled signer failure");
                return parsed.SignData("fixture"u8,HashAlgorithmName.SHA256);
            },cancel.Token,(bytes,text)=> { owned=bytes;chars=text; },
            ()=> { if(fault=="growth") File.AppendAllText(path,"X"); },
            (bytes,excess)=> { owned=bytes;sentinel=excess;if(fault=="cancel") cancel.Cancel();if(fault=="late") { entered.Set();release.Wait(TestContext.Current.CancellationToken); } });
            if(fault=="late") { entered.Wait(TimeSpan.FromSeconds(2),TestContext.Current.CancellationToken).ShouldBeTrue();cancel.Cancel(); }
            if(fault=="valid") { var signature=await work;key.VerifyData("fixture"u8,signature,HashAlgorithmName.SHA256).ShouldBeTrue(); }
            else if(fault=="parse-failure") await Should.ThrowAsync<CryptographicException>(()=>work);
            else if(fault=="growth") await Should.ThrowAsync<IOException>(()=>work);
            else await Should.ThrowAsync<OperationCanceledException>(()=>work);
            release.Set();
            for(int i=0;i<200 && (owned?.Any(value=>value!=0)==true || sentinel?.Any(value=>value!=0)==true);i++) await Task.Delay(10,TestContext.Current.CancellationToken);
            owned.ShouldNotBeNull().ShouldAllBe(value=>value==0);sentinel.ShouldNotBeNull().ShouldAllBe(value=>value==0);
            if(chars is not null) chars.ShouldAllBe(value=>value=='\0');
            consumers.ShouldBe(fault is "valid" or "parse-failure"?1:0);
        }
        finally { release.Set();File.Delete(path); }
    }
}
