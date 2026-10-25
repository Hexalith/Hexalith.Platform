namespace Hexalith.Platform.Composition.Tests;

using System.Text.Json;
using Hexalith.Builds.Tooling.Diagnostics;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.TestEnvironment;
using Hexalith.Builds.Tooling.Workspace;
using Shouldly;

/// <summary>Opt-in real-service proof using declared test commands and descriptor-only infrastructure access.</summary>
public sealed class PlatformTestEnvironmentProvisioningTests
{
    [Fact]
    public async Task DeclaredSuitesPersistIdenticalKeysWithoutObservingPeerDataAsync()
    {
        if (Environment.GetEnvironmentVariable("HEXALITH_PLATFORM_PROVISIONING_LIVE") != "1")
            Assert.Skip("Set HEXALITH_PLATFORM_PROVISIONING_LIVE=1 and HEXALITH_DAPR_HOME for two owned real-service suite commands.");
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "apphost.cs"))) current = current.Parent;
        string root = current!.FullName;
        string evidence = Environment.GetEnvironmentVariable("HEXALITH_PLATFORM_PROVISIONING_EVIDENCE")
            ?? Path.Combine(root, "_bmad-output/implementation-artifacts/evidence/2-4", "live-" + DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssZ", System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(evidence);
        string workspace = Path.Combine(Path.GetTempPath(), "platform-suite-provisioning-" + Guid.NewGuid().ToString("N"));
        string manifest = Path.Combine(root, "eng/test-environment-provisioning/manifest.v2.json");
        string appHost = Path.Combine(root, "references/Hexalith.Builds/src/hosts/Hexalith.Builds.Module.AppHost/bin/Debug/net10.0/Hexalith.Builds.Module.AppHost.dll");
        List<CompositionEngineOptions> owners = [];
        HashSet<string> allocatedRunIds = [];
        try
        {
            foreach (string owner in new[] { root, Path.Combine(root, "references/Hexalith.Builds"), Path.Combine(root, "references/Hexalith.EventStore"), Path.Combine(root, "references/Hexalith.Tenants"), Path.Combine(root, "references/Hexalith.Memories") })
            {
                CompositionProcessResult revision = await CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("git", ["rev-parse", "HEAD"], owner, null), TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
                revision.ExitCode.ShouldBe(0);
                File.AppendAllText(Path.Combine(evidence, "revisions.txt"), owner + " " + revision.Output.Trim() + Environment.NewLine);
            }
            SourceMapping mapping = await WorkspaceRootResolver.ResolveAsync(manifest, WorkspaceMode.Source, TestContext.Current.CancellationToken);
            async Task<int> RunAsync(int index)
            {
                CompositionEngineOptions options = new(appHost, typeof(CompositionEngine).Assembly.Location)
                {
                    SourceMapping = mapping,
                    DaprHome = Environment.GetEnvironmentVariable("HEXALITH_DAPR_HOME"),
                    StateDirectory = Path.Combine(workspace, "state"),
                    WorkspaceRoot = Path.Combine(workspace, "runs"),
                    AppHostLogPath = Path.Combine(evidence, "apphost-" + index + ".log"),
                };
                owners.Add(options);
                StringWriter output = new();
                await using var outputDisposal = output.ConfigureAwait(true);
                int exit = await ModuleCommandExecutionService.ExecuteAsync(ModuleInvocationCommand.Test, manifest, "descriptor-isolation", null, null, ToolOutputFormat.Json, output, TestContext.Current.CancellationToken, typeof(CompositionEngine).Assembly.Location, compositionOptions: options).ConfigureAwait(true);
                string commandOutput = output.ToString();
                File.WriteAllText(Path.Combine(evidence, "command-" + index + ".json"), commandOutput);
                using JsonDocument result = JsonDocument.Parse(commandOutput);
                if (result.RootElement.TryGetProperty("runId", out JsonElement identity) && identity.GetString() is string runId)
                {
                    lock (allocatedRunIds) allocatedRunIds.Add(runId);
                }
                return exit;
            }
            int[] exits = await Task.WhenAll(RunAsync(0), RunAsync(1));
            string proofDirectory = Path.Combine(workspace, "suite-proof");
            if (Directory.Exists(proofDirectory))
            {
                foreach (string receipt in Directory.GetFiles(proofDirectory))
                    File.Copy(receipt, Path.Combine(evidence, Path.GetFileName(receipt)), overwrite: false);
            }
            exits.ShouldAllBe(exit => exit == 0);
            string[] descriptors = allocatedRunIds.Select(runId =>
            {
                string archived = Path.Combine(workspace, "state", "diagnostics", runId, "test-environment.json");
                File.Exists(archived).ShouldBeTrue("The runner must retain its validated descriptor snapshot.");
                string retained = Path.Combine(evidence, runId + ".archived-descriptor.json");
                File.Copy(archived, retained);
                string diagnostics = Path.Combine(evidence, "diagnostics", runId);
                Directory.CreateDirectory(diagnostics);
                foreach (string name in new[] { "test-environment.json", "execution-timing.json", "native.trx", "selected-test.json", "selected-readiness.json", "startup-task-results.json", "selected-plan.json", "selected-source-mapping.json" })
                {
                    string source = Path.Combine(Path.GetDirectoryName(archived)!, name);
                    File.Exists(source).ShouldBeTrue("The runner must retain its validated provisioning observations.");
                    File.Copy(source, Path.Combine(diagnostics, name));
                }
                return retained;
            }).ToArray();
            descriptors.Length.ShouldBe(2);
            foreach (string descriptor in descriptors)
            {
                TestEnvironmentContractReader.ReadDescriptor(descriptor, TestContext.Current.CancellationToken).IsValid.ShouldBeTrue();
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(descriptor));
                string runId = document.RootElement.GetProperty("bindings").GetProperty("run").GetProperty("runId").GetString()!;
                using JsonDocument data = JsonDocument.Parse(File.ReadAllText(Path.Combine(evidence, runId + ".data.json")));
                data.RootElement.GetProperty("observedValue").GetString().ShouldBe(runId);
                data.RootElement.GetProperty("peerKeyVisible").GetBoolean().ShouldBeFalse();
                data.RootElement.GetProperty("persisted").GetBoolean().ShouldBeTrue();
            }
        }
        finally
        {
            List<string> failures = [];
            // Even an assertion or one failed command must attempt cleanup of every identity allocated by this proof.
            foreach (CompositionEngineOptions options in owners.Take(1))
            {
                if (!Directory.Exists(options.StateDirectory)) continue;
                IEnumerable<string> persistedRunIds = Directory.GetFiles(options.StateDirectory, "*.json").Select(Path.GetFileNameWithoutExtension).OfType<string>();
                foreach (string runId in persistedRunIds.Concat(allocatedRunIds).Where(CompositionRunPlanFactory.IsRunId).Distinct(StringComparer.Ordinal))
                {
                    try
                    {
                        CompositionDownResult? stopped = null;
                        // Preserve a first stop diagnostic and verify the existing idempotent owned retry.
                        for (int attempt = 1; attempt <= 2; attempt++)
                        {
                            stopped = await new CompositionEngine(options).DownAsync(runId, CancellationToken.None);
                            File.WriteAllText(Path.Combine(evidence, "cleanup-" + runId + ".attempt-" + attempt + ".json"), JsonSerializer.Serialize(stopped));
                            if (stopped.Result.Outcome.ExitCode == ToolExitCode.Success && stopped.Remaining.IsEmpty) break;
                        }
                        File.WriteAllText(Path.Combine(evidence, "cleanup-" + runId + ".json"), JsonSerializer.Serialize(stopped));
                        if (stopped!.Result.Outcome.ExitCode != ToolExitCode.Success || !stopped.Remaining.IsEmpty) failures.Add(runId);
                    }
                    catch (Exception failure)
                    {
                        failures.Add(runId);
                        File.WriteAllText(Path.Combine(evidence, "cleanup-" + runId + ".json"), JsonSerializer.Serialize(new { runId, failure = failure.GetType().Name }));
                    }
                }
            }
            string claimsDirectory = Path.Combine(workspace, "state", "claims");
            string[] remainingClaims = Directory.Exists(claimsDirectory) ? Directory.GetFiles(claimsDirectory, "*.json") : [];
            string[] remainingWorkspaces = Directory.Exists(Path.Combine(workspace, "runs")) ? Directory.GetDirectories(Path.Combine(workspace, "runs")) : [];
            File.WriteAllText(Path.Combine(evidence, "cleanup-audit.json"), JsonSerializer.Serialize(new { completed = failures.Count == 0 && remainingClaims.Length == 0 && remainingWorkspaces.Length == 0, workspace, failures, remainingClaims, remainingWorkspaces }));
            failures.ShouldBeEmpty("Every owned environment must have verified cleanup.");
            remainingClaims.ShouldBeEmpty();
            remainingWorkspaces.ShouldBeEmpty();
            if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
        }
    }
}
