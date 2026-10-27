using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.Platform.Protection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class JointPhaseSourceStartupTests
{
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task ActualNativeSourceWrapperCapturesReceivingProfileOnceAndSeparatesStartupFromLifetime(bool stall)
    {
        string directory=Path.Combine(Path.GetTempPath(),"native-source-wrapper-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);using var lifetime=new CancellationTokenSource();var token=TestContext.Current.CancellationToken;
        string sourcePath=Path.Combine(directory,"source.json"),receivingPath=Path.Combine(directory,"receiving.json");
        var receiving=new IndependentAuthorityReceiverInstallation("enrollment","source","trust","component","head","unused",key.ExportSubjectPublicKeyInfoPem(),"file:/unused");
        var binding=new IndependentDaprAuthorityBinding("fixture","file:/fixture",key.ExportSubjectPublicKeyInfoPem(),"enrollment","scope","trust");
        var source=new JointPhaseSourceInstallation(receivingPath,"unused",new(),binding,[],"unused","unused","unused",new("tenant","scope","{}",key.ExportSubjectPublicKeyInfoPem(),"binding","authorizer","file:/fixture",binding,binding));
        await File.WriteAllBytesAsync(sourcePath,JsonSerializer.SerializeToUtf8Bytes(source,CustodyBindingContract.Json),token);
        await File.WriteAllBytesAsync(receivingPath,JsonSerializer.SerializeToUtf8Bytes(receiving,CustodyBindingContract.Json),token);
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<int> Serve(JointPhaseSourceInstallation capturedSource,IndependentAuthorityReceiverInstallation captured,CancellationToken startup,CancellationToken running)
        {
            capturedSource.ReceivingInstallationPath.ShouldBe(receivingPath);captured.ShouldBe(receiving);File.Delete(sourcePath);File.Delete(receivingPath);
            if(stall) { await Task.Delay(2000,startup);return -1; }
            var builder=WebApplication.CreateSlimBuilder();builder.Logging.ClearProviders();builder.WebHost.UseUrls("http://127.0.0.1:0");await using var app=builder.Build();
            using var started=app.Lifetime.ApplicationStarted.Register(()=>entered.TrySetResult());
            await IndependentAuthorityHttpApplication.RunServiceAsync(app,startup,running);return 0;
        }
        try
        {
            var work=JointPhaseSourceExecutable.RunWithStartupBudgetAsync(sourcePath,lifetime.Token,TimeSpan.FromSeconds(1),Serve);
            if(stall) await Should.ThrowAsync<OperationCanceledException>(()=>work);
            else
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(3),token);await Task.Delay(1100,token);work.IsCompleted.ShouldBeFalse();
                lifetime.Cancel();(await work.WaitAsync(TimeSpan.FromSeconds(3),token)).ShouldBe(0);
            }
        }
        finally { lifetime.Cancel();Directory.Delete(directory,true); }
    }
}
