using Aspire.Hosting;
using Shouldly;

namespace Hexalith.Platform.Composition.Tests;

// Uses actual Aspire resource construction only. No Build/Start/Run or external service is invoked.
public sealed class PortableDevelopmentCompositionTests
{
    [Fact]
    public async Task TimedOutDevelopmentWorkersKeepAllFourSlotsUntilActualLateCompletion()
    {
        string path=Path.Combine(Path.GetTempPath(),"development-slots-"+Guid.NewGuid().ToString("N"));
        using var release=new ManualResetEventSlim();int entered=0,finished=0;
        var allEntered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var allFinished=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await File.WriteAllTextAsync(path,"{}",TestContext.Current.CancellationToken);
            using var callers=new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
            Task<byte[]> Start()=>BoundedDevelopmentFile.ReadAsync(path,100,callers.Token,()=>
            {
                if(Interlocked.Increment(ref entered)==4) allEntered.TrySetResult();
                try { release.Wait(); }finally { if(Interlocked.Increment(ref finished)==4) allFinished.TrySetResult(); }
            });
            var workers=Enumerable.Range(0,4).Select(_=>Start()).ToArray();await allEntered.Task.WaitAsync(TimeSpan.FromSeconds(2),TestContext.Current.CancellationToken);
            foreach(var worker in workers) await Should.ThrowAsync<OperationCanceledException>(()=>worker);
            using var fifth=new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            await Should.ThrowAsync<OperationCanceledException>(()=>BoundedDevelopmentFile.ReadAsync(path,100,fifth.Token,()=>Interlocked.Increment(ref entered)));
            entered.ShouldBe(4);finished.ShouldBe(0);release.Set();await allFinished.Task.WaitAsync(TimeSpan.FromSeconds(2),TestContext.Current.CancellationToken);
            (await BoundedDevelopmentFile.ReadAsync(path,100,TestContext.Current.CancellationToken)).ShouldBe("{}"u8.ToArray());
        }
        finally { release.Set();File.Delete(path); }
    }
    [Theory]
    [InlineData("valid")][InlineData("parent-link")][InlineData("file-link")][InlineData("changing")][InlineData("oversize")][InlineData("directory")][InlineData("cancel")][InlineData("stall")]
    [InlineData("missing-profile")][InlineData("missing-sibling")][InlineData("rebuilder-collision")][InlineData("rebuilder-collision-case")]
    public async Task ActualDevelopmentTryComposeUsesPortableBoundedMetadataAndRetainsLateWorkerOwnership(string fault)
    {
        string root=Path.Combine(Path.GetTempPath(),"portable-composition-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        using var release=new ManualResetEventSlim();using var cancelled=new CancellationTokenSource();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var finished=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var input=AgentsAcceptedDeploymentContextTests.Inputs(root);
            string path=Path.Combine(root,"profile.json");await File.WriteAllTextAsync(path,input.Json,TestContext.Current.CancellationToken);
            string[] names=["eventstore/src/Hexalith.EventStore/Hexalith.EventStore.csproj","tenants/src/Hexalith.Tenants/Hexalith.Tenants.csproj","tenants/src/Hexalith.Tenants.Api/Hexalith.Tenants.Api.csproj","parties/src/Hexalith.Parties/Hexalith.Parties.csproj","conversations/src/Hexalith.Conversations.Server/Hexalith.Conversations.Server.csproj","agents/src/Hexalith.Agents.Server/Hexalith.Agents.Server.csproj","agents/src/Hexalith.Agents.UI/Hexalith.Agents.UI.csproj"];
            foreach(string name in names) { string file=Path.Combine(root,name);Directory.CreateDirectory(Path.GetDirectoryName(file)!);await File.WriteAllTextAsync(file,"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>",TestContext.Current.CancellationToken); }
            var builder=DistributedApplication.CreateBuilder(new DistributedApplicationOptions { Args=[],AssemblyName=typeof(PortableDevelopmentCompositionTests).Assembly.GetName().Name,ProjectDirectory=root,DisableDashboard=true,AllowUnsecuredTransport=true });
            builder.Environment.EnvironmentName="Development";builder.Configuration[AgentsHostComposition.ProfilePathKey]=path;
            if(fault=="parent-link")
            {
                string parent=Path.Combine(root,"profile-inputs");Directory.CreateDirectory(parent);string target=Path.Combine(parent,"profile.json");File.Move(path,target);
                string alias=Path.Combine(root,"profile-alias");Directory.CreateSymbolicLink(alias,parent);path=Path.Combine(alias,"profile.json");builder.Configuration[AgentsHostComposition.ProfilePathKey]=path;
            }
            if(fault=="file-link") { string target=path+".target";File.Move(path,target);File.CreateSymbolicLink(path,target); }
            if(fault=="missing-profile") File.Delete(path);
            if(fault=="missing-sibling") File.Delete(Path.Combine(root,names[^1]));
            if(fault.StartsWith("rebuilder-collision",StringComparison.Ordinal))
                builder.AddExecutable(fault.EndsWith("-case",StringComparison.Ordinal)?"TENANTS-REBUILDER":"tenants-rebuilder","unused-fixture-command",root);
            var existing=builder.Resources.ToArray();
            int inspections=0;BoundedDevelopmentFile.Inspection=_=>Interlocked.Increment(ref inspections);
            if(fault=="oversize") using(var output=new FileStream(path,FileMode.Open)) output.SetLength(1024*1024+1);
            if(fault=="directory") { File.Delete(path);Directory.CreateDirectory(path); }
            if(fault=="cancel") cancelled.Cancel();
            if(fault=="missing-sibling")
            {
                Should.Throw<FileNotFoundException>(()=>AgentsHostComposition.TryCompose(builder)).FileName.ShouldBe(Path.Combine(root,names[^1]));
                builder.Resources.ShouldBe(existing);return;
            }
            Func<string,int,CancellationToken,Task<byte[]>>? acquire=null;
            if(fault is "changing" or "stall") acquire=(file,max,token)=>BoundedDevelopmentFile.ReadAsync(file,max,token,
                file==path && fault=="stall"?()=> { entered.TrySetResult();try { release.Wait(); }finally { finished.TrySetResult(); } }:null,
                file==path && fault=="changing"?()=> { using var output=new FileStream(path,FileMode.Open,FileAccess.Write,FileShare.ReadWrite);output.SetLength(max+1); }:null);
            bool composed=await Task.Run(()=>AgentsHostComposition.TryCompose(builder,cancelled.Token,fault=="stall"?TimeSpan.FromMilliseconds(100):TimeSpan.FromSeconds(3),acquire),TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5),TestContext.Current.CancellationToken);
            composed.ShouldBe(fault is "valid" or "parent-link" or "file-link");
            if(composed)
            {
                inspections.ShouldBe(8);
                string[] expected=["eventstore","tenants","tenants-api","parties","conversations","agents","agents-ui"];
                builder.Resources.OfType<Aspire.Hosting.ApplicationModel.ProjectResource>().Select(value=>value.Name).Order(StringComparer.Ordinal).ShouldBe(expected.Order(StringComparer.Ordinal));
                builder.Resources.Select(value=>value.Name).Order(StringComparer.Ordinal).ShouldBe(expected.SelectMany(name=>new[]{name,name+"-rebuilder"}).Order(StringComparer.Ordinal));
            }
            else builder.Resources.ShouldBe(existing);
            if(fault=="stall") { entered.Task.IsCompleted.ShouldBeTrue();finished.Task.IsCompleted.ShouldBeFalse();release.Set();await finished.Task.WaitAsync(TimeSpan.FromSeconds(2),TestContext.Current.CancellationToken); }
        }
        finally { BoundedDevelopmentFile.Inspection=null;release.Set();Directory.Delete(root,true); }
    }
}
