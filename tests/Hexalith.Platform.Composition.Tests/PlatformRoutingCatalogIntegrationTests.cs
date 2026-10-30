namespace Hexalith.Platform.Composition.Tests;

using System.Text.Json;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Hexalith.EventStore.Contracts.Routing;
using Shouldly;

[Collection("Platform file entry")]
public class PlatformRoutingCatalogIntegrationTests
{
    [Fact]
    public async Task ActualSidecarsRequireEveryHostToAgreeOnTheRunOwnedCatalog()
    {
        if (Environment.GetEnvironmentVariable("HEXALITH_PLATFORM_LIVE_SMOKE") != "1")
            Assert.Skip("Set HEXALITH_PLATFORM_LIVE_SMOKE=1 for actual Aspire and Dapr activation.");
        var options = await PlatformRoutingCatalogTests.OptionsAsync(includeParties: true);
        string root = options.PlatformRoot;
        string environment = "catalog-live-" + Guid.NewGuid().ToString("N");
        string evidence = Path.Combine(root, "evidence", "3-3", "verification", environment);
        CompositionWorkspace.CreatePrivateDirectory(evidence);
        await SourceMappingMaterializer.WriteAsync(options.Mapping, evidence, TestContext.Current.CancellationToken);
        var launch = new Dictionary<string, string>(SourceMappingMaterializer.Environment(options.Mapping, evidence))
        {
            ["HEXALITH_SOURCE_MAPPING_PATH"] = Path.Combine(evidence, "source-mapping", "mapping.json"),
            ["HEXALITH_SOURCE_MAPPING_HASH"] = options.Mapping.ContentHash,
            ["Platform__EnvironmentId"] = environment,
            ["Platform__ActiveModule"] = "parties",
            ["Platform__DeclarationPath"] = options.DeclarationPaths.Single(),
        };
        string apphost = Path.Combine(root, "apphost.cs");
        bool started = false;
        bool passed = false;
        try
        {
            foreach (var server in PlatformAppModel.Prepare(options))
            {
                var build = await CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("dotnet",
                    ["build", server.ProjectPath!, "-c", "Debug", "-m:1", "-p:GeneratePackageOnBuild=false", "--nologo"], root, launch),
                    TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);
                File.WriteAllText(Path.Combine(evidence, server.ResourceId + "-build.txt"), build.Output + build.ErrorOutput);
                build.ExitCode.ShouldBe(0, server.ResourceId + ": " + build.Output + build.ErrorOutput);
            }
            var start = await RunAsync("start", ["--isolated", "--format", "Json"], launch, TimeSpan.FromMinutes(5));
            started = start.ExitCode == 0;
            start.ExitCode.ShouldBe(0, start.Output + start.ErrorOutput);
            foreach (var server in PlatformAppModel.Prepare(options))
            {
                var ready = await RunAsync("wait", [server.ResourceId, "--timeout", "180"], null, TimeSpan.FromMinutes(4));
                ready.ExitCode.ShouldBe(0, server.ResourceId + ": " + ready.Output + ready.ErrorOutput);
            }
            var described = await RunAsync("describe", ["--include-hidden", "--format", "Json"], null, TimeSpan.FromSeconds(30));
            described.ExitCode.ShouldBe(0, described.Output + described.ErrorOutput);
            string temporary = Path.Combine(Path.GetTempPath(), "hexalith-platform");
            string catalogDirectory = Directory.GetDirectories(temporary).Select(path => Path.Combine(path, environment + "-catalog"))
                .Single(Directory.Exists);
            byte[] canonical = File.ReadAllBytes(Path.Combine(catalogDirectory, "catalog.json"));
            var envelope = RouteCatalogCodec.Decode(canonical);
            envelope.SchemaVersion.ShouldBe(3);
            string digest = RouteCatalogCodec.ComputeRootDigest(envelope);
            var receipts = Directory.GetFiles(catalogDirectory, "*.committed.json");
            receipts.Length.ShouldBeGreaterThanOrEqualTo(4);
            foreach (string path in receipts)
            {
                var readback = JsonSerializer.Deserialize<RouteCatalogGenerationReadback>(File.ReadAllBytes(path));
                readback!.Serving.ShouldBeTrue(); readback.RootDigest.ShouldBe(digest);
                File.Copy(path, Path.Combine(evidence, Path.GetFileName(path)), true);
            }
            File.WriteAllBytes(Path.Combine(evidence, "catalog.json"), canonical);
            File.Copy(Path.Combine(catalogDirectory, "receipt.json"), Path.Combine(evidence, "receipt.json"), true);
            passed = true;
        }
        finally
        {
            if (started && passed) (await RunAsync("stop", [], null, TimeSpan.FromSeconds(30))).ExitCode.ShouldBe(0);
        }

        async Task<CompositionProcessResult> RunAsync(string command, string[] arguments, IReadOnlyDictionary<string, string>? values, TimeSpan timeout)
        {
            var result = await CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("aspire", [command, .. arguments, "--apphost", apphost, "--non-interactive"], root, values), timeout, TestContext.Current.CancellationToken);
            string safe = System.Text.RegularExpressions.Regex.Replace(result.Output + result.ErrorOutput, "([?&]t=)[^\\s\\\"&]+", "$1[redacted]");
            File.WriteAllText(Path.Combine(evidence, command + ".txt"), safe);
            return result;
        }
    }
}
