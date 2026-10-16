namespace Hexalith.Platform.Works.Tests;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Hexalith.Platform.Composition;
using Shouldly;

/// <summary>Checks the Platform-owned target composition and its admission refusals.</summary>
public sealed class WorksPreviewModelTests
{
    /// <summary>A manifest cannot grant a production preview exception or fabricate qualified providers.</summary>
    [Fact]
    public void ProductionIsRefusedBeforeComposingAnyResource()
    {
        IDistributedApplicationBuilder builder = Builder("Production");
        Should.Throw<InvalidOperationException>(() => WorksPreviewModel.Compose(builder)).Message.ShouldContain("profile");
        builder.Resources.ShouldBeEmpty();
        string profile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(profile, WorksProductionProfileTests.Candidate().ToJsonString());
            builder.Configuration["Platform:Works:ProductionProfile"] = profile;
            Should.Throw<InvalidOperationException>(() => WorksPreviewModel.Compose(builder)).Message.ShouldContain("Maintainer");
            builder.Resources.ShouldBeEmpty();
        }
        finally { File.Delete(profile); }
    }

    /// <summary>Platform.Identity remains an explicitly selected synthetic source dependency.</summary>
    [Fact]
    public void IdentityRequiresExplicitSyntheticProvenance()
    {
        IDistributedApplicationBuilder builder = Builder("Development");
        builder.Configuration["Platform:Identity:Enabled"] = "true";
        Should.Throw<InvalidOperationException>(() => WorksPreviewModel.Compose(builder)).Message.ShouldContain("test-only");
        builder.Resources.ShouldBeEmpty();
    }

    /// <summary>Unavailable Agents qualification retains its closed contract.</summary>
    [Fact]
    public void AgentsCompositionStillRefusesUnavailableQualification()
    {
        IDistributedApplicationBuilder builder = Builder("Development");
        builder.Configuration["Platform:Agents:Enabled"] = "true";
        Should.Throw<InvalidOperationException>(() => PlatformAppModel.ComposeEntry(builder)).Message.ShouldContain("EXT-HOST-1");
        builder.Resources.ShouldBeEmpty();
    }

    /// <summary>The target consumes both SDK modes, readiness and named workload identity.</summary>
    [Fact]
    public async Task WorksConsumesSdkReadinessWithoutGatewayStartupCycle()
    {
        IDistributedApplicationBuilder builder = Builder("Development");
        WorksPreviewModel.Compose(builder);
        ProjectResource works = builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == "works");
        Dictionary<string, object> environment = new(StringComparer.Ordinal);
        foreach (EnvironmentCallbackAnnotation callback in works.Annotations.OfType<EnvironmentCallbackAnnotation>())
            await callback.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, works, environment,
                TestContext.Current.CancellationToken));
        environment["Works__Projection__UseSdkDelivery"].ShouldBe("true");
        environment["Works__Recovery__UseSdkRuntime"].ShouldBe("true");
        environment["Authentication__Workload__Audience"].ShouldBe("works");
        environment["Authentication__Workload__AllowedCallers__0"].ShouldBe("eventstore");
        works.Annotations.OfType<HealthCheckAnnotation>().Single().Key.ShouldContain("/ready");
        works.Annotations.OfType<WaitAnnotation>().ShouldNotContain(wait => wait.Resource.Name == "eventstore");
        ProjectResource gateway = builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == "eventstore");
        gateway.Annotations.OfType<HealthCheckAnnotation>().Single().Key.ShouldContain("/ready");
    }

    private static IDistributedApplicationBuilder Builder(string environment)
        => DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = ["--environment=" + environment], ProjectDirectory = WorksProductionProfileTests.Root,
            AssemblyName = typeof(PlatformAppModel).Assembly.GetName().Name, DisableDashboard = true, AllowUnsecuredTransport = true,
        });
}
