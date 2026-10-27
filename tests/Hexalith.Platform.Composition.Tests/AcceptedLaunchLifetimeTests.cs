using Aspire.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace Hexalith.Platform.Composition.Tests;

public sealed class AcceptedLaunchLifetimeTests
{
    [Theory]
    [InlineData("early-build")][InlineData("options-build")][InlineData("transfer")][InlineData("abandoned")][InlineData("valid")]
    public void ActualAcceptedCompositionOwnsEveryLeaseBeforeBuildAndTransfersOnlyOnSuccess(string boundary)
    {
        string root=Path.Combine(Path.GetTempPath(),"accepted-lifetime-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var input=AcceptedLaunchFixture.Enroll(AgentsAcceptedDeploymentContextTests.Inputs(root));
            var accepted=AgentsDeploymentProfile.ValidateAcceptedContext(input.Json,input.Register,input.Packet);
            AcceptedLaunchArtifactLease.KernelProofOverride=(_,_)=>true;
            var builder=DistributedApplication.CreateBuilder(new DistributedApplicationOptions { Args=[],ProjectDirectory=root,AssemblyName=typeof(AcceptedLaunchLifetimeTests).Assembly.GetName().Name,DisableDashboard=true,AllowUnsecuredTransport=true });builder.Environment.EnvironmentName="Production";
            var launch=AgentsHostComposition.OwnLaunch(builder);
            try
            {
                AgentsHostComposition.Compose(builder,accepted,null);
                var leases=builder.Resources.SelectMany(value=>value.Annotations.OfType<AcceptedLaunchArtifactLease>()).ToArray();leases.Length.ShouldBe(7);leases.ShouldAllBe(value=>!value.IsDisposed);
                if(boundary=="early-build") builder.Services.AddSingleton<IHostApplicationLifetime>(_=>throw new InvalidOperationException("controlled pre-options failure"));
                if(boundary=="options-build") builder.Services.Configure<HostOptions>(_=>throw new InvalidOperationException("controlled build failure"));
                if(boundary=="transfer") builder.Services.AddSingleton<AcceptedLaunchLeaseOwner>(_=>throw new InvalidOperationException("controlled transfer failure"));
                if(boundary is "early-build" or "options-build" or "transfer") Should.Throw<InvalidOperationException>(()=>launch.Build());
                else if(boundary=="abandoned") launch.Dispose();
                else
                {
                    using(var app=launch.Build()) { launch.Dispose();leases.ShouldAllBe(value=>!value.IsDisposed); }
                }
                leases.ShouldAllBe(value=>value.IsDisposed);
            }
            finally { launch.Dispose(); }
        }
        finally { AcceptedLaunchArtifactLease.KernelProofOverride=null;Directory.Delete(root,true); }
    }
}
