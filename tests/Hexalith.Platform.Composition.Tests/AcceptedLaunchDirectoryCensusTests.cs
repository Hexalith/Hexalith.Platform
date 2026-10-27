using Aspire.Hosting;
using Shouldly;

namespace Hexalith.Platform.Composition.Tests;

public sealed class AcceptedLaunchDirectoryCensusTests
{
    [Theory]
    [InlineData(10000,true)][InlineData(10001,false)]
    public void ActualAcceptedAcquisitionBoundsEmptyDirectoriesAndDisposesEveryHandle(int directories,bool allowed)
    {
        string root=Path.Combine(Path.GetTempPath(),"accepted-directory-census-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var input=AcceptedLaunchFixture.Enroll(AgentsAcceptedDeploymentContextTests.Inputs(root));string selected=Path.Combine(root,"agents");
            for(int index=0;index<directories;index++) Directory.CreateDirectory(Path.Combine(selected,"empty-"+index));
            input=AcceptedLaunchFixture.Enroll(input);var accepted=AgentsDeploymentProfile.ValidateAcceptedContext(input.Json,input.Register,input.Packet);
            // Framework assembly handles survive first host initialization. Warm only an empty
            // build/dispose lifecycle so the unchanged bound measures actual archive ownership.
            using(var warmup=DistributedApplication.CreateBuilder(new DistributedApplicationOptions { Args=[],ProjectDirectory=root,AssemblyName=typeof(AcceptedLaunchDirectoryCensusTests).Assembly.GetName().Name,DisableDashboard=true,AllowUnsecuredTransport=true }).Build()) { }
            AcceptedLaunchArtifactLease.KernelProofOverride=(_,_)=>true;int before=Directory.GetFiles("/proc/self/fd").Length;
            using var inventory=System.Text.Json.JsonDocument.Parse(input.Packet);var graph=inventory.RootElement.GetProperty("launchInventory");
            if(allowed) { using var lease=AcceptedLaunchArtifactLease.Acquire(graph,"agents",selected,TestContext.Current.CancellationToken);lease.IsDisposed.ShouldBeFalse(); }
            else
            {
                var builder=DistributedApplication.CreateBuilder(new DistributedApplicationOptions { Args=[],ProjectDirectory=root,AssemblyName=typeof(AcceptedLaunchDirectoryCensusTests).Assembly.GetName().Name,DisableDashboard=true,AllowUnsecuredTransport=true });builder.Environment.EnvironmentName="Production";using var launchLifetime=AgentsHostComposition.OwnLaunch(builder);
                Should.Throw<IOException>(()=>AgentsHostComposition.Compose(builder,accepted,null));builder.Resources.ShouldBeEmpty();
            }
            (Directory.GetFiles("/proc/self/fd").Length-before).ShouldBeLessThan(16);
            Directory.GetFiles("/proc/self/fd").Select(path=>new FileInfo(path).LinkTarget).ShouldNotContain(target=>target != null && target.StartsWith(root,StringComparison.Ordinal));
        }
        finally { AcceptedLaunchArtifactLease.KernelProofOverride=null;Directory.Delete(root,true); }
    }
}
