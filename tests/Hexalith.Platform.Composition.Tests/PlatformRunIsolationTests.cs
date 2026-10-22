namespace Hexalith.Platform.Composition.Tests;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Hexalith.Builds.Tooling.Runtime;
using Shouldly;
using System.Text.Json.Nodes;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public sealed partial class PlatformAppModelTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IdenticalSelectionsOwnPortsCredentialsDiscoveryAndStorageAsync(bool signed)
    {
        string directory = Path.Combine(Path.GetTempPath(), "platform-isolation-" + Guid.NewGuid().ToString("N"));
        CompositionWorkspace.CreatePrivateDirectory(directory);
        List<int> listeners = [];
        List<string> writable = [];
        List<string> registries = [];
        try
        {
            for (int index = 0; index < 2; index++)
            {
                var builder = Builder();
                // A configured path is a parent, never the shared issuer directory itself.
                string certificateParent = Path.Combine(directory, "certificate-parent");
                builder.Configuration["Dapr:Mtls:CertificateDirectory"] = certificateParent;
                CompositionRunPlan? plan = null;
                if (signed)
                {
                    string workspace = Path.Combine(directory, "run-" + index);
                    var mapping = Options().Mapping with { Entries = [.. Options().Mapping.Entries, new Hexalith.Builds.Tooling.Workspace.SourceMappingEntry("Hexalith.Platform", "source", Root)] };
                    plan = CompositionRunPlanFactory.Create(CompositionRunPlanFactory.NewRunId(), workspace, directory,
                        CompositionPortAllocator.Allocate(1), [], [], mapping,
                        [Path.Combine(Root, "references/Hexalith.Tenants/module/hexalith.module-manifest.v2.json")], "tenants", typeof(PlatformAppModel).Assembly.Location);
                    await CompositionDaprComponentRenderer.WriteAsync(plan, TestContext.Current.CancellationToken);
                    PlatformAppModel.ComposeRun(builder, plan, CompositionSigningKey.Create(), null);
                }
                else PlatformAppModel.Compose(builder, Options());

                var servers = builder.Resources.Where(resource => resource.Annotations.OfType<PlatformServerAnnotation>().Any()).ToArray();
                servers.Length.ShouldBe(6);
                builder.Resources.ShouldNotContain(resource => resource.Name.Contains("keycloak", StringComparison.OrdinalIgnoreCase));
                var sentry = builder.Resources.Single(resource => resource.Name == "dapr-sentry");
                string credentials = sentry.Annotations.OfType<ContainerMountAnnotation>().Single(mount => mount.Target == "/var/run/dapr/credentials").Source!;
                Path.GetDirectoryName(credentials).ShouldBe(certificateParent);
                writable.ShouldNotContain(credentials);
                writable.Add(credentials);
                Directory.Exists(credentials).ShouldBeTrue();
                if (!OperatingSystem.IsWindows()) File.GetUnixFileMode(credentials).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                File.WriteAllText(Path.Combine(credentials, "issuer.key"), "run-" + index);

                foreach (string name in new[] { "dapr-sentry", "dapr-placement-mtls", "dapr-scheduler-mtls", "redis", "memories-vectors", "memories-graph" })
                {
                    var resource = builder.Resources.Single(resource => resource.Name == name);
                    foreach (int port in resource.Annotations.OfType<EndpointAnnotation>().Select(endpoint => endpoint.Port).OfType<int>())
                    {
                        listeners.ShouldNotContain(port);
                        listeners.Add(port);
                    }
                    resource.Annotations.OfType<ContainerMountAnnotation>().ShouldNotContain(mount => mount.Type == ContainerMountType.Volume);
                    foreach (var mount in resource.Annotations.OfType<ContainerMountAnnotation>().Where(mount => !mount.IsReadOnly))
                    {
                        if (mount.Source == credentials) continue;
                        writable.ShouldNotContain(mount.Source);
                        writable.Add(mount.Source!);
                        Path.GetRelativePath(Root, mount.Source!).ShouldStartWith("..");
                        File.WriteAllText(Path.Combine(mount.Source!, "same-key"), "run-" + index);
                    }
                }
                int placement = builder.Resources.Single(resource => resource.Name == "dapr-placement-mtls").Annotations.OfType<EndpointAnnotation>().Single(endpoint => endpoint.Name == "grpc").Port!.Value;
                int scheduler = builder.Resources.Single(resource => resource.Name == "dapr-scheduler-mtls").Annotations.OfType<EndpointAnnotation>().Single(endpoint => endpoint.Name == "grpc").Port!.Value;
                if (plan is not null) { placement.ShouldBe(plan.Ports.Placement); scheduler.ShouldBe(plan.Ports.Scheduler); }
                foreach (var resource in servers)
                {
                    var sidecar = resource.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options;
                    sidecar.PlacementHostAddress.ShouldBe("localhost:" + placement);
                    sidecar.SchedulerHostAddress.ShouldBe("localhost:" + scheduler);
                    var configuration = JsonNode.Parse(File.ReadAllText(sidecar.Config!))!;
                    configuration["spec"]!["mtls"]!["enabled"]!.GetValue<bool>().ShouldBeTrue();
                    configuration["spec"]!["accessControl"]!["defaultAction"]!.GetValue<string>().ShouldBe("deny");
                    string registry = configuration["spec"]!["nameResolution"]!["configuration"]!["connectionString"]!.GetValue<string>();
                    if (resource == servers[0])
                    {
                        registries.ShouldNotContain(registry);
                        registries.Add(registry);
                        File.WriteAllText(registry, "run-" + index);
                    }
                    else registry.ShouldBe(registries[^1]);
                }
            }
            foreach (string registry in registries) File.ReadAllText(registry).ShouldBe("run-" + registries.IndexOf(registry));
            File.ReadAllText(Path.Combine(writable[0], "issuer.key")).ShouldBe("run-0");
        }
        finally
        {
            foreach (string registry in registries.Where(path => !path.StartsWith(directory, StringComparison.Ordinal))) await CompositionWorkspace.TryDeleteAsync(Path.GetDirectoryName(registry)!, CancellationToken.None);
            await CompositionWorkspace.TryDeleteAsync(directory, CancellationToken.None);
        }
    }

    [Fact]
    public void SecondaryPortAllocationRetriesPersistedOverlapAndFailsClosed()
    {
        CompositionRunPorts persisted = CompositionPortAllocator.Allocate(1);
        CompositionRunPorts extra = PlatformAppModel.AllocateSecondaryPorts(1, persisted);
        extra.ToList().ShouldNotContain(port => persisted.ToList().Contains(port));
        int calls = 0;
        Should.Throw<InvalidOperationException>(() => PlatformAppModel.AllocateSecondaryPorts(1, persisted, _ => { calls++; return persisted; }))
            .Message.ShouldContain("overlap");
        calls.ShouldBe(64);
    }

    [Fact]
    public void SecondaryClaimsOnlyTheAcceptedNonOverlappingAllocation()
    {
        string directory = Path.Combine(Path.GetTempPath(), "platform-secondary-claims-" + Guid.NewGuid().ToString("N"));
        try
        {
            string owner = CompositionRunPlanFactory.NewRunId();
            CompositionRunPorts persisted = CompositionPortAllocator.AllocateOwned(1, owner, directory);
            CompositionRunPorts independent = CompositionPortAllocator.Allocate(1);
            CompositionRunPorts rejected = independent with { Redis = persisted.Scheduler };
            CompositionRunPlan plan = CompositionRunPlanFactory.Create(owner, Path.Combine(directory, "run"), directory, persisted, [], []) with { ResourceClaimStateDirectory = directory };
            int attempts = 0;
            PlatformAppModel.AllocateOwnedSecondaryPorts(1, plan, _ => ++attempts == 1 ? rejected : independent).ShouldBe(independent);
            attempts.ShouldBe(2);
            string peer = CompositionRunPlanFactory.NewRunId();
            var claims = new CompositionResourceClaimStore(directory);
            Should.Throw<CompositionResourceClaimConflictException>(() => claims.Acquire(peer, ["tcp:127.0.0.1:" + persisted.Scheduler])).OwnerRunId.ShouldBe(owner);
            Should.Throw<CompositionResourceClaimConflictException>(() => claims.Acquire(peer, ["tcp:127.0.0.1:" + independent.Redis])).OwnerRunId.ShouldBe(owner);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void CertificateParentRejectsDanglingSymlinksIntoTheCheckoutBeforeAddingResources()
    {
        string directory = Path.Combine(Path.GetTempPath(), "platform-certificate-link-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string target = Path.Combine(Root, "uncreated-certificates-" + Guid.NewGuid().ToString("N"));
        try
        {
            string link = Path.Combine(directory, "link");
            Directory.CreateSymbolicLink(link, target);
            var builder = Builder();
            builder.Configuration["Dapr:Mtls:CertificateDirectory"] = Path.Combine(link, "parent");
            var failure = Should.Throw<InvalidOperationException>(() => DaprSelfHostedMtls.AddSentry(builder, "/unused/sentry.yaml", Root, directory, 24001));
            failure.Message.ShouldContain("must resolve outside the repository");
            failure.Message.ShouldContain(target);
            builder.Resources.ShouldBeEmpty();
            Directory.Exists(target).ShouldBeFalse();
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CertificateParentRejectsMappedOwnerCheckoutOutsidePlatformAsync(bool symlink)
    {
        using PlatformOwnerFixture owner = PlatformOwnerFixture.Copy(Root, "eventstore");
        string directory = Path.Combine(Path.GetTempPath(), "platform-external-owner-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string parent = Path.Combine(owner.Root, "private-certificates");
            if (symlink)
            {
                string link = Path.Combine(directory, "owner-link");
                Directory.CreateSymbolicLink(link, owner.Root);
                parent = Path.Combine(link, "private-certificates");
            }
            var builder = Builder();
            builder.Configuration["Dapr:Mtls:CertificateDirectory"] = parent;
            var options = Options(active: "eventstore");
            options = options with { Mapping = options.Mapping with { Entries = options.Mapping.Entries.Select(entry => entry.Identity == "Hexalith.EventStore" ? entry with { Path = owner.Root } : entry).ToArray() } };
            var failure = Should.Throw<InvalidOperationException>(() => PlatformAppModel.Compose(builder, options));
            failure.Message.ShouldContain("must resolve outside the repository");
            failure.Message.ShouldContain(owner.Root);
            builder.Resources.ShouldBeEmpty();
            Directory.Exists(Path.Combine(owner.Root, "private-certificates")).ShouldBeFalse();
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task ConfiguredCertificateParentDisposalDeletesOnlyOwnedChildAsync()
    {
        string parent = Path.Combine(Path.GetTempPath(), "platform-certificate-parent-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(parent);
        string foreign = Path.Combine(parent, "foreign.txt");
        File.WriteAllText(foreign, "untouched");
        List<DistributedApplication> apps = [];
        List<string> credentials = [];
        try
        {
            for (int index = 0; index < 2; index++)
            {
                var builder = Builder();
                builder.Configuration["Dapr:Mtls:CertificateDirectory"] = parent;
                builder.Configuration["DcpPublisher:CliPath"] = Path.Combine(parent, "test-dcp-not-started");
                builder.Configuration["DcpPublisher:DashboardPath"] = Path.Combine(parent, "test-dashboard-not-started");
                PlatformAppModel.Compose(builder, Options(active: "eventstore"));
                credentials.Add(builder.Resources.Single(resource => resource.Name == "dapr-sentry").Annotations.OfType<ContainerMountAnnotation>().Single(mount => mount.Target == "/var/run/dapr/credentials").Source!);
                File.WriteAllText(Path.Combine(credentials[index], "issuer.key"), "owned-" + index);
                var app = builder.Build();
                apps.Add(app);
                app.Services.GetServices<IHostedService>().OfType<PlatformCompositionWorkspace>().Count().ShouldBe(2);
            }
            await apps[0].DisposeAsync();
            Directory.Exists(credentials[0]).ShouldBeFalse();
            Directory.Exists(parent).ShouldBeTrue();
            File.ReadAllText(foreign).ShouldBe("untouched");
            File.ReadAllText(Path.Combine(credentials[1], "issuer.key")).ShouldBe("owned-1");
            await apps[1].DisposeAsync();
            Directory.Exists(credentials[1]).ShouldBeFalse();
            Directory.Exists(parent).ShouldBeTrue();
            File.ReadAllText(foreign).ShouldBe("untouched");
        }
        finally
        {
            foreach (var app in apps) await app.DisposeAsync();
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public async Task OccupiedSentryPortReportsBindingFailureAndPreservesForeignListenerAsync()
    {
        if (Environment.GetEnvironmentVariable("HEXALITH_PLATFORM_OCCUPIED_PORT_LIVE") != "1")
            Assert.Skip("Set HEXALITH_PLATFORM_OCCUPIED_PORT_LIVE=1 for the bounded Docker listener-conflict proof.");
        string runId = CompositionRunPlanFactory.NewRunId();
        string foreignRun = CompositionRunPlanFactory.NewRunId();
        string name = "platform-occupied-" + runId;
        string foreignName = "platform-foreign-listener-" + foreignRun;
        string workspace = Path.Combine(Path.GetTempPath(), name);
        string evidence = Path.Combine(Root, "_bmad-output/implementation-artifacts/evidence/2-3/occupied-port");
        CompositionWorkspace.CreatePrivateDirectory(workspace);
        Directory.CreateDirectory(evidence);
        int port = CompositionPortAllocator.Allocate(1).Redis;
        try
        {
            // Docker Desktop can publish in a different network namespace from the test process.
            // A separately owned Docker listener guarantees that the actual publish boundary is occupied.
            var foreign = await DockerAsync(["run", "--pull=never", "-d", "--name", foreignName, "--label", CompositionEnvironment.ContainerRunLabel + "=" + foreignRun,
                "--publish", $"127.0.0.1:{port}:6379", CompositionToolchainPins.RedisImageReference]);
            foreign.ExitCode.ShouldBe(0, foreign.Output + foreign.ErrorOutput);
            CompositionProcessResult marker = foreign;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                marker = await DockerAsync(["exec", foreignName, "redis-cli", "SET", "foreign-marker", foreignRun]);
                if (marker.ExitCode == 0) break;
                await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
            }
            marker.ExitCode.ShouldBe(0, marker.Output + marker.ErrorOutput);
            string config = Path.Combine(workspace, "sentry.yaml");
            File.WriteAllText(config, "apiVersion: dapr.io/v1alpha1\nkind: Configuration\nmetadata:\n  name: occupied-port-proof\nspec:\n  mtls:\n    enabled: true\n");
            var builder = Builder();
            var (sentry, _) = DaprSelfHostedMtls.AddSentry(builder, config, Root, workspace, port);
            var endpoint = sentry.Resource.Annotations.OfType<EndpointAnnotation>().Single(endpoint => endpoint.Name == "grpc");
            endpoint.Port.ShouldBe(port);
            List<object> arguments = [];
            foreach (var callback in sentry.Resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
                await callback.Callback(new CommandLineArgsCallbackContext(arguments, TestContext.Current.CancellationToken));
            List<string> command = ["run", "--pull=never", "-d", "--name", name, "--label", CompositionEnvironment.ContainerRunLabel + "=" + runId,
                "--publish", $"127.0.0.1:{endpoint.Port}:{endpoint.TargetPort}", "--entrypoint", "/sentry"];
            foreach (var mount in sentry.Resource.Annotations.OfType<ContainerMountAnnotation>())
                command.AddRange(["--volume", mount.Source + ":" + mount.Target + (mount.IsReadOnly ? ":ro" : string.Empty)]);
            var image = sentry.Resource.Annotations.OfType<ContainerImageAnnotation>().Single();
            command.Add(image.Image + ":" + image.Tag);
            command.AddRange(arguments.Select(argument => argument.ToString()!));
            CompositionProcessResult started = await DockerAsync(command.ToArray());
            string diagnostic = started.Output + started.ErrorOutput;
            string diagnosticPath = Path.Combine(evidence, runId + "-startup.log");
            File.WriteAllText(diagnosticPath, diagnostic);
            started.TimedOut.ShouldBeFalse(diagnostic);
            started.ExitCode.ShouldNotBe(0, diagnostic);
            (diagnostic.Contains("address already in use", StringComparison.OrdinalIgnoreCase)
                || diagnostic.Contains("port is already allocated", StringComparison.OrdinalIgnoreCase)
                || diagnostic.Contains("failed to bind host port", StringComparison.OrdinalIgnoreCase)
                || diagnostic.Contains("ports are not available", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue(diagnostic);
            diagnostic.ShouldContain(port.ToString(System.Globalization.CultureInfo.InvariantCulture));
            await RemoveOwnedAsync(name, runId);
            var retained = await DockerAsync(["exec", foreignName, "redis-cli", "GET", "foreign-marker"]);
            retained.ExitCode.ShouldBe(0, retained.Output + retained.ErrorOutput);
            retained.Output.Trim().ShouldBe(foreignRun);
            using CancellationTokenSource connectionBound = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            connectionBound.CancelAfter(TimeSpan.FromSeconds(5));
            using TcpClient client = new();
            await client.ConnectAsync(IPAddress.Loopback, port, connectionBound.Token);
            await client.GetStream().WriteAsync(System.Text.Encoding.ASCII.GetBytes("PING\r\n"), connectionBound.Token);
            byte[] pong = new byte[32];
            int read = await client.GetStream().ReadAsync(pong, connectionBound.Token);
            System.Text.Encoding.ASCII.GetString(pong, 0, read).ShouldBe("+PONG\r\n");
            File.AppendAllText(diagnosticPath, "\nForeign listener accepted PING and retained its marker after failed startup and candidate-only cleanup.\n");
        }
        finally
        {
            await RemoveOwnedAsync(name, runId);
            await RemoveOwnedAsync(foreignName, foreignRun);
            await CompositionWorkspace.TryDeleteAsync(workspace, CancellationToken.None);
        }

        Task<CompositionProcessResult> DockerAsync(string[] arguments) => CompositionProcess.RunAsync(
            CompositionProcess.CreateStartInfo("docker", arguments, workspace, null), TimeSpan.FromSeconds(20), CancellationToken.None);

        async Task RemoveOwnedAsync(string container, string owner)
        {
            var owned = await DockerAsync(["inspect", "--format", "{{index .Config.Labels \"hexalith.g4.run\"}}", container]);
            if (owned.ExitCode != 0) return;
            owned.Output.Trim().ShouldBe(owner);
            var cleanup = await DockerAsync(["rm", "--force", "--volumes", container]);
            File.WriteAllText(Path.Combine(evidence, owner + "-cleanup.log"), cleanup.Output + cleanup.ErrorOutput);
            cleanup.ExitCode.ShouldBe(0, cleanup.Output + cleanup.ErrorOutput);
        }
    }
}
