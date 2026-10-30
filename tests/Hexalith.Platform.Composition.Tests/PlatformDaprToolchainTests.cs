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
using System.Text.Json.Nodes;

public sealed partial class PlatformAppModelTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelectedDaprHomeConfiguresTheActualCliAndEverySidecarRuntimeAsync(bool signedRun)
    {
        string workspace = Path.Combine(Path.GetTempPath(), "platform-dapr-toolchain-" + Guid.NewGuid().ToString("N"));
        string ambientHome = Path.Combine(workspace, "ambient dapr");
        string selectedHome = signedRun ? Path.Combine(workspace, "signed dapr") : ambientHome;
        string? previousHome = Environment.GetEnvironmentVariable("HEXALITH_DAPR_HOME");
        Directory.CreateDirectory(Path.Combine(selectedHome, ".dapr", "bin"));
        File.WriteAllText(Path.Combine(selectedHome, ".dapr", "bin", OperatingSystem.IsWindows() ? "daprd.exe" : "daprd"), "model fixture");
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
            servers.Length.ShouldBe(7);
            foreach (IResource server in servers)
            {
                DaprSidecarOptions options = server.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options;
                options.RuntimePath.ShouldBe(selectedHome, server.Name);
                ExecutableResource cli = model.Resources.OfType<ExecutableResource>().Single(resource => resource.Name == server.Name + "-dapr-cli");
                cli.Command.ShouldBe(Path.Combine(selectedHome, ".dapr", "bin", OperatingSystem.IsWindows() ? "daprd.exe" : "daprd"));
                foreach (var endpoint in cli.Annotations.OfType<EndpointAnnotation>())
                    endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "127.0.0.1", endpoint.TargetPort ?? endpoint.Port ?? 32000);
                List<object> arguments = [];
                foreach (CommandLineArgsCallbackAnnotation callback in cli.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
                    await callback.Callback(new CommandLineArgsCallbackContext(arguments, TestContext.Current.CancellationToken));
                string[] commandLine = arguments.Select(argument => argument.ToString()!).ToArray();
                commandLine.ShouldNotContain("run");
                commandLine.ShouldNotContain("--runtime-path");
                commandLine.ShouldContain("--enable-mtls");
                int sentry = model.Resources.Single(resource => resource.Name == "dapr-sentry").Annotations.OfType<EndpointAnnotation>().Single(endpoint => endpoint.Name == "grpc").Port!.Value;
                commandLine[Array.IndexOf(commandLine, "--sentry-address") + 1].ShouldBe("localhost:" + sentry);
                commandLine[Array.IndexOf(commandLine, "--placement-host-address") + 1].ShouldBe(options.PlacementHostAddress);
                commandLine[Array.IndexOf(commandLine, "--scheduler-host-address") + 1].ShouldBe(options.SchedulerHostAddress);
                commandLine[Array.IndexOf(commandLine, "--config") + 1].ShouldBe(options.Config);
                string receiptPath = PlatformDaprEndpointSubscriber.ListenerInventoryPath(Path.GetDirectoryName(options.Config!)!, server.Name);
                JsonObject receipt = JsonNode.Parse(File.ReadAllText(receiptPath))!.AsObject();
                receipt.Select(property => property.Key).ShouldBe(["Resource", "Listeners"]);
                receipt["Resource"]!.GetValue<string>().ShouldBe(server.Name);
                JsonObject listeners = receipt["Listeners"]!.AsObject();
                listeners.Count.ShouldBe(4);
                listeners["--dapr-http-port"]!.GetValue<int>().ShouldBe(options.DaprHttpPort!.Value);
                listeners["--dapr-grpc-port"]!.GetValue<int>().ShouldBe(options.DaprGrpcPort!.Value);
                listeners["--dapr-internal-grpc-port"]!.GetValue<int>().ShouldBe(options.DaprInternalGrpcPort!.Value);
                listeners["--metrics-port"]!.GetValue<int>().ShouldBe(options.MetricsPort!.Value);
                string configuration = File.ReadAllText(options.Config!);
                configuration.ShouldContain("nameResolution");
                configuration.ShouldContain("name-resolution.db");
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("HEXALITH_DAPR_HOME", previousHome);
            Directory.Delete(workspace, recursive: true);
        }
    }
    [Fact]
    public async Task MissingSelectedNativeRuntimeRefusesStartupAndPreservesOwnedDiagnosticsAsync()
    {
        string workspace = Path.Combine(Path.GetTempPath(), "platform-missing-runtime-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        string foreign = Path.Combine(workspace, "foreign-resource");
        File.WriteAllText(foreign, "untouched");
        string? previous = Environment.GetEnvironmentVariable("HEXALITH_DAPR_HOME");
        try
        {
            Environment.SetEnvironmentVariable("HEXALITH_DAPR_HOME", Path.Combine(workspace, "missing-runtime"));
            var builder = Builder();
            builder.Configuration["DcpPublisher:CliPath"] = Path.Combine(workspace, "test-dcp-not-started");
            builder.Configuration["DcpPublisher:DashboardPath"] = Path.Combine(workspace, "test-dashboard-not-started");
            builder.AddDapr(options => options.EnableTelemetry = false);
            PlatformAppModel.Compose(builder, Options(active: "eventstore"));
            string config = builder.Resources.Single(resource => resource.Name == "eventstore").Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options.Config!;
            await using (var app = builder.Build())
            {
                // Aspire publishes BeforeStart before the host resolves hosted services, so the workspace is never disposed here.
                var failure = await Should.ThrowAsync<InvalidDataException>(async () => await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true));
                failure.Message.ShouldContain("selected Dapr native runtime is unavailable");
                failure.Message.ShouldContain(Path.Combine(workspace, "missing-runtime"));
            }
            File.Exists(config).ShouldBeTrue("host disposal after a failed start must retain its diagnostics");
            File.ReadAllText(Path.Combine(Path.GetDirectoryName(config)!, "composition-failure.txt")).ShouldContain("selected Dapr native runtime is unavailable");
            File.ReadAllText(foreign).ShouldBe("untouched");
            await CompositionWorkspace.TryDeleteAsync(Path.GetDirectoryName(config)!, CancellationToken.None);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HEXALITH_DAPR_HOME", previous);
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task EndpointSubscriberRetainsDiagnosticsForAnyStartupFailureAsync()
    {
        string workspace = Path.Combine(Path.GetTempPath(), "platform-subscriber-failure-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        try
        {
            var builder = Builder();
            // A sidecar annotation whose generated CLI is absent makes the CLI lookup throw InvalidOperationException.
            builder.AddExecutable("orphan", "sleep", workspace).WithDaprSidecar();
            var model = new DistributedApplicationModel(builder.Resources);
            using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
            var eventing = new DistributedApplicationEventing();
            await new PlatformDaprEndpointSubscriber([], workspace: workspace).SubscribeAsync(eventing, builder.ExecutionContext, TestContext.Current.CancellationToken);
            var failure = await Should.ThrowAsync<InvalidOperationException>(async () => await eventing.PublishAsync(new BeforeStartEvent(services, model), TestContext.Current.CancellationToken).ConfigureAwait(true));
            File.ReadAllText(Path.Combine(workspace, PlatformCompositionWorkspace.FailureMarker)).ShouldBe(failure.Message);
            new PlatformCompositionWorkspace(workspace).Dispose();
            Directory.Exists(workspace).ShouldBeTrue("a marked failure must survive registered-service disposal");
        }
        finally { Directory.Delete(workspace, recursive: true); }
    }

    [Fact]
    public void FailureMarkerWriteNeverReplacesTheCompositionFailure()
    {
        string missing = Path.Combine(Path.GetTempPath(), "platform-missing-workspace-" + Guid.NewGuid().ToString("N"));
        Should.NotThrow(() => PlatformCompositionWorkspace.MarkFailed(missing, "original failure"));
        Directory.Exists(missing).ShouldBeFalse();
    }
}
