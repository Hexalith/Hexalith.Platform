namespace Hexalith.Platform.Composition.Tests;

using System.Security.Cryptography;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Shouldly;

public sealed partial class PlatformAppModelTests
{
    private static CompositionRunPlan RetentionPlan()
    {
        PlatformCompositionOptions options = Options(active: "memories");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return CompositionRunPlanFactory.Create(CompositionRunPlanFactory.NewRunId(), Path.GetTempPath(), Path.GetTempPath(), CompositionPortAllocator.Allocate(1), [], [], options.Mapping,
            [Path.Combine(Root, "references", "Hexalith.Memories", "module", "hexalith.module-manifest.v2.json")], null, typeof(PlatformAppModel).Assembly.Location) with
        {
            SelectedTest = new("memories", "functional-retention", "python3", "tools/verify-access-telemetry-lifecycle.py", new string('a', 64), new string('b', 64), 14400, 900, now, now.AddHours(4), now.AddHours(4), "source") { DaprRuntimeVersion = "1.18.1" },
        };
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RetentionCertificateCoversStartupCeilingAndFullExecutionBudget(bool batch)
    {
        CompositionRunPlan initial = RetentionPlan();
        CompositionRunPlan plan = initial with
        {
            StartupDeadlineUtc = initial.SelectedTest!.RequestedAtUtc.AddHours(2),
            SelectedTests = batch ? [initial.SelectedTest, initial.SelectedTest with { Name = "other", ExecutionTimeoutSeconds = 120 }] : [],
        };
        long executionSeconds = batch ? plan.SelectedTests.Sum(entry => (long)entry.ExecutionTimeoutSeconds + entry.StopTimeoutSeconds) : plan.SelectedTest!.ExecutionTimeoutSeconds;
        PlatformRetentionProfile profile = PlatformRetentionProfile.Select(plan, Root).ShouldNotBeNull();
        profile.PreStartValidityCeiling.ShouldBe(plan.StartupDeadlineUtc!.Value.AddSeconds(executionSeconds + 1));
        profile.PreStartValidityCeiling.ShouldBeGreaterThan(plan.SelectedTest!.CredentialExpiryUtc);
        string directory = Path.Combine(Path.GetTempPath(), "retention-startup-cert-" + Guid.NewGuid().ToString("N"));
        try
        {
            profile.Prepare(directory, 12345);
            using X509Certificate2 certificate = X509Certificate2.CreateFromPemFile(Path.Combine(directory, "retention", "server.crt"), Path.Combine(directory, "retention", "server.key"));
            new DateTimeOffset(certificate.NotAfter.ToUniversalTime()).ShouldBeGreaterThanOrEqualTo(plan.StartupDeadlineUtc.Value.AddSeconds(executionSeconds));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void RetentionProfileIsRunAndMappingBoundAndConfiguresEveryMemoriesService()
    {
        CompositionRunPlan plan = RetentionPlan();
        PlatformRetentionProfile profile = PlatformRetentionProfile.Select(plan, Root).ShouldNotBeNull();
        PlatformRetentionProfile.DaprImage.ShouldBe("ghcr.io/dapr/daprd:1.18.1@sha256:b7f7d296f01f0b4b82bf3c5f087ecf26165ce08caf3e87f94b8c72b9e11873f8");
        PlatformRetentionProfile.PostgresImage.ShouldBe("docker.io/library/postgres:18.6-trixie@sha256:5a5a84b19854a9ffaa54082c166ff4ec27473a361e496e5ea167f298f2da9722");
        profile.ProfileHash.ShouldBe(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(profile.CanonicalJson))));
        profile.ProfileHash.ShouldNotBe("95aa36e8aba8d9914566f6df894c33098739251040dd0cc4b2917f76a994ccdb");
        using JsonDocument canonical = JsonDocument.Parse(profile.CanonicalJson);
        canonical.RootElement.GetProperty("qualificationCredit").GetBoolean().ShouldBeFalse();
        canonical.RootElement.GetProperty("runtimeImage").GetString().ShouldBe(PlatformRetentionProfile.DaprImage);
        canonical.RootElement.GetProperty("postgresqlImage").GetString().ShouldBe(PlatformRetentionProfile.PostgresImage);
        foreach (string server in new[] { "server", "clock", "telemetry" })
        {
            Dictionary<string, string> values = [];
            profile.Configure(values, server);
            values["retentionSeconds"].ShouldBe("3600");
            values["AccessTelemetryLifecycle__Retention"].ShouldBe("01:00:00");
            values["AccessTelemetryLifecycle__ComponentProfileHash"].ShouldBe(profile.ProfileHash);
            values["AccessTelemetryLifecycle__LocalFunctionalRunId"].ShouldBe(plan.RunId);
            values["AccessTelemetryLifecycle__CapabilityEvidence__ValidUntilUtc"].ShouldBe(profile.PreStartValidityCeiling.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            values["AccessTelemetryLifecycle__CapabilityEvidence__ExactVersionPinned"].ShouldBe("true");
        }
        PlatformRetentionProfile.Select(plan with { RunId = "other-run" }, Root).ShouldNotBeNull().ProfileHash.ShouldNotBe(profile.ProfileHash);
        PlatformRetentionProfile.Select(plan with { SelectedTest = null }, Root).ShouldBeNull();
    }

    [Theory]
    [InlineData("runtime")]
    [InlineData("package")]
    [InlineData("foreign-source")]
    public void RetentionProfileRejectsUnsupportedRuntimeModeAndCandidate(string drift)
    {
        CompositionRunPlan plan = RetentionPlan();
        plan = drift switch
        {
            "runtime" => plan with { SelectedTest = plan.SelectedTest! with { DaprRuntimeVersion = "1.18.2" } },
            "package" => plan with { SelectedTest = plan.SelectedTest! with { Mode = "package" } },
            _ => plan with { SourceMapping = plan.SourceMapping! with { Entries = plan.SourceMapping.Entries.Select(entry => entry.Identity == "Hexalith.Memories" ? entry with { Path = "/foreign/Memories" } : entry).ToArray() } },
        };
        Should.Throw<InvalidDataException>(() => PlatformRetentionProfile.Select(plan, Root));
    }

    [Fact]
    public void RetentionComponentUsesV2TlsAndExplicitScopeWhileDefaultStaysRedis()
    {
        CompositionRunPlan plan = RetentionPlan();
        PlatformRetentionProfile profile = PlatformRetentionProfile.Select(plan, Root).ShouldNotBeNull();
        string directory = Path.Combine(Path.GetTempPath(), "retention-render-" + Guid.NewGuid().ToString("N"));
        try
        {
            profile.Prepare(directory, CompositionPortAllocator.Allocate(1).Redis);
            using JsonDocument component = JsonDocument.Parse(profile.RenderComponent(["memories-access-telemetry"]));
            JsonElement spec = component.RootElement.GetProperty("spec");
            spec.GetProperty("type").GetString().ShouldBe("state.postgresql");
            spec.GetProperty("version").GetString().ShouldBe("v2");
            spec.GetProperty("metadata").EnumerateArray().Single(item => item.GetProperty("name").GetString() == "connectionString").GetProperty("value").GetString()!.ShouldContain("sslmode=verify-full");
            component.RootElement.GetProperty("scopes").EnumerateArray().Select(item => item.GetString()).ShouldBe(["memories-access-telemetry"]);
            string hba = File.ReadAllText(Path.Combine(directory, "retention", "pg_hba.conf"));
            hba.ShouldContain("local all all reject");
            hba.ShouldContain("host all all ::0/0 reject");
            File.ReadAllText(Path.Combine(directory, "retention", "pgpass")).ShouldStartWith("127.0.0.1:5432:telemetry:telemetry_observer:");
            spec.GetProperty("metadata").EnumerateArray().Single(item => item.GetProperty("name").GetString() == "connectionString").GetProperty("value").GetString()!.ShouldContain("host=127.0.0.1 ");
            using JsonDocument gate = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "retention", "gate.json")));
            gate.RootElement.GetProperty("state").GetString().ShouldBe("disabled");
            gate.RootElement.GetProperty("profileSha256").GetString().ShouldBeEmpty();
            gate.RootElement.GetProperty("expiresUtcMs").GetInt64().ShouldBe(0);
            PlatformDaprBindings bindings = PlatformDaprBindings.Resolve(PlatformDeclarationSet.Load(Options(active: "memories")), profile);
            bindings.Type("access-telemetry-store").ShouldBe("state.postgresql");
            IReadOnlyDictionary<string, string> rendered = bindings.Render(Path.Combine(Root, "DaprComponents", "reference"), 6379, 8080, "offline-secrets", null);
            using JsonDocument renderedComponent = JsonDocument.Parse(rendered[bindings.FileName("access-telemetry-store")]);
            renderedComponent.RootElement.GetProperty("spec").GetProperty("type").GetString().ShouldBe("state.postgresql");
            renderedComponent.RootElement.GetProperty("scopes").EnumerateArray().Select(value => value.GetString()).ShouldContain("memories-access-telemetry");
            PlatformDaprBindings.Resolve(PlatformDeclarationSet.Load(Options(active: "memories"))).Type("access-telemetry-store").ShouldBe("state.redis");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
    [Theory]
    [InlineData("non-tls", HealthStatus.Unhealthy)]
    [InlineData("untrusted", HealthStatus.Unhealthy)]
    [InlineData("trusted", HealthStatus.Healthy)]
    [InlineData("open-port", HealthStatus.Unhealthy)]
    [InlineData("caller-cancellation", HealthStatus.Unhealthy)]
    public async Task PostgresqlReadinessRequiresTrustedTlsAndPropagatesCallerCancellationAsync(string scenario, HealthStatus expected)
    {
        string directory = Path.Combine(Path.GetTempPath(), "postgres-readiness-" + Guid.NewGuid().ToString("N"));
        try
        {
            using TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            PlatformRetentionProfile profile = PlatformRetentionProfile.Select(RetentionPlan(), Root).ShouldNotBeNull();
            profile.Prepare(directory, port);
            string certificates = Path.Combine(directory, "retention");
            using X509Certificate2 server = X509Certificate2.CreateFromPemFile(Path.Combine(certificates, "server.crt"), Path.Combine(certificates, "server.key"));
            string authorityPath = Path.Combine(certificates, "ca.pem");
            if (scenario == "untrusted")
            {
                using RSA key = RSA.Create(2048);
                CertificateRequest request = new("CN=foreign", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
                using X509Certificate2 foreign = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(5));
                authorityPath = Path.Combine(certificates, "foreign.pem");
                await File.WriteAllTextAsync(authorityPath, foreign.ExportCertificatePem(), TestContext.Current.CancellationToken);
            }

            using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            bound.CancelAfter(TimeSpan.FromSeconds(15));
            using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(bound.Token);
            Task<HealthCheckResult> checking = new PlatformPostgresqlHealthCheck(port, authorityPath).CheckHealthAsync(new HealthCheckContext(), caller.Token);
            try
            {
                using TcpClient client = await listener.AcceptTcpClientAsync(bound.Token);
                NetworkStream stream = client.GetStream();
                byte[] request = new byte[8];
                await stream.ReadExactlyAsync(request, bound.Token);
                request.ShouldBe(new byte[] { 0, 0, 0, 8, 4, 210, 22, 47 });
                if (scenario == "caller-cancellation")
                {
                    await caller.CancelAsync();
                    _ = await Should.ThrowAsync<OperationCanceledException>(() => checking);
                }
                else
                {
                    if (scenario == "non-tls") await stream.WriteAsync("N"u8.ToArray(), bound.Token);
                    else if (scenario != "open-port")
                    {
                        await stream.WriteAsync("S"u8.ToArray(), bound.Token);
                        using SslStream tls = new(stream, leaveInnerStreamOpen: true);
                        try
                        {
                            await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = server, EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13 }, bound.Token);
                        }
                        catch (Exception error) when (scenario == "untrusted" && error is AuthenticationException or IOException)
                        {
                            // A rejected peer may close its TLS handshake before the server finishes.
                        }
                    }

                    (await checking).Status.ShouldBe(expected);
                }
            }
            finally
            {
                await caller.CancelAsync();
                try { _ = await checking; }
                catch (OperationCanceledException) { }
            }
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task RetentionSelectedComposeRunAdoptsGraphCallbacksReadinessAndIsolatedControlPortsAsync()
    {
        string workspace = Path.Combine(Path.GetTempPath(), "retention-selected-graph-" + Guid.NewGuid().ToString("N"));
        try
        {
            CompositionRunPlan initial = RetentionPlan();
            var mapping = initial.SourceMapping! with { Entries = [.. initial.SourceMapping.Entries, new Hexalith.Builds.Tooling.Workspace.SourceMappingEntry("Hexalith.Platform", "source", Root)] };
            CompositionRunPlan plan = CompositionRunPlanFactory.Create(initial.RunId, workspace, workspace, CompositionPortAllocator.Allocate(3), [], [], mapping, initial.PlatformDeclarationPaths, "memories", typeof(PlatformAppModel).Assembly.Location) with { SelectedTest = initial.SelectedTest, DaprHome = Path.Combine(workspace, "dapr-home") };
            await CompositionDaprComponentRenderer.WriteAsync(plan, TestContext.Current.CancellationToken);
            IDistributedApplicationBuilder builder = Builder();
            builder.Environment.EnvironmentName = "Development";
            builder.Configuration["DcpPublisher:CliPath"] = Path.Combine(workspace, "test-dcp-not-started");
            builder.Configuration["DcpPublisher:DashboardPath"] = Path.Combine(workspace, "test-dashboard-not-started");
            builder.AddDapr(options => options.EnableTelemetry = false);
            PlatformAppModel.ComposeRun(builder, plan, CompositionSigningKey.Create(), null);
            builder.Services.ShouldContain(service => service.ServiceType == typeof(ICompositionTestObservationProvider));
            AssertContainerImage(builder, "dapr-sentry", "ghcr.io/dapr/sentry", "1.18.1", "2f98508dff56c75329dbd51674c89f41ce349e06c7744ab2519cb69ba338d41f");
            AssertContainerImage(builder, "dapr-placement-mtls", "ghcr.io/dapr/placement", "1.18.1", "6caf20016d115d4a7f133b9206b739a10abd9f558d76683b27be9ab60f759e26");
            AssertContainerImage(builder, "dapr-scheduler-mtls", "ghcr.io/dapr/scheduler", "1.18.1", "c9bb9ada0cd6a63cd92c26470da1985124e423432af4e39f09b96979fd1059c0");
            AssertContainerImage(builder, "memories-vectors", "docker.io/redis/redis-stack-server", "7.4.0-v8", "798ab84d9f266936b034ab11c4d04a2b8e4b441884c5aa7d17ac951eefdf742a");
            AssertContainerImage(builder, "memories-graph", "falkordb/falkordb", "v4.12.0", "7927eb194df0fadf70e2deb6c20c8178743ec92aacd739c790f8a35ce5dee613");
            IDistributedApplicationBuilder defaults = Builder();
            PlatformAppModel.Compose(defaults, Options(active: "memories"));
            foreach (string name in new[] { "dapr-sentry", "dapr-placement-mtls", "dapr-scheduler-mtls" })
                AssertContainerImage(defaults, name, name == "dapr-sentry" ? "daprio/sentry" : "daprio/dapr", CompositionToolchainPins.DaprRuntimeVersion, null);
            AssertContainerImage(defaults, "memories-vectors", "redis/redis-stack", "latest", null);
            AssertContainerImage(defaults, "memories-graph", "falkordb/falkordb", "latest", null);

            ContainerResource postgres = builder.Resources.OfType<ContainerResource>().Single(resource => resource.Name == "memories-retention-postgresql");
            postgres.Annotations.OfType<HealthCheckAnnotation>().ShouldNotBeEmpty();
            IResource[] memories = builder.Resources.Where(resource => resource.Name is "memories" or "memories-access-telemetry-clock" or "memories-access-telemetry").ToArray();
            memories.Length.ShouldBe(3);
            foreach (IResource resource in memories)
            {
                Dictionary<string, object> environment = [];
                foreach (EnvironmentCallbackAnnotation callback in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
                    await callback.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, resource, environment, TestContext.Current.CancellationToken));
                environment["retentionSeconds"].ShouldBe("3600");
                environment["AccessTelemetryLifecycle__Retention"].ShouldBe("01:00:00");
                environment["HEXALITH_G4_RUN_ID"].ShouldBe(plan.RunId);
                resource.Annotations.OfType<HealthCheckAnnotation>().ShouldNotBeEmpty();
                DaprSidecarOptions sidecar = resource.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options;
                sidecar.PlacementHostAddress.ShouldBe("localhost:" + plan.Ports.Placement);
                sidecar.SchedulerHostAddress.ShouldBe("localhost:" + plan.Ports.Scheduler);
                resource.Annotations.OfType<WaitAnnotation>().ShouldContain(wait => wait.Resource == postgres);
            }
            int sentry = builder.Resources.Single(resource => resource.Name == "dapr-sentry").Annotations.OfType<EndpointAnnotation>().Single(endpoint => endpoint.Name == "grpc").Port!.Value;
            plan.Ports.ToList().ShouldNotContain(sentry);
            sentry.ShouldNotBe(50001);
            string config = File.ReadAllText(Path.Combine(workspace, "platform-components", "accesscontrol.memories.yaml"));
            config.ShouldContain("localhost:" + sentry);
            builder.Resources.Single(resource => resource.Name == "dapr-placement-mtls").Annotations.OfType<EndpointAnnotation>().Single(endpoint => endpoint.Name == "grpc").Port.ShouldBe(plan.Ports.Placement);
            builder.Resources.Single(resource => resource.Name == "dapr-scheduler-mtls").Annotations.OfType<EndpointAnnotation>().Single(endpoint => endpoint.Name == "grpc").Port.ShouldBe(plan.Ports.Scheduler);

            // Run the real integration/subscriber handoff without starting any executable or container.
            string credentials = builder.Resources.Single(resource => resource.Name == "dapr-sentry").Annotations.OfType<ContainerMountAnnotation>().Single(mount => mount.Target == "/var/run/dapr/credentials").Source!;
            Directory.CreateDirectory(Path.Combine(plan.DaprHome, ".dapr", "bin"));
            File.WriteAllText(Path.Combine(plan.DaprHome, ".dapr", "bin", OperatingSystem.IsWindows() ? "daprd.exe" : "daprd"), "model fixture");
            foreach (string file in new[] { "ca.crt", "issuer.crt", "issuer.key" }) File.WriteAllText(Path.Combine(credentials, file), "fixture-" + file);
            int allocatedAppPort = 32000;
            foreach (IResource application in builder.Resources.Where(resource => resource.Annotations.OfType<PlatformServerAnnotation>().Any()))
                foreach (EndpointAnnotation endpoint in application.Annotations.OfType<EndpointAnnotation>())
                    endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "127.0.0.1", endpoint.Port ?? allocatedAppPort++);
            await using DistributedApplication app = builder.Build();
            DistributedApplicationModel model = app.Services.GetRequiredService<DistributedApplicationModel>();
            IDistributedApplicationEventing eventing = app.Services.GetRequiredService<IDistributedApplicationEventing>();
            foreach (IDistributedApplicationEventingSubscriber subscriber in app.Services.GetServices<IDistributedApplicationEventingSubscriber>())
                await subscriber.SubscribeAsync(eventing, builder.ExecutionContext, TestContext.Current.CancellationToken);
            await eventing.PublishAsync(new BeforeStartEvent(app.Services, model), TestContext.Current.CancellationToken);
            ExecutableResource[] selectedSidecars = model.Resources.OfType<ExecutableResource>().Where(resource => resource.Name.EndsWith("-dapr-cli", StringComparison.Ordinal)).ToArray();
            selectedSidecars.Length.ShouldBe(4);
            foreach (ExecutableResource sidecar in selectedSidecars)
            {
                IResource application = model.Resources.Single(resource => resource.Name + "-dapr-cli" == sidecar.Name);
                sidecar.Command.ShouldBe(Path.Combine(plan.DaprHome, ".dapr", "bin", OperatingSystem.IsWindows() ? "daprd.exe" : "daprd"));
                foreach (var endpoint in sidecar.Annotations.OfType<EndpointAnnotation>())
                    endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "127.0.0.1", endpoint.TargetPort ?? endpoint.Port ?? 32000);
                List<object> arguments = [];
                foreach (CommandLineArgsCallbackAnnotation callback in sidecar.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
                    await callback.Callback(new CommandLineArgsCallbackContext(arguments, TestContext.Current.CancellationToken));
                List<string> resolved = [];
                foreach (object argument in arguments)
                    resolved.Add(argument is IValueProvider value ? (await value.GetValueAsync(TestContext.Current.CancellationToken))! : argument.ToString()!);
                resolved.ShouldNotContain("run");
                resolved.ShouldNotContain("--runtime-path");
                resolved.ShouldContain("--enable-mtls");
                resolved[resolved.IndexOf("--sentry-address") + 1].ShouldBe("localhost:" + sentry);
                resolved[resolved.IndexOf("--placement-host-address") + 1].ShouldBe("localhost:" + plan.Ports.Placement);
                resolved[resolved.IndexOf("--scheduler-host-address") + 1].ShouldBe("localhost:" + plan.Ports.Scheduler);
                resolved[resolved.IndexOf("--app-port") + 1].ShouldBe(application.Annotations.OfType<EndpointAnnotation>().Single(endpoint => endpoint.Name == "http").AllocatedEndpoint!.Port.ToString(System.Globalization.CultureInfo.InvariantCulture));
                resolved[resolved.IndexOf("--config") + 1].ShouldStartWith(Path.Combine(workspace, "platform-components", "accesscontrol."));
                resolved.ShouldContain("--resources-path");
                foreach ((string name, string flag) in new[] { ("http", "--dapr-http-port"), ("grpc", "--dapr-grpc-port"), ("metrics", "--metrics-port") })
                    resolved[resolved.IndexOf(flag) + 1].ShouldBe(sidecar.Annotations.OfType<EndpointAnnotation>().Single(endpoint => endpoint.Name == name).Port!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                Dictionary<string, object> environment = [];
                foreach (EnvironmentCallbackAnnotation callback in sidecar.Annotations.OfType<EnvironmentCallbackAnnotation>())
                    await callback.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, sidecar, environment, TestContext.Current.CancellationToken));
                environment["DAPR_TRUST_ANCHORS"].ShouldBe("fixture-ca.crt");
                environment["DAPR_CERT_CHAIN"].ShouldBe("fixture-issuer.crt");
                environment["DAPR_CERT_KEY"].ShouldBe("fixture-issuer.key");
                environment["DAPR_CONTROLPLANE_TRUST_DOMAIN"].ShouldBe("localhost");
                PlatformServerBinding binding = application.Annotations.OfType<PlatformServerAnnotation>().Single().Binding;
                foreach (string token in new[] { "APP_API_TOKEN", "DAPR_API_TOKEN" })
                    if (binding.Environment.TryGetValue(token, out string? value)) environment[token].ShouldBe(value);
            }
        }
        finally { if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true); }
    }

    [Fact]
    public async Task RetentionControlPlanePortsAreExplicitAndDisjointAcrossRunsAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), "retention-control-bindings-" + Guid.NewGuid().ToString("N"));
        try
        {
            List<int> observed = [];
            for (int run = 0; run < 2; run++)
            {
                IDistributedApplicationBuilder builder = Builder();
                string certificates = Path.Combine(directory, run.ToString(System.Globalization.CultureInfo.InvariantCulture));
                builder.Configuration["Dapr:Mtls:CertificateDirectory"] = certificates;
                CompositionRunPorts ports = CompositionPortAllocator.Allocate(1);
                var (sentry, credentials) = DaprSelfHostedMtls.AddSentry(builder, Path.Combine(directory, "sentry.yaml"), Root, certificates, ports.PlacementHealth, "1.18.1");
                var (placement, scheduler) = DaprSelfHostedMtls.AddControlPlane(builder, sentry, credentials, Path.Combine(directory, "scheduler-" + run), ports.Placement, ports.Scheduler, "1.18.1");
                foreach (var control in new[] { placement.Resource, scheduler.Resource })
                {
                    var mount = control.Annotations.OfType<ContainerMountAnnotation>().Single(mount => mount.Target == "/var/run/dapr/credentials");
                    mount.Source.ShouldBe(credentials);
                    mount.IsReadOnly.ShouldBeTrue();
                }
                foreach (var pair in new[] { (sentry.Resource, ports.PlacementHealth), (placement.Resource, ports.Placement), (scheduler.Resource, ports.Scheduler) })
                {
                    EndpointAnnotation endpoint = pair.Item1.Annotations.OfType<EndpointAnnotation>().Single(endpoint => endpoint.Name == "grpc");
                    endpoint.Port.ShouldBe(pair.Item2);
                    endpoint.IsProxied.ShouldBeFalse();
                    observed.ShouldNotContain(pair.Item2);
                    observed.Add(pair.Item2);
                }
                List<object> arguments = [];
                foreach (CommandLineArgsCallbackAnnotation callback in scheduler.Resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
                    await callback.Callback(new CommandLineArgsCallbackContext(arguments, TestContext.Current.CancellationToken));
                arguments.OfType<string>().ShouldContain("--override-broadcast-host-port=localhost:" + ports.Scheduler);
                PlatformRetentionProfile profile = PlatformRetentionProfile.Select(RetentionPlan(), Root).ShouldNotBeNull();
                profile.Prepare(Path.Combine(directory, "rendered-" + run), ports.Redis);
                IReadOnlyDictionary<string, string> files = PlatformDaprBindings.Resolve(PlatformDeclarationSet.Load(Options(active: "memories")), profile).Render(Path.Combine(Root, "DaprComponents/reference"), ports.Redis, ports.UiHttp, "offline-secrets", null, "localhost:" + ports.PlacementHealth);
                files["accesscontrol.memories.yaml"].ShouldContain("localhost:" + ports.PlacementHealth);
            }
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    [Theory]
    [InlineData("matching", true)]
    [InlineData("wrong-digest", false)]
    [InlineData("wrong-loaded-id", false)]
    [InlineData("missing-digest", false)]
    public void RetentionBackendBindsActualLoadedImageInsteadOfRequestedName(string drift, bool accepted)
    {
        string id = "sha256:" + new string('a', 64);
        using JsonDocument container = JsonDocument.Parse(JsonSerializer.Serialize(new { Image = id, Config = new { Image = PlatformRetentionProfile.PostgresImage } }));
        string[] digests = drift == "missing-digest" ? [] : ["postgres@" + (drift == "wrong-digest" ? "sha256:" + new string('b', 64) : PlatformRetentionProfile.PostgresImage.Split('@')[1])];
        using JsonDocument image = JsonDocument.Parse(JsonSerializer.Serialize(new { Id = drift == "wrong-loaded-id" ? "sha256:" + new string('c', 64) : id, RepoDigests = digests }));
        (PlatformRetentionProfile.BoundBackendImage(container.RootElement, image.RootElement) is not null).ShouldBe(accepted);
    }

    private static void AssertContainerImage(IDistributedApplicationBuilder builder, string resource, string image, string tag, string? sha256)
    {
        ContainerImageAnnotation annotation = builder.Resources.Single(value => value.Name == resource).Annotations.OfType<ContainerImageAnnotation>().Single();
        annotation.Image.ShouldBe(image);
        // Aspire drops a floating tag when an immutable digest selects the effective image.
        annotation.Tag.ShouldBe(sha256 is null ? tag : null);
        annotation.SHA256.ShouldBe(sha256);
    }
}
