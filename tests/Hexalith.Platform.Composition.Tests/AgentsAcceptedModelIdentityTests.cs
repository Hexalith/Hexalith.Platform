using System.Collections;
using System.Text.Json.Nodes;
using Aspire.Hosting;
using Shouldly;

namespace Hexalith.Platform.Composition.Tests;

public sealed class AgentsAcceptedModelIdentityTests
{
    [Fact]
    public void UnchangedAcceptedCopiesAndEqualReplacementRootsComposeTheExactOwnedSnapshot()
    {
        string root=Path.Combine(Path.GetTempPath(),"accepted-model-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            foreach(string path in new[]{"eventstore/src/Hexalith.EventStore/Hexalith.EventStore.csproj","tenants/src/Hexalith.Tenants/Hexalith.Tenants.csproj","tenants/src/Hexalith.Tenants.Api/Hexalith.Tenants.Api.csproj","parties/src/Hexalith.Parties/Hexalith.Parties.csproj","conversations/src/Hexalith.Conversations.Server/Hexalith.Conversations.Server.csproj","agents/src/Hexalith.Agents.Server/Hexalith.Agents.Server.csproj","agents/src/Hexalith.Agents.UI/Hexalith.Agents.UI.csproj"})
            { string project=Path.Combine(root,path);Directory.CreateDirectory(Path.GetDirectoryName(project)!);File.WriteAllText(project,"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>"); }
            AcceptedLaunchArtifactLease.KernelProofOverride=(_,_)=>true;
            var input=AcceptedLaunchFixture.Enroll(AgentsAcceptedDeploymentContextTests.Inputs(root));var accepted=AgentsDeploymentProfile.ValidateAcceptedContext(input.Json,input.Register,input.Packet);
            foreach(var model in new[]{accepted with { },accepted with { Services=new Dictionary<string,string>(accepted.Services) }})
            {
                var builder=DistributedApplication.CreateBuilder(new DistributedApplicationOptions { Args=[],ProjectDirectory=root,AssemblyName=typeof(AgentsAcceptedModelIdentityTests).Assembly.GetName().Name,DisableDashboard=true,AllowUnsecuredTransport=true });builder.Environment.EnvironmentName="Production";using var launchLifetime=AgentsHostComposition.OwnLaunch(builder);
                AgentsHostComposition.Compose(builder,model,null);builder.Resources.Count(value=>value.Name is "eventstore" or "tenants" or "tenants-api" or "parties" or "conversations" or "agents" or "agents-ui").ShouldBe(7);
            }
        }
        finally { AcceptedLaunchArtifactLease.KernelProofOverride=null;Directory.Delete(root,true); }
    }
    [Theory]
    [InlineData("tenant")][InlineData("admin")][InlineData("human")][InlineData("approver")][InlineData("replay")][InlineData("recorder")][InlineData("worker")]
    [InlineData("spool")][InlineData("replicas")][InlineData("failure")][InlineData("engine")][InlineData("observer")][InlineData("parties")][InlineData("conversations")][InlineData("date")][InlineData("root")][InlineData("concurrent")]
    public void AlteredWithCopiesAndMutableDictionariesCannotCarryAcceptedProductionAuthority(string field)
    {
        var input=AgentsAcceptedDeploymentContextTests.Inputs();var genuine=AgentsDeploymentProfile.ValidateAcceptedContext(input.Json,input.Register,input.Packet);
        var changed=field switch
        {
            "tenant"=>genuine with { CurrentTenant="changed" },"admin"=>genuine with { GlobalAdministrator="changed" },"human"=>genuine with { HumanIdentity="changed" },"approver"=>genuine with { ApproverEvidence="changed" },
            "replay"=>genuine with { ReplayReference="changed" },"recorder"=>genuine with { RecorderReference="changed" },"worker"=>genuine with { WorkerReference="changed" },"spool"=>genuine with { SpoolBackend="changed" },
            "replicas"=>genuine with { ReplicaCount=4 },"failure"=>genuine with { FailureModel="changed" },"engine"=>genuine with { ProtectionEngine="changed" },"observer"=>genuine with { ProtectionObserver="changed" },
            "parties"=>genuine with { PartiesBinding="changed" },"conversations"=>genuine with { ConversationsBinding="changed" },"date"=>genuine with { IntegrationDate="2026-11-04" },
            "concurrent"=>genuine with { Services=new ChangingRoots(genuine.Services) },_=>genuine with { Services=new Dictionary<string,string>(genuine.Services) { ["agents"]="changed-root" } }
        };
        var builder=DistributedApplication.CreateBuilder(new DistributedApplicationOptions { Args=[],DisableDashboard=true,AllowUnsecuredTransport=true });builder.Environment.EnvironmentName="Production";using var launchLifetime=AgentsHostComposition.OwnLaunch(builder);
        Should.Throw<InvalidDataException>(()=>AgentsHostComposition.Compose(builder,changed,null));builder.Resources.ShouldBeEmpty();
        genuine.CompositionSnapshot(true).ModelSeal().ShouldBe(genuine.ModelSeal());
        (genuine with { }).CompositionSnapshot(true).ModelSeal().ShouldBe(genuine.ModelSeal());
        var replacement=new Dictionary<string,string>(genuine.Services);var copy=genuine with { Services=replacement };replacement["agents"]="mutated-root";
        Should.Throw<InvalidDataException>(()=>AgentsHostComposition.Compose(builder,copy,null));builder.Resources.ShouldBeEmpty();
        if(genuine.Services is IDictionary<string,string> exposed) Should.Throw<NotSupportedException>(()=>exposed["agents"]="mutated");
    }
    [Theory]
    [InlineData("packet")][InlineData("nested-packet")][InlineData("binding")][InlineData("section")][InlineData("target")][InlineData("date")][InlineData("status")][InlineData("duplicate-binding")][InlineData("identical-target")]
    public void ActualAcceptedContextRejectsEveryAmbiguousInputBeforeTryCompose(string field)
    {
        var (json,register,packet)=AgentsAcceptedDeploymentContextTests.Inputs();
        if(field=="packet") packet=packet.Insert(1,"\"candidateCommit\":\""+new string('f',40)+"\",");
        else if(field=="nested-packet") packet=packet.Replace("\"platformCommit\":","\"platformCommit\":\""+new string('f',40)+"\",\"platformCommit\":",StringComparison.Ordinal);
        else if(field=="binding") register=register.Replace("\"profileSha256\":","\"profileSha256\":\""+new string('f',64)+"\",\"profileSha256\":",StringComparison.Ordinal);
        else if(field=="section") register+="\n"+AcceptedDependencyRecords.Section(register,"EXT-HOST-1");
        else
        {
            string name=field switch { "target" or "identical-target"=>"TargetVersionOrCommit","date"=>"TargetIntegrationDate","status"=>"AcceptedStatus",_=>"HostQualificationBinding" };
            string value=field switch { "target"=>new string('f',40),"identical-target"=>new string('a',40),"date"=>"2026-11-04","status"=>"Uncommitted",_=>"{}" };
            register=register.Replace("### EXT-HOST-1 — Host\n","### EXT-HOST-1 — Host\n| `"+name+"` | `"+value+"` |\n",StringComparison.Ordinal);
        }
        Should.Throw<InvalidDataException>(()=>AgentsDeploymentProfile.ValidateAcceptedContext(json,register,packet));
    }
    [Fact]
    public void UniqueUnknownExtensionsArePreservedInTheGenuineFullAcceptedContext()
    {
        var (json,register,packet)=AgentsAcceptedDeploymentContextTests.Inputs();
        register=register.Replace("### EXT-HOST-1 — Host\n","### EXT-HOST-1 — Host\n| `FutureUniqueExtension` | future-value |\n",StringComparison.Ordinal);
        AgentsDeploymentProfile.ValidateAcceptedContext(json,register,packet).CompositionSnapshot(true).AcceptedContextValidated.ShouldBeTrue();
        register.ShouldContain("| `FutureUniqueExtension` | future-value |");
    }
    private sealed class ChangingRoots(IReadOnlyDictionary<string,string> original):IReadOnlyDictionary<string,string>
    {
        public string this[string key]=>original[key];public IEnumerable<string> Keys=>original.Keys;public IEnumerable<string> Values=>original.Values;public int Count=>original.Count;
        public bool ContainsKey(string key)=>original.ContainsKey(key);public bool TryGetValue(string key,out string value)=>original.TryGetValue(key,out value!);
        public IEnumerator<KeyValuePair<string,string>> GetEnumerator()
        {
            var mutable=new Dictionary<string,string>(original);using var entered=new ManualResetEventSlim();using var done=new ManualResetEventSlim();
            var mutation=Task.Run(()=>{ entered.Wait();mutable["agents"]="concurrently-changed";done.Set(); });
            entered.Set();done.Wait();mutation.GetAwaiter().GetResult();return mutable.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
    }
}
