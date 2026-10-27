using System.Runtime.InteropServices;
using Aspire.Hosting;
using Hexalith.Platform.Secrets.Runtime;
using Shouldly;

namespace Hexalith.Platform.Composition.Tests;

// Real Aspire builder/resource APIs, actual production TryCompose, disposable metadata only. No host starts.
public sealed class AgentsHostStartupBoundaryTests
{
    [Theory]
    [InlineData("profile","fifo")][InlineData("register","fifo")][InlineData("packet","fifo")][InlineData("project","fifo")]
    [InlineData("profile","oversize")][InlineData("register","oversize")][InlineData("packet","oversize")]
    [InlineData("profile","parent-link")][InlineData("register","parent-link")][InlineData("packet","parent-link")]
    [InlineData("profile","changing")][InlineData("register","changing")][InlineData("packet","changing")]
    [InlineData("profile","stall")][InlineData("register","stall")][InlineData("packet","stall")][InlineData("profile","cancel")]
    [InlineData("register","duplicate-section")][InlineData("register","duplicate-target")][InlineData("register","duplicate-date")][InlineData("register","duplicate-status")][InlineData("register","duplicate-binding")]
    [InlineData("packet","duplicate-json")][InlineData("packet","nested-duplicate-json")]
    [InlineData("profile","valid")][InlineData("project","development-missing")]
    [InlineData("profile","late-name-conflict")][InlineData("profile","late-name-conflict-case")]
    public async Task ActualTryComposeBoundsAllSelectedMetadataBeforeResourceComposition(string selected,string fault)
    {
        string root=Path.Combine(Path.GetTempPath(),"apphost-boundary-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        using var released=new ManualResetEventSlim();using var cancel=new CancellationTokenSource();var finished=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var input=AgentsAcceptedDeploymentContextTests.Inputs(root);
            var paths=new Dictionary<string,string>();
            foreach(var pair in new[]{("profile",input.Json),("register",input.Register),("packet",input.Packet)})
            { string dir=Path.Combine(root,pair.Item1);Directory.CreateDirectory(dir);paths[pair.Item1]=Path.Combine(dir,"input");await File.WriteAllTextAsync(paths[pair.Item1],pair.Item2,TestContext.Current.CancellationToken); }
            string[] projects=["eventstore/src/Hexalith.EventStore/Hexalith.EventStore.csproj","tenants/src/Hexalith.Tenants/Hexalith.Tenants.csproj","tenants/src/Hexalith.Tenants.Api/Hexalith.Tenants.Api.csproj","parties/src/Hexalith.Parties/Hexalith.Parties.csproj","conversations/src/Hexalith.Conversations.Server/Hexalith.Conversations.Server.csproj","agents/src/Hexalith.Agents.Server/Hexalith.Agents.Server.csproj","agents/src/Hexalith.Agents.UI/Hexalith.Agents.UI.csproj"];
            foreach(string relative in projects) { string path=Path.Combine(root,relative);Directory.CreateDirectory(Path.GetDirectoryName(path)!);await File.WriteAllTextAsync(path,"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>",TestContext.Current.CancellationToken); }
            input=AcceptedLaunchFixture.Enroll(input);AcceptedLaunchArtifactLease.KernelProofOverride=(_,_)=>true;
            foreach(var pair in new[]{("profile",input.Json),("register",input.Register),("packet",input.Packet)}) await File.WriteAllTextAsync(paths[pair.Item1],pair.Item2,TestContext.Current.CancellationToken);
            paths["project"]=Path.Combine(root,projects[^1]);string chosen=paths[selected];
            if(fault.StartsWith("duplicate-",StringComparison.Ordinal) || fault=="nested-duplicate-json")
            {
                string text=File.ReadAllText(chosen);
                if(fault=="duplicate-section") text+="\n"+AcceptedDependencyRecords.Section(text,"EXT-HOST-1");
                else if(fault=="duplicate-json") text=text.Insert(1,"\"candidateCommit\":\""+new string('f',40)+"\",");
                else if(fault=="nested-duplicate-json") text=text.Replace("\"platformCommit\":","\"platformCommit\":\""+new string('f',40)+"\",\"platformCommit\":",StringComparison.Ordinal);
                else
                {
                    string name=fault switch { "duplicate-target"=>"TargetVersionOrCommit","duplicate-date"=>"TargetIntegrationDate","duplicate-status"=>"AcceptedStatus",_=>"HostQualificationBinding" };
                    text=text.Replace("### EXT-HOST-1 — Host\n","### EXT-HOST-1 — Host\n| `"+name+"` | `contradiction` |\n",StringComparison.Ordinal);
                }
                File.WriteAllText(chosen,text);
            }
            if(fault=="development-missing") File.Delete(chosen);
            if(fault=="fifo") { File.Delete(chosen);Mkfifo(chosen,0x180).ShouldBe(0); }
            if(fault=="oversize") using(var writer=new FileStream(chosen,FileMode.Open)) writer.SetLength(1024*1024+1);
            if(fault=="parent-link") { string parent=Path.GetDirectoryName(chosen)!;Directory.Move(parent,parent+"-parked");Directory.CreateSymbolicLink(parent,parent+"-parked"); }
            var builder=DistributedApplication.CreateBuilder(new DistributedApplicationOptions { Args=[],AssemblyName=typeof(AgentsHostStartupBoundaryTests).Assembly.GetName().Name,ProjectDirectory=root,DisableDashboard=true,AllowUnsecuredTransport=true });
            using var launchLifetime=AgentsHostComposition.OwnLaunch(builder);
            builder.Environment.EnvironmentName=fault=="development-missing"?"Development":"Production";
            builder.Configuration[AgentsHostComposition.ProfilePathKey]=paths["profile"];builder.Configuration["Platform:Agents:AcceptedRegister"]=paths["register"];builder.Configuration["Platform:Agents:AcceptancePacket"]=paths["packet"];
            if(fault.StartsWith("late-name-conflict",StringComparison.Ordinal)) builder.AddProject(fault.EndsWith("-case",StringComparison.Ordinal)?"AGENTS-UI":"agents-ui",paths["project"],launchProfileName:null);
            var callerResources=builder.Resources.ToArray();
            if(fault=="cancel") cancel.Cancel();
            Func<string,int,CancellationToken,Task<byte[]>>? acquire=null;
            if(fault is "changing" or "stall") acquire=(path,max,ct)=>path!=chosen?BoundedRegularFile.ReadAsync(path,max,ct):
                BoundedRegularFile.ReadWithAcquisitionAsync(path,max,ct,null,fault=="stall"?()=> { try { released.Wait(); }finally { finished.TrySetResult(); } }:null,
                    fault=="changing"?()=> { using var writer=new FileStream(path,FileMode.Open,FileAccess.Write,FileShare.ReadWrite|FileShare.Delete);writer.SetLength(max+1);writer.Flush(true); }:null);
            if(fault=="development-missing") { Should.Throw<FileNotFoundException>(()=>AgentsHostComposition.TryCompose(builder)).FileName.ShouldBe(chosen);builder.Resources.ShouldBeEmpty();return; }
            bool composed=await Task.Run(()=>AgentsHostComposition.TryCompose(builder,cancel.Token,fault=="stall"?TimeSpan.FromMilliseconds(100):TimeSpan.FromSeconds(3),acquire),TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5),TestContext.Current.CancellationToken);
            composed.ShouldBe(fault=="valid");
            if(composed) builder.Resources.Count(value=>value.Name is "eventstore" or "tenants" or "tenants-api" or "parties" or "conversations" or "agents" or "agents-ui").ShouldBe(7);else builder.Resources.ShouldBe(callerResources);
            if(fault=="stall") { finished.Task.IsCompleted.ShouldBeFalse();released.Set();await finished.Task.WaitAsync(TimeSpan.FromSeconds(2),TestContext.Current.CancellationToken); }
        }
        finally { AcceptedLaunchArtifactLease.KernelProofOverride=null;released.Set();Directory.Delete(root,true); }
    }
    [DllImport("libc",EntryPoint="mkfifo",SetLastError=true)] private static extern int Mkfifo(string path,int mode);
}
