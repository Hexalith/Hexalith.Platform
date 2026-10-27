using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class QualificationOwningLifetimeTests
{
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task ActualOwnerWrapperRetainsOneStartupSnapshotAndIndependentCallerLifetime(bool expireDuringStartup)
    {
        using var signer=ECDsa.Create(ECCurve.NamedCurves.nistP256);using var lifetime=new CancellationTokenSource();
        string path=Path.Combine(Path.GetTempPath(),"owner-wrapper-"+Guid.NewGuid().ToString("N")+".json");
        var profile=new IndependentAuthorityReceiverInstallation("enrollment","owner","current","component","head","unused",signer.ExportSubjectPublicKeyInfoPem(),"file:/unused");
        await File.WriteAllBytesAsync(path,JsonSerializer.SerializeToUtf8Bytes(profile,CustodyBindingContract.Json),TestContext.Current.CancellationToken);
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var originals=Substitute.For<IQualificationOriginalStore>();
        try
        {
            async Task<int> Start(IndependentAuthorityReceiverInstallation captured,IReadOnlyList<IIndependentOwnerOperationHandler> handlers,CancellationToken startup,CancellationToken running)
            {
                captured.ShouldBe(profile);handlers.Count.ShouldBe(1);File.Delete(path);
                if(expireDuringStartup) { await Task.Delay(TimeSpan.FromSeconds(2),startup);return -1; }
                var builder=WebApplication.CreateSlimBuilder();builder.Logging.ClearProviders();builder.WebHost.UseUrls("http://127.0.0.1:0");await using var app=builder.Build();
                using var started=app.Lifetime.ApplicationStarted.Register(()=>entered.TrySetResult());var service=IndependentAuthorityHttpApplication.RunServiceAsync(app,startup,running);await service;return 0;
            }
            var operation=QualificationOwnerApplication.RunWithStartupBudgetAsync(path,new("executor","executor","v1",signer.ExportSubjectPublicKeyInfoPem()),signer,originals,new Dictionary<string,IQualificationOwnerCaseDriver>(),null,null,lifetime.Token,TimeSpan.FromSeconds(1),Start);
            if(expireDuringStartup) await Should.ThrowAsync<OperationCanceledException>(()=>operation);
            else
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(3),TestContext.Current.CancellationToken);await Task.Delay(1100,TestContext.Current.CancellationToken);
                operation.IsCompleted.ShouldBeFalse();lifetime.Cancel();(await operation.WaitAsync(TimeSpan.FromSeconds(3),TestContext.Current.CancellationToken)).ShouldBe(0);
            }
        }
        finally { lifetime.Cancel();File.Delete(path); }
    }
}
