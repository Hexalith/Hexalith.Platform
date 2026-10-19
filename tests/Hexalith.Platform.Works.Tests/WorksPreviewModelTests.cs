namespace Hexalith.Platform.Works.Tests;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Hexalith.Platform.Composition;
using Hexalith.Platform.Works.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
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
        environment["Authentication__Workload__AllowedCallers__1"].ShouldBe("eventstore-admin");
        environment.Keys.Where(key => key.StartsWith("Authentication__Workload__AllowedCallers__", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal).ShouldBe(["Authentication__Workload__AllowedCallers__0", "Authentication__Workload__AllowedCallers__1"]);
        // Platform's runtime hook and hosting startup are the target's own telemetry composition.
        environment["WORKS_PLATFORM_RUNTIME"].ShouldBe("1");
        environment["DOTNET_STARTUP_HOOKS"].ShouldBe(typeof(WorksRecoveryHostingStartup).Assembly.Location);
        environment["ASPNETCORE_HOSTINGSTARTUPASSEMBLIES"].ShouldBe("Hexalith.Platform.Works.Runtime");
        works.Annotations.OfType<HealthCheckAnnotation>().Single().Key.ShouldBe("works/ready");
        works.Annotations.OfType<WaitAnnotation>().ShouldNotContain(wait => wait.Resource.Name == "eventstore");
        ProjectResource gateway = builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == "eventstore");
        gateway.Annotations.OfType<HealthCheckAnnotation>().Single().Key.ShouldBe("eventstore/ready");
        // Both names resolve to Platform's SDK readiness check, which keeps Degraded distinct; a plain HTTP probe would not.
        await using ServiceProvider services = builder.Services.BuildServiceProvider();
        HealthCheckRegistration[] registrations = [.. services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations];
        foreach (string name in new[] { "works/ready", "eventstore/ready" })
            registrations.Single(registration => registration.Name == name).Factory(services).ShouldBeOfType<SdkReadinessHealthCheck>(name);
    }

    /// <summary>Only the gateway sidecar loads the configuration store, whose keys can override tenant rate limits at runtime.</summary>
    [Fact]
    public void OnlyTheGatewaySidecarReferencesTheConfigurationStore()
    {
        IDistributedApplicationBuilder builder = Builder("Development");
        WorksPreviewModel.Compose(builder);
        Dictionary<string, string[]> components = builder.Resources.OfType<ProjectResource>().ToDictionary(project => project.Name,
            project => project.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar.Annotations.OfType<DaprComponentReferenceAnnotation>()
                .Select(reference => reference.Component.Name).ToArray(), StringComparer.Ordinal);
        components.Keys.Order(StringComparer.Ordinal).ShouldBe(["eventstore", "eventstore-admin", "eventstore-operations", "works"]);
        components.Where(pair => pair.Value.Contains("configstore")).Select(pair => pair.Key).ShouldBe(["eventstore"]);
    }

    /// <summary>Operations waits only for Works to start, not to become healthy, so it stays usable while Works recovery is degraded.</summary>
    [Fact]
    public void OperationsWaitsOnlyForWorksToStart()
    {
        IDistributedApplicationBuilder builder = Builder("Development");
        WorksPreviewModel.Compose(builder);
        ProjectResource operations = builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == "eventstore-operations");
        operations.Annotations.OfType<WaitAnnotation>().Where(wait => wait.Resource.Name == "works").ShouldHaveSingleItem()
            .WaitType.ShouldBe(WaitType.WaitUntilStarted);
    }

    /// <summary>The preview composes explicit, finite development/test EventStore rate limits.</summary>
    [Fact]
    public async Task GatewayComposesExplicitDevelopmentRateLimits()
    {
        IDistributedApplicationBuilder builder = Builder("Development");
        WorksPreviewModel.Compose(builder);
        ProjectResource gateway = builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == "eventstore");
        Dictionary<string, object> environment = new(StringComparer.Ordinal);
        foreach (EnvironmentCallbackAnnotation callback in gateway.Annotations.OfType<EnvironmentCallbackAnnotation>())
            await callback.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, gateway, environment,
                TestContext.Current.CancellationToken));
        Dictionary<string, object> limits = environment.Where(pair => pair.Key.StartsWith("EventStore__RateLimiting__", StringComparison.Ordinal))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        limits.ShouldBe(new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["EventStore__RateLimiting__PermitLimit"] = "8192",
            ["EventStore__RateLimiting__ConsumerPermitLimit"] = "8192",
            ["EventStore__RateLimiting__TenantPermitLimits__anonymous"] = "8192",
            ["EventStore__RateLimiting__ConsumerPermitLimits__anonymous"] = "8192",
        }, ignoreOrder: true);
        ProjectResource works = builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == "works");
        Dictionary<string, object> worksEnvironment = new(StringComparer.Ordinal);
        foreach (EnvironmentCallbackAnnotation callback in works.Annotations.OfType<EnvironmentCallbackAnnotation>())
            await callback.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, works, worksEnvironment,
                TestContext.Current.CancellationToken));
        worksEnvironment.Keys.ShouldNotContain(key => key.StartsWith("EventStore__RateLimiting__", StringComparison.Ordinal));
    }

    /// <summary>Every receiver requires a distinct secret app-channel credential even in Development.</summary>
    [Fact]
    public async Task EachReceiverRequiresItsOwnSecretAppChannelToken()
    {
        IDistributedApplicationBuilder builder = Builder("Development");
        WorksPreviewModel.Compose(builder);
        HashSet<ParameterResource> tokens = [];
        ProjectResource[] projects = [.. builder.Resources.OfType<ProjectResource>()];
        projects.Length.ShouldBe(4);
        foreach (ProjectResource project in projects)
        {
            Dictionary<string, object> environment = new(StringComparer.Ordinal);
            foreach (EnvironmentCallbackAnnotation callback in project.Annotations.OfType<EnvironmentCallbackAnnotation>())
                await callback.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, project, environment,
                    TestContext.Current.CancellationToken));
            environment.TryGetValue("APP_API_TOKEN", out object? value).ShouldBeTrue();
            ParameterResource token = value.ShouldBeOfType<ParameterResource>();
            token.Secret.ShouldBeTrue();
            tokens.Add(token).ShouldBeTrue("Each receiver needs its own credential.");
            builder.Resources.OfType<ParameterResource>().ShouldContain(parameter => ReferenceEquals(parameter, token));
        }
    }

    /// <summary>An explicit qualified producer directory controls every SDK executable entry point.</summary>
    [Fact]
    public void ExplicitSdkSourceRootSelectsEverySdkEntryProject()
    {
        string root = Path.Combine(Path.GetTempPath(), "platform-sdk-source-" + Guid.NewGuid().ToString("N"));
        string[] names = ["Hexalith.EventStore", "Hexalith.EventStore.Operations", "Hexalith.EventStore.Admin.Server.Host"];
        try
        {
            foreach (string name in names)
            {
                string directory = Path.Combine(root, "src", name);
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, name + ".csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");
            }
            IDistributedApplicationBuilder builder = Builder("Development");
            builder.Configuration["Platform:Works:EventStoreSourceRoot"] = root;
            WorksPreviewModel.Compose(builder);
            foreach (ProjectResource project in builder.Resources.OfType<ProjectResource>().Where(project => project.Name != "works"))
                project.GetProjectMetadata().ProjectPath.ShouldStartWith(root + Path.DirectorySeparatorChar);
        }
        finally { Directory.Delete(root, true); }
    }

    /// <summary>A missing explicit producer cannot silently fall back to the sibling checkout.</summary>
    [Fact]
    public void MissingExplicitSdkSourceRootRefusesBeforeResources()
    {
        IDistributedApplicationBuilder builder = Builder("Development");
        string root = Path.Combine(Path.GetTempPath(), "platform-sdk-missing-" + Guid.NewGuid().ToString("N"));
        builder.Configuration["Platform:Works:EventStoreSourceRoot"] = root;
        Should.Throw<FileNotFoundException>(() => WorksPreviewModel.Compose(builder)).FileName.ShouldStartWith(root);
        builder.Resources.ShouldBeEmpty();
    }

    private static IDistributedApplicationBuilder Builder(string environment)
        => DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = ["--environment=" + environment], ProjectDirectory = WorksProductionProfileTests.Root,
            AssemblyName = typeof(PlatformAppModel).Assembly.GetName().Name, DisableDashboard = true, AllowUnsecuredTransport = true,
        });
}
