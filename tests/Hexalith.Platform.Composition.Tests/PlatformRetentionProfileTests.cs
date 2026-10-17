namespace Hexalith.Platform.Composition.Tests;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Microsoft.Extensions.DependencyInjection;
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

    [Fact]
    public void RetentionProfileIsRunAndMappingBoundAndConfiguresEveryMemoriesService()
    {
        CompositionRunPlan plan = RetentionPlan();
        PlatformRetentionProfile profile = PlatformRetentionProfile.Select(plan, Root).ShouldNotBeNull();
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
            values["AccessTelemetryLifecycle__CapabilityEvidence__ValidUntilUtc"].ShouldBe(plan.SelectedTest!.ExecutionDeadlineUtc.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
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
            PlatformDaprBindings.Resolve(PlatformDeclarationSet.Load(Options(active: "memories"))).Type("access-telemetry-store").ShouldBe("state.redis");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
    [Fact]
    public async Task RetentionSelectedComposeRunAdoptsGraphCallbacksReadinessAndIsolatedControlPortsAsync()
    {
        string workspace = Path.Combine(Path.GetTempPath(), "retention-selected-graph-" + Guid.NewGuid().ToString("N"));
        try
        {
            CompositionRunPlan initial = RetentionPlan();
            var mapping = initial.SourceMapping! with { Entries = [.. initial.SourceMapping.Entries, new Hexalith.Builds.Tooling.Workspace.SourceMappingEntry("Hexalith.Platform", "source", Root)] };
            CompositionRunPlan plan = CompositionRunPlanFactory.Create(initial.RunId, workspace, workspace, CompositionPortAllocator.Allocate(3), [], [], mapping, initial.PlatformDeclarationPaths, "memories", typeof(PlatformAppModel).Assembly.Location) with { SelectedTest = initial.SelectedTest };
            await CompositionDaprComponentRenderer.WriteAsync(plan, TestContext.Current.CancellationToken);
            IDistributedApplicationBuilder builder = Builder();
            PlatformAppModel.ComposeRun(builder, plan, CompositionSigningKey.Create(), null);
            builder.Services.ShouldContain(service => service.ServiceType == typeof(ICompositionTestObservationProvider));
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
                var (sentry, _) = DaprSelfHostedMtls.AddSentry(builder, Path.Combine(directory, "sentry.yaml"), Root, "1.18.1", ports.PlacementHealth);
                var (placement, scheduler) = DaprSelfHostedMtls.AddControlPlane(builder, sentry, certificates, Path.Combine(directory, "scheduler-" + run), "1.18.1", ports.Placement, ports.Scheduler);
                foreach (var pair in new[] { (sentry.Resource, ports.PlacementHealth), (placement.Resource, ports.Placement), (scheduler.Resource, ports.Scheduler) })
                {
                    pair.Item1.Annotations.OfType<EndpointAnnotation>().Single(endpoint => endpoint.Name == "grpc").Port.ShouldBe(pair.Item2);
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

}
