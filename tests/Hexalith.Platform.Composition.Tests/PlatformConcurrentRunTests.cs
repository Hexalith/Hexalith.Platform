namespace Hexalith.Platform.Composition.Tests;

using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Builds.Tooling.Diagnostics;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Shouldly;

/// <summary>Opt-in qualification of two real runner-owned reference environments.</summary>
public sealed class PlatformConcurrentRunTests
{
    [Fact]
    public async Task TwoBuildsReferenceRunsPersistIndependentlyAndSurvivePeerCleanupAsync()
    {
        if (Environment.GetEnvironmentVariable("HEXALITH_PLATFORM_CONCURRENT_LIVE") != "1")
            Assert.Skip("Set HEXALITH_PLATFORM_CONCURRENT_LIVE=1 and HEXALITH_DAPR_HOME to qualify two real six-server Builds sessions.");
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "apphost.cs"))) current = current.Parent;
        string root = current!.FullName;
        string evidence = Environment.GetEnvironmentVariable("HEXALITH_PLATFORM_CONCURRENT_EVIDENCE")
            ?? Path.Combine(root, "_bmad-output/implementation-artifacts/evidence/2-3", "live-" + DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssZ", System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(evidence);
        string workspace = Path.Combine(Path.GetTempPath(), "platform-two-runs-" + Guid.NewGuid().ToString("N"));
        string manifest = Path.Combine(root, "references/Hexalith.Tenants/module/hexalith.module-manifest.v2.json");
        List<(CompositionEngine Engine, CompositionRunSession Session)> runs = [];
        try
        {
            foreach (string owner in new[] { root, Path.Combine(root, "references/Hexalith.Builds"), Path.Combine(root, "references/Hexalith.EventStore"), Path.Combine(root, "references/Hexalith.Tenants"), Path.Combine(root, "references/Hexalith.Memories") })
            {
                var revision = await CommandAsync("git", ["rev-parse", "HEAD"], owner);
                File.AppendAllText(Path.Combine(evidence, "revisions.txt"), owner + " " + revision.Output.Trim() + Environment.NewLine);
            }
            SourceMapping mapping = await WorkspaceRootResolver.ResolveAsync(manifest, WorkspaceMode.Source, TestContext.Current.CancellationToken);
            for (int index = 0; index < 2; index++)
            {
                var engine = new CompositionEngine(new CompositionEngineOptions(
                    Path.Combine(root, "references/Hexalith.Builds/src/hosts/Hexalith.Builds.Module.AppHost/bin/Debug/net10.0/Hexalith.Builds.Module.AppHost.dll"), typeof(CompositionEngine).Assembly.Location)
                {
                    AspireCommand = Environment.GetEnvironmentVariable("HEXALITH_ASPIRE_CLI") ?? "aspire",
                    SourceMapping = mapping,
                    DaprHome = Environment.GetEnvironmentVariable("HEXALITH_DAPR_HOME"),
                    StateDirectory = Path.Combine(workspace, "state"),
                    WorkspaceRoot = Path.Combine(workspace, "runs"),
                    AppHostLogPath = Path.Combine(evidence, "apphost-" + index + ".log"),
                });
                CompositionStartResult started = await engine.StartAsync(manifest, TestContext.Current.CancellationToken);
                File.WriteAllText(Path.Combine(evidence, "startup-" + index + ".json"), JsonSerializer.Serialize(started.Result));
                if (started.Session is { } session) runs.Add((engine, session));
                started.IsReady.ShouldBeTrue(string.Join(Environment.NewLine, started.Result.Diagnostics.Select(diagnostic => diagnostic.RuleId + ": " + diagnostic.Message)));
                started.Session.ShouldNotBeNull();
                started.Session.Readiness.Resources.Where(resource => resource.Kind == "project").Select(resource => resource.Name).Order()
                    .ShouldBe(new[] { "eventstore", "tenants", "tenants-api", "memories", "memories-access-telemetry-clock", "memories-access-telemetry" }.Order());
                started.Session.Readiness.Resources.Where(resource => resource.Kind == "project").ShouldAllBe(resource => resource.State == "Healthy");
                File.WriteAllText(Path.Combine(evidence, "readiness-" + index + ".json"), JsonSerializer.Serialize(started.Session.Readiness));
            }
            runs[0].Session.Plan.Ports.ToList().Intersect(runs[1].Session.Plan.Ports.ToList()).ShouldBeEmpty();
            string[] discovery = runs.Select(run => Discovery(run.Session)).ToArray();
            discovery.Distinct().Count().ShouldBe(2);
            foreach (string registry in discovery) File.Exists(registry).ShouldBeTrue();
            // Run labels are the inventory boundary; no developer container or legacy dapr-init service is used.
            List<string[]> certificates = [];
            List<HashSet<string>> containerIds = [];
            List<string[]> stores = [];
            List<string> writableMounts = [];
            HashSet<int> observedPorts = [];
            for (int index = 0; index < runs.Count; index++)
            {
                string runId = runs[index].Session.RunId;
                var listing = await CommandAsync("docker", ["ps", "-q", "--filter", "label=" + CompositionEnvironment.ContainerRunLabel + "=" + runId], root);
                string[] ids = listing.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                ids.ShouldNotBeEmpty();
                containerIds.Add(ids.ToHashSet(StringComparer.Ordinal));
                // Request only ownership and mount metadata: full inspect output includes environment
                // values and can exceed the process boundary's bounded output capture.
                var inspect = await CommandAsync("docker", ["inspect", "--format", "{\"Id\":{{json .Id}},\"Image\":{{json .Config.Image}},\"Mounts\":{{json .Mounts}},\"Ports\":{{json .NetworkSettings.Ports}}}", .. ids], root);
                string inventoryJson = "[" + string.Join(',', inspect.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) + "]";
                using JsonDocument inventory = JsonDocument.Parse(inventoryJson);
                File.WriteAllText(Path.Combine(evidence, "resources-" + index + ".json"), inventoryJson);
                var native = NativeListenerInventory(runs[index].Session.Plan.Workspace,
                    runs[index].Session.Readiness.Resources.Where(resource => resource.Kind == "project").Select(resource => resource.Name));
                File.WriteAllText(Path.Combine(evidence, "native-listeners-" + index + ".json"), JsonSerializer.Serialize(native));
                HashSet<int> boundPorts = runs[index].Session.Plan.Ports.ToList().Concat(native.Values.SelectMany(ports => ports.Values)).ToHashSet();
                foreach (var container in inventory.RootElement.EnumerateArray())
                foreach (var port in container.GetProperty("Ports").EnumerateObject().Where(port => port.Value.ValueKind == JsonValueKind.Array))
                foreach (var binding in port.Value.EnumerateArray())
                    boundPorts.Add(int.Parse(binding.GetProperty("HostPort").GetString()!, System.Globalization.CultureInfo.InvariantCulture));
                observedPorts.Intersect(boundPorts).ShouldBeEmpty();
                observedPorts.UnionWith(boundPorts);
                string[] mounts = inventory.RootElement.EnumerateArray().SelectMany(container => container.GetProperty("Mounts").EnumerateArray())
                    .Where(mount => mount.GetProperty("Destination").GetString() == "/var/run/dapr/credentials")
                    .Select(mount => mount.GetProperty("Source").GetString()!).Distinct().ToArray();
                mounts.Length.ShouldBe(1);
                certificates.Add(new[] { "ca.crt", "issuer.crt" }.Select(file => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(mounts[0], file))))).ToArray());
                foreach (var mount in inventory.RootElement.EnumerateArray().SelectMany(container => container.GetProperty("Mounts").EnumerateArray()).Where(mount => mount.GetProperty("RW").GetBoolean()))
                {
                    string path = mount.GetProperty("Source").GetString()!;
                    if (mount.GetProperty("Destination").GetString() == "/var/run/dapr/credentials") continue;
                    writableMounts.ShouldNotContain(path);
                    writableMounts.Add(path);
                }
                string StoreId(string image) => inventory.RootElement.EnumerateArray().Single(container =>
                    container.GetProperty("Image").GetString()!.Contains(image, StringComparison.Ordinal))
                    .GetProperty("Id").GetString()!;
                string[] runStores = [runs[index].Session.Plan.RedisContainerName, StoreId("redis-stack"), StoreId("falkordb")];
                stores.Add(runStores);
                foreach (string store in runStores)
                {
                    _ = await CommandAsync("docker", ["exec", store, "redis-cli", "SET", "platform-concurrent-same-key", "run-" + index], root);
                    var value = await CommandAsync("docker", ["exec", store, "redis-cli", "GET", "platform-concurrent-same-key"], root);
                    value.Output.Trim().ShouldBe("run-" + index);
                    _ = await CommandAsync("docker", ["exec", store, "redis-cli", "SAVE"], root);
                    var snapshot = await CommandAsync("docker", ["exec", store, "sh", "-c", "test -s /data/dump.rdb"], root);
                    snapshot.ExitCode.ShouldBe(0);
                    File.AppendAllText(Path.Combine(evidence, "persisted-values.txt"), runId + " " + store + " " + value.Output.Trim() + Environment.NewLine);
                }
            }
            for (int index = 0; index < stores.Count; index++)
            foreach (string store in stores[index])
            {
                var persisted = await CommandAsync("docker", ["exec", store, "redis-cli", "GET", "platform-concurrent-same-key"], root);
                persisted.Output.Trim().ShouldBe("run-" + index);
            }
            containerIds[0].Intersect(containerIds[1]).ShouldBeEmpty();
            certificates[0].Intersect(certificates[1]).ShouldBeEmpty();
            var stopped = await runs[0].Engine.DownAsync(runs[0].Session, CancellationToken.None);
            File.WriteAllText(Path.Combine(evidence, "peer-cleanup.json"), JsonSerializer.Serialize(stopped));
            stopped.Result.Outcome.ExitCode.ShouldBe(ToolExitCode.Success);
            stopped.Remaining.IsEmpty.ShouldBeTrue();
            var survivor = runs[1].Session;
            string nonce = CompositionRunPlanFactory.NewRunId();
            await CompositionDocumentStore.WriteAsync(CompositionControlRequest.PathFor(survivor.Plan.Workspace),
                new CompositionControlRequest(CompositionControlRequest.SupportedSchema, survivor.RunId, nonce, "refresh-readiness"), TestContext.Current.CancellationToken);
            using (CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken))
            {
                bound.CancelAfter(TimeSpan.FromSeconds(90));
                CompositionControlResponse? response;
                do
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(200), bound.Token);
                    response = await CompositionDocumentStore.TryReadAsync<CompositionControlResponse>(CompositionControlResponse.PathFor(survivor.Plan.Workspace), bound.Token);
                } while (response?.Nonce != nonce);
                response.RunId.ShouldBe(survivor.RunId);
                response.Success.ShouldBeTrue(response.FailureStep);
                CompositionReadiness refreshed = (await CompositionDocumentStore.TryReadAsync<CompositionReadiness>(survivor.Plan.ReadinessPath, bound.Token)).ShouldNotBeNull();
                refreshed.RefreshId.ShouldBe(nonce);
                refreshed.Resources.Where(resource => resource.Kind == "project").ShouldAllBe(resource => resource.State == "Healthy");
                File.WriteAllText(Path.Combine(evidence, "survivor-readiness.json"), JsonSerializer.Serialize(refreshed));
            }
            foreach (string store in stores[1])
            {
                var survivorValue = await CommandAsync("docker", ["exec", store, "redis-cli", "GET", "platform-concurrent-same-key"], root);
                survivorValue.Output.Trim().ShouldBe("run-1");
                File.AppendAllText(Path.Combine(evidence, "persisted-values.txt"), "survivor " + store + " " + survivorValue.Output.Trim() + Environment.NewLine);
            }
        }
        catch (Exception failure)
        {
            File.WriteAllText(Path.Combine(evidence, "failure.txt"), failure.ToString());
            throw;
        }
        finally
        {
            await CleanupAllAsync(runs.Select(run => (run.Session.RunId, (Func<Task<CompositionDownResult>>)(() => run.Engine.DownAsync(run.Session, CancellationToken.None)))), evidence);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FinalCleanupAttemptsEveryOwnerAndSavesReceiptsBeforeReportingErrorsAsync(bool throws)
    {
        string directory = Path.Combine(Path.GetTempPath(), "platform-cleanup-proof-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        List<string> attempted = [];
        try
        {
            CompositionDownResult Successful() => new(new ToolCommandResult("completed", ToolOutcome.Passed(), []), new CompositionRunResources([], []));
            Func<Task<CompositionDownResult>> first = () =>
            {
                attempted.Add("first");
                if (throws) throw new IOException("owned cleanup failed");
                return Task.FromResult(Successful() with { Remaining = new CompositionRunResources(["owned-leftover"], []) });
            };
            Func<Task<CompositionDownResult>> second = () => { attempted.Add("second"); return Task.FromResult(Successful()); };
            AggregateException failure = await Should.ThrowAsync<AggregateException>(() => CleanupAllAsync([("first", first), ("second", second)], directory));
            failure.InnerExceptions.Count.ShouldBe(1);
            attempted.ShouldBe(["first", "second"]);
            File.Exists(Path.Combine(directory, "cleanup-first.json")).ShouldBeTrue();
            File.Exists(Path.Combine(directory, "cleanup-second.json")).ShouldBeTrue();
            using var receipt = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "cleanup-second.json")));
            receipt.RootElement.GetProperty("Remaining").GetProperty("IsEmpty").GetBoolean().ShouldBeTrue();
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task NativeInventoryIncludesEverySecondaryListenerAndOmitsOtherArguments()
    {
        string directory = Path.Combine(Path.GetTempPath(), "platform-native-inventory-" + Guid.NewGuid().ToString("N"));
        string rendered = Path.Combine(directory, "platform-components");
        Directory.CreateDirectory(rendered);
        try
        {
            int port = 23000;
            foreach (string name in new[] { "eventstore", "secondary" })
            {
                List<object> arguments = ["--app-id", name, "--app-api-token", "must-not-be-recorded"];
                foreach (string flag in new[] { "--dapr-http-port", "--dapr-grpc-port", "--dapr-internal-grpc-port", "--metrics-port" }) arguments.AddRange([flag, port++]);
                File.WriteAllText(PlatformDaprEndpointSubscriber.ListenerInventoryPath(rendered, name), JsonSerializer.Serialize(new { Resource = name, Listeners = await PlatformDaprEndpointSubscriber.NativeListenerPortsAsync(arguments, TestContext.Current.CancellationToken) }));
            }
            var inventory = NativeListenerInventory(directory, ["eventstore", "secondary"]);
            inventory["secondary"].Values.Order().ShouldBe([23004, 23005, 23006, 23007]);
            inventory.Values.SelectMany(ports => ports.Values).Distinct().Count().ShouldBe(8);
            JsonSerializer.Serialize(inventory).ShouldNotContain("must-not-be-recorded");
            File.Delete(PlatformDaprEndpointSubscriber.ListenerInventoryPath(rendered, "secondary"));
            Should.Throw<FileNotFoundException>(() => NativeListenerInventory(directory, ["eventstore", "secondary"]));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> NativeListenerInventory(string workspace, IEnumerable<string> resources)
    {
        Dictionary<string, IReadOnlyDictionary<string, int>> inventory = [];
        foreach (string resource in resources)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(PlatformDaprEndpointSubscriber.ListenerInventoryPath(Path.Combine(workspace, "platform-components"), resource)));
            document.RootElement.GetProperty("Resource").GetString().ShouldBe(resource);
            var ports = JsonSerializer.Deserialize<Dictionary<string, int>>(document.RootElement.GetProperty("Listeners")).ShouldNotBeNull();
            ports.Keys.Order().ShouldBe(new[] { "--dapr-http-port", "--dapr-grpc-port", "--dapr-internal-grpc-port", "--metrics-port" }.Order());
            ports.Values.ShouldAllBe(port => port >= 1 && port <= 65535);
            inventory.Add(resource, ports);
        }
        inventory.Values.SelectMany(ports => ports.Values).Distinct().Count().ShouldBe(inventory.Count * 4);
        return inventory;
    }

    private static async Task CleanupAllAsync(IEnumerable<(string RunId, Func<Task<CompositionDownResult>> Cleanup)> runs, string evidence)
    {
        List<Exception> failures = [];
        foreach (var run in runs)
        {
            string receipt = Path.Combine(evidence, "cleanup-" + run.RunId + ".json");
            try
            {
                var stopped = await run.Cleanup();
                File.WriteAllText(receipt, JsonSerializer.Serialize(stopped));
                if (stopped.Result.Outcome.ExitCode != ToolExitCode.Success || !stopped.Remaining.IsEmpty)
                    throw new InvalidOperationException("Owned cleanup was incomplete for " + run.RunId);
            }
            catch (Exception failure)
            {
                failures.Add(failure);
                if (!File.Exists(receipt))
                {
                    try { File.WriteAllText(receipt, JsonSerializer.Serialize(new { run.RunId, Failure = failure.GetType().Name, failure.Message })); }
                    catch (Exception receiptFailure) { failures.Add(receiptFailure); }
                }
            }
        }
        if (failures.Count > 0) throw new AggregateException("Owned cleanup attempts completed with failures.", failures);
    }

    private static string Discovery(CompositionRunSession session)
    {
        var configuration = JsonNode.Parse(File.ReadAllText(Path.Combine(session.Plan.Workspace, "platform-components", "accesscontrol.eventstore.yaml")))!;
        return configuration["spec"]!["nameResolution"]!["configuration"]!["connectionString"]!.GetValue<string>();
    }

    private static async Task<CompositionProcessResult> CommandAsync(string command, string[] arguments, string root)
    {
        CompositionProcessResult result = await CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo(command, arguments, root, null), TimeSpan.FromMinutes(2), TestContext.Current.CancellationToken);
        result.ExitCode.ShouldBe(0, command + " " + string.Join(' ', arguments) + Environment.NewLine + result.Output + result.ErrorOutput);
        result.TimedOut.ShouldBeFalse();
        return result;
    }
}
