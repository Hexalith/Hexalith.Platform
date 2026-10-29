namespace Hexalith.Platform.Composition.Tests;

using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Builds.Tooling.Diagnostics;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.TestEnvironment;
using Hexalith.Builds.Tooling.Workspace;
using Shouldly;

/// <summary>Gated actual-service lifecycle proof with resource audits and an independently started peer.</summary>
public sealed class PlatformLocalRunLifecycleTests
{
    [Fact]
    public async Task LocalOutcomesRetainFailuresCleanSuccessAndPreserveIndependentPeerAsync()
    {
        if (Environment.GetEnvironmentVariable("HEXALITH_PLATFORM_LIFECYCLE_LIVE") != "1")
            Assert.Skip("Set HEXALITH_PLATFORM_LIFECYCLE_LIVE=1 and HEXALITH_DAPR_HOME for the real local lifecycle proof.");
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "apphost.cs"))) current = current.Parent;
        string root = current!.FullName;
        string evidence = Environment.GetEnvironmentVariable("HEXALITH_PLATFORM_LIFECYCLE_EVIDENCE")
            ?? Path.Combine(root, "_bmad-output/implementation-artifacts/evidence/2-5", "live-" + DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssZ", System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(evidence);
        string workspace = Path.Combine(Path.GetTempPath(), "platform-local-lifecycle-" + Guid.NewGuid().ToString("N"));
        string original = Path.Combine(root, "eng/test-environment-provisioning/manifest.v2.json");
        string appHost = Path.Combine(root, "references/Hexalith.Builds/src/hosts/Hexalith.Builds.Module.AppHost/bin/Debug/net10.0/Hexalith.Builds.Module.AppHost.dll");
        SourceMapping mapping = await WorkspaceRootResolver.ResolveAsync(original, WorkspaceMode.Source, TestContext.Current.CancellationToken);
        CompositionEngineOptions options = new(appHost, typeof(CompositionEngine).Assembly.Location)
        {
            SourceMapping = mapping,
            DaprHome = Environment.GetEnvironmentVariable("HEXALITH_DAPR_HOME"),
            StateDirectory = Path.Combine(workspace, "state"),
            WorkspaceRoot = Path.Combine(workspace, "runs"),
            PersistAfterParentExit = true,
        };
        List<string> owned = [];
        CompositionRunSession? peer = null;
        try
        {
            foreach (string repository in new[] { root, Path.Combine(root, "references/Hexalith.Builds"), Path.Combine(root, "references/Hexalith.EventStore") })
            {
                CompositionProcessResult revision = await CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("git", ["rev-parse", "HEAD"], repository, null), TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
                revision.ExitCode.ShouldBe(0);
                File.AppendAllText(Path.Combine(evidence, "revisions.txt"), repository + " " + revision.Output.Trim() + Environment.NewLine);
            }
            CompositionStartResult startedPeer = await new CompositionEngine(options).StartAsync(original, TestContext.Current.CancellationToken);
            startedPeer.Result.Outcome.ExitCode.ShouldBe(ToolExitCode.Success, JsonSerializer.Serialize(startedPeer.Result));
            peer = startedPeer.Session.ShouldNotBeNull();
            owned.Add(peer.RunId);
            CompositionRunResources peerBefore = await new CompositionEngine(options).Scanner.FindAsync(peer.RunId, TestContext.Current.CancellationToken);
            peerBefore.ContainerIds.ShouldNotBeEmpty();
            peerBefore.Unverified.ShouldBeFalse();
            CompositionRunState peerState = (await new CompositionRunStateStore(options.StateDirectory).TryReadAsync(peer.RunId, TestContext.Current.CancellationToken)).ShouldNotBeNull();
            int appHostPid = peerState.AppHostProcessId.ShouldNotBeNull();
            int[] stablePeerPids = [.. peer.Readiness.Resources.Where(resource => resource.Kind is "project" or "executable").Select(resource => resource.ProcessId).OfType<int>().Append(appHostPid).Distinct()];
            foreach (int processId in stablePeerPids) peerBefore.ProcessIds.ShouldContain(processId);
            string peerContainer = peer.Plan.RedisContainerName;
            await DockerAsync(["exec", peerContainer, "redis-cli", "SET", "story25-peer-key", "unchanged"]);
            CompositionProcessResult initialPeerValue = await DockerAsync(["exec", peerContainer, "redis-cli", "GET", "story25-peer-key"]);
            initialPeerValue.Output.Trim().ShouldBe("unchanged");
            File.WriteAllText(Path.Combine(evidence, "peer-before.json"), JsonSerializer.Serialize(new { peer.RunId, peerBefore.ContainerIds, peerBefore.ProcessIds, stablePeerPids, observedValue = initialPeerValue.Output.Trim() }));
            foreach (string scenario in new[] { "success", "failure", "timeout", "partial-startup" })
            {
                JsonNode declaration = JsonNode.Parse(File.ReadAllText(original))!;
                JsonNode test = declaration["modules"]![0]!["tests"]![0]!;
                test["name"] = "lifecycle-" + scenario;
                test["executionTimeoutSeconds"] = scenario == "timeout" ? 2 : 120;
                if (scenario == "partial-startup")
                    declaration["modules"]![0]!["lifecycle"]!["tasks"]![0]!["arguments"] = new JsonArray("--fail-after-readiness");
                string manifest = Path.Combine(evidence, "manifest-" + scenario + ".json");
                File.WriteAllText(manifest, declaration.ToJsonString());
                StringWriter output = new();
                await using var outputDisposal = output.ConfigureAwait(true);
                int exit = await ModuleCommandExecutionService.ExecuteAsync(ModuleInvocationCommand.Test, manifest, "lifecycle-" + scenario, null, null, ToolOutputFormat.Json, output, TestContext.Current.CancellationToken, typeof(CompositionEngine).Assembly.Location, compositionOptions: options with { AppHostLogPath = Path.Combine(evidence, "apphost-" + scenario + ".log") });
                File.WriteAllText(Path.Combine(evidence, "command-" + scenario + ".json"), output.ToString());
                using JsonDocument command = JsonDocument.Parse(output.ToString());
                string runId = command.RootElement.GetProperty("runId").GetString()!;
                owned.Add(runId);
                exit.ShouldBe(scenario == "success" ? 0 : scenario == "partial-startup" ? 3 : 4, output.ToString());
                string resultPath = Path.Combine(options.StateDirectory, "diagnostics", runId, "test-result.json");
                TestEnvironmentContractReadResult result = TestEnvironmentContractReader.ReadResult(resultPath, TestContext.Current.CancellationToken);
                result.IsValid.ShouldBeTrue(output.ToString());
                JsonElement terminal = result.Document!.Value.GetProperty("terminal");
                terminal.GetProperty("outcome").GetString().ShouldBe(scenario == "success" ? "passed" : "failed");
                string expectedCause = scenario switch { "success" => "tests-completed", "timeout" => "execution-timeout", "partial-startup" => "startup-failure", _ => "test-failure" };
                terminal.GetProperty("cause").GetString().ShouldBe(expectedCause);
                DateTimeOffset firstAt = terminal.GetProperty("recordedAt").GetDateTimeOffset();
                string initialArchive = Path.Combine(evidence, "initial-diagnostics", scenario);
                Directory.CreateDirectory(initialArchive);
                foreach (string file in Directory.GetFiles(Path.GetDirectoryName(resultPath)!))
                    if (Path.GetExtension(file) is ".json" or ".trx") File.Copy(file, Path.Combine(initialArchive, Path.GetFileName(file)), overwrite: true);
                CompositionRunResources observed = await new CompositionEngine(options).Scanner.FindAsync(runId, TestContext.Current.CancellationToken);
                File.WriteAllText(Path.Combine(evidence, "resources-" + scenario + ".json"), JsonSerializer.Serialize(observed));
                observed.Unverified.ShouldBeFalse();
                if (scenario == "success")
                {
                    observed.IsEmpty.ShouldBeTrue();
                    Directory.Exists(Path.Combine(options.WorkspaceRoot, runId)).ShouldBeFalse();
                }
                else
                {
                    observed.ContainerIds.ShouldNotBeEmpty();
                    Directory.Exists(Path.Combine(options.WorkspaceRoot, runId)).ShouldBeTrue();
                    result.Document.Value.GetProperty("retention").GetProperty("disposition").GetString().ShouldBe("failure-retained");
                    ToolCommandResult listing = await CompositionRunListing.ReadAsync(new CompositionRunStateStore(options.StateDirectory), DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);
                    listing.Environments!.ShouldContain(item => item.OwnerRunId == runId && item.FailureRunIds.Contains(runId));
                    File.WriteAllText(Path.Combine(evidence, "list-" + scenario + ".json"), JsonSerializer.Serialize(listing));
                }
                CompositionDownResult? stopped = null;
                for (int attempt = 1; attempt <= 2; attempt++)
                {
                    stopped = await new CompositionEngine(options).DownAsync(runId, CancellationToken.None);
                    File.WriteAllText(Path.Combine(evidence, "cleanup-" + scenario + "-" + attempt + ".json"), JsonSerializer.Serialize(stopped));
                    if (stopped.Result.Outcome.ExitCode == ToolExitCode.Success && stopped.Remaining.IsEmpty) break;
                }
                stopped!.Result.Outcome.ExitCode.ShouldBe(ToolExitCode.Success);
                stopped.Remaining.IsEmpty.ShouldBeTrue();
                CompositionDownResult repeated = await new CompositionEngine(options).DownAsync(runId, CancellationToken.None);
                File.WriteAllText(Path.Combine(evidence, "cleanup-" + scenario + "-idempotent.json"), JsonSerializer.Serialize(repeated));
                repeated.Result.Outcome.ExitCode.ShouldBe(ToolExitCode.Success);
                repeated.Remaining.IsEmpty.ShouldBeTrue();
                TestEnvironmentContractReadResult cleaned = TestEnvironmentContractReader.ReadResult(resultPath, TestContext.Current.CancellationToken);
                cleaned.IsValid.ShouldBeTrue();
                cleaned.Document!.Value.GetProperty("terminal").GetProperty("recordedAt").GetDateTimeOffset().ShouldBe(firstAt);
                cleaned.Document.Value.GetProperty("terminal").GetProperty("outcome").GetString().ShouldBe(scenario == "success" ? "passed" : "failed");
                cleaned.Document.Value.GetProperty("terminal").GetProperty("cause").GetString().ShouldBe(expectedCause);
                string archive = Path.Combine(evidence, "diagnostics", scenario);
                Directory.CreateDirectory(archive);
                foreach (string file in Directory.GetFiles(Path.GetDirectoryName(resultPath)!))
                    if (Path.GetExtension(file) is ".json" or ".trx") File.Copy(file, Path.Combine(archive, Path.GetFileName(file)), overwrite: true);
                CompositionRunResources peerAfter = await new CompositionEngine(options).Scanner.FindAsync(peer.RunId, TestContext.Current.CancellationToken);
                peerAfter.ContainerIds.ShouldBe(peerBefore.ContainerIds);
                peerAfter.Unverified.ShouldBeFalse();
                foreach (int processId in stablePeerPids) peerAfter.ProcessIds.ShouldContain(processId);
                CompositionProcessResult peerValue = await DockerAsync(["exec", peerContainer, "redis-cli", "GET", "story25-peer-key"]);
                peerValue.Output.Trim().ShouldBe("unchanged");
                File.WriteAllText(Path.Combine(evidence, "peer-audit-" + scenario + ".json"), JsonSerializer.Serialize(new { peer.RunId, peerAfter.ContainerIds, peerAfter.ProcessIds, stablePeerPids, observedValue = peerValue.Output.Trim() }));
            }
        }
        finally
        {
            List<string> cleanupFailures = [];
            IEnumerable<string> persisted = Directory.Exists(options.StateDirectory)
                ? Directory.GetFiles(options.StateDirectory, "*.json").Select(Path.GetFileNameWithoutExtension).OfType<string>().Where(CompositionRunPlanFactory.IsRunId)
                : [];
            cleanupFailures.AddRange(await CleanupOwnersAsync(
                owned.Concat(persisted).Distinct(StringComparer.Ordinal),
                (runId, attempt) => attempt == 1 && peer?.RunId == runId ? new CompositionEngine(options).DownAsync(peer, CancellationToken.None) : new CompositionEngine(options).DownAsync(runId, CancellationToken.None),
                (runId, attempt, down) => File.WriteAllText(Path.Combine(evidence, "final-cleanup-" + runId + "-" + attempt + ".json"), JsonSerializer.Serialize(down))));
            string[] claims = Directory.Exists(Path.Combine(options.StateDirectory, "claims")) ? Directory.GetFiles(Path.Combine(options.StateDirectory, "claims"), "*.json") : [];
            string[] runs = Directory.Exists(options.WorkspaceRoot) ? Directory.GetDirectories(options.WorkspaceRoot) : [];
            File.WriteAllText(Path.Combine(evidence, "cleanup-audit.json"), JsonSerializer.Serialize(new { claims, runs, cleanupFailures, completed = claims.Length == 0 && runs.Length == 0 && cleanupFailures.Count == 0 }));
            cleanupFailures.ShouldBeEmpty();
            claims.ShouldBeEmpty();
            runs.ShouldBeEmpty();
        }

        async Task<CompositionProcessResult> DockerAsync(IReadOnlyList<string> arguments)
        {
            CompositionProcessResult result = await CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("docker", arguments, root, null), TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken).ConfigureAwait(true);
            result.ExitCode.ShouldBe(0);
            return result;
        }
    }

    [Fact]
    public async Task FinalCleanupAttemptsEveryOwnerDespiteFailuresAsync()
    {
        string first = CompositionRunPlanFactory.NewRunId();
        string second = CompositionRunPlanFactory.NewRunId();
        List<string> attempted = [];
        CompositionDownResult removed = new(new ToolCommandResult("completed", ToolOutcome.Passed(), []), new CompositionRunResources([], []));
        IReadOnlyList<string> failures = await CleanupOwnersAsync(
            [first, second],
            (runId, _) =>
            {
                attempted.Add(runId);
                return runId == first ? throw new IOException("cleanup unavailable") : Task.FromResult(removed);
            },
            (_, _, _) => throw new IOException("receipt unavailable"));
        attempted.ShouldContain(second);
        attempted.Count(runId => runId == first).ShouldBe(2);
        failures.ShouldContain(item => item.StartsWith(first, StringComparison.Ordinal));
        failures.ShouldContain(item => item.StartsWith(second, StringComparison.Ordinal));
    }

    private static async Task<IReadOnlyList<string>> CleanupOwnersAsync(IEnumerable<string> owners, Func<string, int, Task<CompositionDownResult>> cleanup, Action<string, int, CompositionDownResult> receipt)
    {
        List<string> failures = [];
        foreach (string runId in owners)
        {
            bool complete = false;
            for (int attempt = 1; attempt <= 2; attempt++)
            {
                CompositionDownResult down;
                try
                {
                    down = await cleanup(runId, attempt).ConfigureAwait(true);
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    failures.Add(runId + ":cleanup-" + attempt);
                    continue;
                }

                try
                {
                    receipt(runId, attempt, down);
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    failures.Add(runId + ":receipt-" + attempt);
                }

                if (down.Result.Outcome.ExitCode == ToolExitCode.Success && down.Remaining.IsEmpty)
                {
                    complete = true;
                    break;
                }
            }

            if (!complete) failures.Add(runId + ":incomplete");
        }

        return failures;
    }
}
