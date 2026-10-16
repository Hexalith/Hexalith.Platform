namespace Hexalith.Platform.Composition.Tests;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

public sealed partial class PlatformAppModelTests
{
    [Fact]
    public void ControlPlaneWithoutOwnedSchedulerDataUsesThePreservedNamedVolume()
    {
        IDistributedApplicationBuilder builder = Builder();
        var sentry = builder.AddContainer("dapr-sentry", "fixture");
        var (_, scheduler) = DaprSelfHostedMtls.AddControlPlane(builder, sentry, Path.Combine(Path.GetTempPath(), "platform-volume-fixture"));
        ContainerMountAnnotation data = scheduler.Resource.Annotations.OfType<ContainerMountAnnotation>().Single(mount => mount.Target == "/var/lock");
        data.Type.ShouldBe(ContainerMountType.Volume);
        data.Source.ShouldBe("hexalith-platform-dapr-scheduler");
        data.IsReadOnly.ShouldBeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelectedDaprHomeConfiguresTheActualCliAndEverySidecarRuntimeAsync(bool signedRun)
    {
        string workspace = Path.Combine(Path.GetTempPath(), "platform-dapr-toolchain-" + Guid.NewGuid().ToString("N"));
        string ambientHome = Path.Combine(workspace, "ambient dapr");
        string selectedHome = signedRun ? Path.Combine(workspace, "signed dapr") : ambientHome;
        string? previousHome = Environment.GetEnvironmentVariable("HEXALITH_DAPR_HOME");
        Directory.CreateDirectory(selectedHome);
        try
        {
            Environment.SetEnvironmentVariable("HEXALITH_DAPR_HOME", ambientHome);
            IDistributedApplicationBuilder builder = Builder();
            builder.Configuration["Dapr:Mtls:CertificateDirectory"] = Path.Combine(workspace, "certificates");
            builder.Configuration["DcpPublisher:CliPath"] = Path.Combine(workspace, "test-dcp-not-started");
            builder.Configuration["DcpPublisher:DashboardPath"] = Path.Combine(workspace, "test-dashboard-not-started");
            builder.AddDapr(options => options.EnableTelemetry = false);
            if (signedRun)
            {
                SourceMapping mapping = Options().Mapping with { Entries = [.. Options().Mapping.Entries, new SourceMappingEntry("Hexalith.Platform", "source", Root)] };
                CompositionRunPlan plan = CompositionRunPlanFactory.Create(CompositionRunPlanFactory.NewRunId(), workspace, selectedHome,
                    CompositionPortAllocator.Allocate(1), [], [], mapping,
                    [Path.Combine(Root, "references", "Hexalith.Tenants", "module", "hexalith.module-manifest.v2.json")], "tenants", typeof(PlatformAppModel).Assembly.Location);
                await CompositionDaprComponentRenderer.WriteAsync(plan, TestContext.Current.CancellationToken);
                PlatformAppModel.ComposeRun(builder, plan, CompositionSigningKey.Create(), null);
            }
            else PlatformAppModel.Compose(builder, Options());

            await using DistributedApplication app = builder.Build();
            if (!signedRun) _ = app.Services.GetRequiredService<PlatformCompositionWorkspace>();
            DistributedApplicationModel model = app.Services.GetRequiredService<DistributedApplicationModel>();
            IDistributedApplicationEventing eventing = app.Services.GetRequiredService<IDistributedApplicationEventing>();
            foreach (IDistributedApplicationEventingSubscriber subscriber in app.Services.GetServices<IDistributedApplicationEventingSubscriber>())
                await subscriber.SubscribeAsync(eventing, builder.ExecutionContext, TestContext.Current.CancellationToken);
            await eventing.PublishAsync(new BeforeStartEvent(app.Services, model), TestContext.Current.CancellationToken);

            IResource[] servers = model.Resources.Where(resource => resource.Annotations.OfType<PlatformServerAnnotation>().Any()).ToArray();
            servers.Length.ShouldBe(6);
            foreach (IResource server in servers)
            {
                DaprSidecarOptions options = server.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options;
                options.RuntimePath.ShouldBe(selectedHome, server.Name);
                ExecutableResource cli = model.Resources.OfType<ExecutableResource>().Single(resource => resource.Name == server.Name + "-dapr-cli");
                cli.Command.ShouldBe(Path.Combine(selectedHome, "tools", OperatingSystem.IsWindows() ? "dapr.exe" : "dapr"));
                List<object> arguments = [];
                foreach (CommandLineArgsCallbackAnnotation callback in cli.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
                    await callback.Callback(new CommandLineArgsCallbackContext(arguments, TestContext.Current.CancellationToken));
                string[] commandLine = arguments.Select(argument => argument.ToString()!).ToArray();
                commandLine.Count(argument => argument == "--runtime-path").ShouldBe(1);
                commandLine[Array.IndexOf(commandLine, "--runtime-path") + 1].ShouldBe(selectedHome);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("HEXALITH_DAPR_HOME", previousHome);
            Directory.Delete(workspace, recursive: true);
        }
    }
}
