namespace Hexalith.Platform.Composition.Tests;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
            profile.Prepare(directory);
            using JsonDocument component = JsonDocument.Parse(profile.RenderComponent(["memories-access-telemetry"]));
            JsonElement spec = component.RootElement.GetProperty("spec");
            spec.GetProperty("type").GetString().ShouldBe("state.postgresql");
            spec.GetProperty("version").GetString().ShouldBe("v2");
            spec.GetProperty("metadata").EnumerateArray().Single(item => item.GetProperty("name").GetString() == "connectionString").GetProperty("value").GetString()!.ShouldContain("sslmode=verify-full");
            component.RootElement.GetProperty("scopes").EnumerateArray().Select(item => item.GetString()).ShouldBe(["memories-access-telemetry"]);
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
}
