namespace Hexalith.Platform.Composition.Tests;

using Hexalith.Builds.Tooling.Diagnostics;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Shouldly;

public sealed class BuildsSourceStartupTests
{
    [Fact]
    public Task TechnicalEventStoreStartsThroughTheRealBuildsEngineAsync() => StartSelectionAsync("eventstore", ["eventstore"]);

    [Fact]
    public Task DomainTenantsStartsThroughTheRealBuildsEngineAsync() => StartSelectionAsync("tenants",
        ["eventstore", "tenants", "tenants-api", "memories", "memories-access-telemetry-clock", "memories-access-telemetry"]);

    private static async Task StartSelectionAsync(string activeModule, string[] expectedServers)
    {
        if (Environment.GetEnvironmentVariable("HEXALITH_PLATFORM_BUILDS_LIVE_SMOKE") != "1") Assert.Skip("Set HEXALITH_PLATFORM_BUILDS_LIVE_SMOKE=1 and HEXALITH_DAPR_HOME to run the real Builds v2 source path.");
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "apphost.cs"))) directory = directory.Parent;
        string root = directory!.FullName;
        string manifest = Path.Combine(root, "references", PlatformServerBindings.Repository(activeModule), "module", "hexalith.module-manifest.v2.json");
        SourceMapping mapping = await WorkspaceRootResolver.ResolveAsync(manifest, WorkspaceMode.Source, TestContext.Current.CancellationToken);
        string workspace = Path.Combine(Path.GetTempPath(), "platform-builds-smoke-" + Guid.NewGuid().ToString("N"));
        string log = Environment.GetEnvironmentVariable("HEXALITH_PLATFORM_BUILDS_SMOKE_LOG") ?? Path.Combine(workspace, "apphost.log");
        CompositionEngine engine = new(new CompositionEngineOptions(
            Path.Combine(root, "references", "Hexalith.Builds", "src", "hosts", "Hexalith.Builds.Module.AppHost", "bin", "Debug", "net10.0", "Hexalith.Builds.Module.AppHost.dll"),
            typeof(CompositionEngine).Assembly.Location)
        {
            AspireCommand = Environment.GetEnvironmentVariable("HEXALITH_ASPIRE_CLI") ?? "aspire",
            SourceMapping = mapping,
            DaprHome = Environment.GetEnvironmentVariable("HEXALITH_DAPR_HOME"),
            StateDirectory = Path.Combine(workspace, "state"),
            WorkspaceRoot = Path.Combine(workspace, "runs"),
            AppHostLogPath = log,
        });
        CompositionRunSession? session = null;
        try
        {
            CompositionStartResult started = await engine.StartAsync(manifest, TestContext.Current.CancellationToken);
            session = started.Session;
            string diagnostics = string.Join(Environment.NewLine, started.Result.Diagnostics.Select(diagnostic => diagnostic.RuleId + ": " + diagnostic.Message));
            if (!started.IsReady && File.Exists(log)) diagnostics += Environment.NewLine + string.Join(Environment.NewLine, File.ReadLines(log).TakeLast(80));
            started.IsReady.ShouldBeTrue(diagnostics);
            session.ShouldNotBeNull();
            session.Plan.IsPlatformComposition.ShouldBeTrue();
            session.Plan.PlatformActiveModule.ShouldBe(activeModule);
            session.Plan.SourceMapping!.ContentHash.ShouldBe(mapping.ContentHash);
            session.Plan.Modules.ShouldBeEmpty();
            File.Exists(session.Plan.PlatformModelAssemblyPath).ShouldBeTrue();
            session.Readiness.Resources.Where(resource => resource.Kind == "project").Select(resource => resource.Name).OrderBy(name => name, StringComparer.Ordinal)
                .ShouldBe(expectedServers.OrderBy(name => name, StringComparer.Ordinal));
            foreach (string server in expectedServers)
            {
                session.Readiness.Resources.ShouldContain(resource => resource.Name == server && resource.State == "Healthy");
            }
            session.Readiness.Resources.ShouldNotContain(resource => resource.Name.Contains("admin", StringComparison.Ordinal));
        }
        finally
        {
            if (session is not null)
            {
                CompositionDownResult stopped = await engine.DownAsync(session, CancellationToken.None);
                stopped.Result.Outcome.ExitCode.ShouldBe(ToolExitCode.Success, string.Join(Environment.NewLine, stopped.Result.Diagnostics.Select(diagnostic => diagnostic.Message)));
                stopped.Remaining.IsEmpty.ShouldBeTrue();
                Directory.Exists(session.Plan.Workspace).ShouldBeFalse();
            }
        }
    }
}
