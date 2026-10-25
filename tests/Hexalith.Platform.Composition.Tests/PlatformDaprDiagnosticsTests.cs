namespace Hexalith.Platform.Composition.Tests;

using System.Diagnostics;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shouldly;

/// <summary>Exercises real public Aspire console and notification services without starting a topology.</summary>
public sealed class PlatformDaprDiagnosticsTests
{
    [Fact]
    public async Task ContainerOwnedSubscriberDrainsOnApplicationDisposalAsync()
    {
        string root = TemporaryDirectory();
        try
        {
            IDistributedApplicationBuilder builder = Builder();
            ExecutableResource native = builder.AddExecutable("memories-dapr-cli", "daprd", root).Resource;
            string directory = Path.Combine(root, "evidence");
            builder.Services.AddSingleton<PlatformDaprDiagnostics>(_ => new(directory, ["memories"], []));
            builder.Services.AddSingleton<IDistributedApplicationEventingSubscriber>(services => services.GetRequiredService<PlatformDaprDiagnostics>());
            await using DistributedApplication app = builder.Build();
            PlatformDaprDiagnostics capture = app.Services.GetRequiredService<PlatformDaprDiagnostics>();
            app.Services.GetServices<IDistributedApplicationEventingSubscriber>().ShouldContain(capture);
            await StartCaptureAsync(app, builder, capture);
            app.Services.GetRequiredService<ResourceLoggerService>().GetLogger(native).LogError("last line before container disposal");
            await app.Services.GetRequiredService<ResourceNotificationService>().PublishUpdateAsync(native, snapshot => snapshot with { ExitCode = 31 });
            await app.DisposeAsync();
            File.ReadAllText(Path.Combine(directory, native.Name + ".console.jsonl")).ShouldContain("last line before container disposal");
            JsonElement final = StateRecords(directory).Last();
            final.GetProperty("Kind").GetString().ShouldBe("capture-stopped");
            final.GetProperty("CaptureFailed").GetBoolean().ShouldBeFalse();
            bool observedExit = StateRecords(directory).Any(record => record.TryGetProperty("ExitCode", out JsonElement code) && code.ValueKind == JsonValueKind.Number && code.GetInt32() == 31);
            observedExit.ShouldBeTrue();
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task DisposalReconcilesPublishedAllocationWhileStateDeliveryIsBlockedAsync()
    {
        string root = TemporaryDirectory();
        try
        {
            IDistributedApplicationBuilder builder = Builder();
            ExecutableResource native = builder.AddExecutable("memories-dapr-cli", "daprd", root).Resource;
            ExecutableResource foreign = builder.AddExecutable("foreign-dapr-cli", "daprd", root).Resource;
            await using DistributedApplication app = builder.Build();
            string directory = Path.Combine(root, "evidence");
            PlatformDaprDiagnostics capture = new(directory, ["memories"], []);
            await using var captureDisposal = capture.ConfigureAwait(true);
            await StartCaptureAsync(app, builder, capture);
            ResourceLoggerService logs = app.Services.GetRequiredService<ResourceLoggerService>();
            ResourceNotificationService notifications = app.Services.GetRequiredService<ResourceNotificationService>();
            string allocated = native.Name + "-allocated";
            // Hold only the collector's admission gate, so its existing background consumer
            // cannot bind this new instance before disposal. Aspire publication/logging stay real.
            object admission = typeof(PlatformDaprDiagnostics).GetField("_watcherGate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(capture)!;
            Task disposal;
            lock (admission)
            {
                logs.GetLogger(native).LogInformation("logical backlog before immediate disposal");
                logs.GetLogger(allocated).LogInformation("allocated backlog before immediate disposal");
                logs.GetLogger(foreign.Name + "-allocated").LogInformation("foreign backlog must stay excluded");
                notifications.PublishUpdateAsync(native, snapshot => snapshot with { State = new("Running", null) }).IsCompletedSuccessfully.ShouldBeTrue();
                notifications.PublishUpdateAsync(foreign, foreign.Name + "-allocated", snapshot => snapshot with { State = new("Exited", null), ExitCode = 99 }).IsCompletedSuccessfully.ShouldBeTrue();
                notifications.PublishUpdateAsync(native, allocated, snapshot => snapshot with { State = new("Exited", null), ExitCode = 23 }).IsCompletedSuccessfully.ShouldBeTrue();
                disposal = capture.DisposeAsync().AsTask();
            }
            await disposal.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            string console = File.ReadAllText(Path.Combine(directory, native.Name + ".console.jsonl"));
            console.ShouldContain("logical backlog before immediate disposal");
            console.ShouldContain("allocated backlog before immediate disposal");
            console.ShouldNotContain("foreign backlog must stay excluded");
            StateRecords(directory).Count(record => record.GetProperty("Kind").GetString() == "log-stream-completed").ShouldBe(2);
            JsonElement final = StateRecords(directory).Last();
            final.GetProperty("Kind").GetString().ShouldBe("capture-stopped");
            final.GetProperty("CaptureFailed").GetBoolean().ShouldBeFalse();
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task LateAllocatedPublicInstanceRetainsBacklogAndLiveOutputAndDrainsTheSubscribedKeyAsync()
    {
        string root = TemporaryDirectory();
        try
        {
            IDistributedApplicationBuilder builder = Builder();
            ExecutableResource native = builder.AddExecutable("memories-dapr-cli", "daprd", root).Resource;
            ExecutableResource foreign = builder.AddExecutable("foreign-dapr-cli", "daprd", root).Resource;
            await using DistributedApplication app = builder.Build();
            string directory = Path.Combine(root, "evidence");
            PlatformDaprDiagnostics capture = new(directory, ["memories"], []);
            await StartCaptureAsync(app, builder, capture);
            ResourceLoggerService logs = app.Services.GetRequiredService<ResourceLoggerService>();
            ResourceNotificationService notifications = app.Services.GetRequiredService<ResourceNotificationService>();
            string consolePath = Path.Combine(directory, native.Name + ".console.jsonl");
            logs.GetLogger(native).LogInformation("logical stream line one");
            await WaitForAsync(() => File.ReadAllText(consolePath).Contains("logical stream line one", StringComparison.Ordinal));

            // DCP assigns a distinct public ResourceId after BeforeStart. Its first log line
            // can already be in the real logger backlog when that notification is delivered.
            string instance = native.Name + "-allocated";
            ILogger allocated = logs.GetLogger(instance);
            allocated.LogInformation("allocated stream backlog line one");
            logs.GetLogger(foreign.Name + "-allocated").LogInformation("foreign allocated output");
            await notifications.PublishUpdateAsync(foreign, foreign.Name + "-allocated", snapshot => snapshot with { State = new(KnownResourceStates.Running, null) });
            await notifications.PublishUpdateAsync(native, instance, snapshot => snapshot with
            {
                State = new(KnownResourceStates.Running, null), Properties = [new("executable.pid", 1729)],
            });
            await WaitForAsync(() => StateRecords(directory).Any(record => record.TryGetProperty("ResourceId", out JsonElement id) && id.GetString() == instance));
            allocated.LogError("allocated stream live final line");
            await notifications.PublishUpdateAsync(native, instance, snapshot => snapshot with { State = new("Terminated", null), ExitCode = 23 });
            await WaitForAsync(() => StateRecords(directory).Any(record => record.TryGetProperty("ResourceId", out JsonElement id) && id.GetString() == instance
                && record.TryGetProperty("ExitCode", out JsonElement code) && code.ValueKind == JsonValueKind.Number && code.GetInt32() == 23));
            await capture.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            string console = File.ReadAllText(consolePath);
            console.ShouldContain("logical stream line one");
            console.ShouldContain("allocated stream backlog line one");
            console.ShouldContain("allocated stream live final line");
            console.ShouldContain(instance);
            console.ShouldNotContain("foreign allocated output");
            JsonElement final = StateRecords(directory).Last();
            final.GetProperty("Kind").GetString().ShouldBe("capture-stopped");
            final.GetProperty("CaptureFailed").GetBoolean().ShouldBeFalse();
            StateRecords(directory).Count(record => record.GetProperty("Kind").GetString() == "log-stream-completed").ShouldBe(2);
            final.GetProperty("TerminalStateObserved").GetBoolean().ShouldBeTrue();
            final.GetProperty("ExitCodeObserved").GetBoolean().ShouldBeTrue();
            final.GetProperty("TerminalEvidence").GetString().ShouldBe("reported-state-and-exit-code");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("Exited")]
    [InlineData("Terminated")]
    public async Task PublicConsoleAndNotificationsRetainPrivateNativeEvidenceAfterStoppingAsync(string terminalState)
    {
        string root = TemporaryDirectory();
        string workspace = Path.Combine(root, "runtime-workspace");
        string evidence = Path.Combine(root, "retained-evidence");
        Directory.CreateDirectory(workspace);
        try
        {
            IDistributedApplicationBuilder builder = Builder();
            ExecutableResource native = builder.AddExecutable("memories-dapr-cli", "daprd", workspace).Resource;
            ExecutableResource foreign = builder.AddExecutable("foreign-dapr-cli", "daprd", workspace).Resource;
            await using DistributedApplication app = builder.Build();
            PlatformDaprDiagnostics capture = new(evidence, ["memories"], ["known-channel-value-123"]);
            await StartCaptureAsync(app, builder, capture);
            ResourceLoggerService logs = app.Services.GetRequiredService<ResourceLoggerService>();
            ResourceNotificationService notifications = app.Services.GetRequiredService<ResourceNotificationService>();
            ILogger logger = logs.GetLogger(native);
            logger.LogInformation("time=2026-10-23T12:00:00Z level=info msg=\"native sidecar running\" scope=dapr.runtime");
            logger.LogInformation("known-channel-value-123 https://user:secret@example.invalid/private");
            logger.LogInformation("DAPR_API_TOKEN=never-retain-env --config private-args");
            logger.LogInformation("-----BEGIN PRIVATE KEY-----\nprivate-key-material\n-----END PRIVATE KEY-----");
            logs.GetLogger(foreign).LogInformation("foreign output must not be captured");
            await notifications.PublishUpdateAsync(native, snapshot => snapshot with
            {
                State = new(KnownResourceStates.Running, null),
                Properties = [new("executable.pid", 1729), new("executable.args", "never-retain-arguments"), new("private", "never-retain-property")],
                EnvironmentVariables = [new("PRIVATE", "never-retain-environment", true)],
                StartTimeStamp = DateTime.UtcNow,
            });
            await WaitForAsync(() => StateRecords(evidence).Any(record => record.TryGetProperty("ReportedPid", out JsonElement pid) && pid.ValueKind == JsonValueKind.Number));
            // ApplicationStopping precedes native shutdown. Capture must remain subscribed through it.
            app.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
            logger.LogError("native fatal: control plane connection refused");
            await notifications.PublishUpdateAsync(native, snapshot => snapshot with
            {
                State = new(terminalState, null), ExitCode = 17, Properties = [], StopTimeStamp = DateTime.UtcNow,
            });
            await capture.DisposeAsync();
            Directory.Delete(workspace, recursive: true);

            string console = File.ReadAllText(Path.Combine(evidence, native.Name + ".console.jsonl"));
            console.ShouldContain("native sidecar running");
            console.ShouldContain("native fatal: control plane connection refused");
            console.ShouldContain("redacted");
            foreach (string excluded in new[] { "known-channel-value-123", "example.invalid", "never-retain", "PRIVATE KEY", "private-key-material", "foreign output" }) console.ShouldNotContain(excluded);
            JsonElement terminal = StateRecords(evidence).Last(record => record.TryGetProperty("ExitCode", out JsonElement code) && code.ValueKind == JsonValueKind.Number);
            terminal.GetProperty("Resource").GetString().ShouldBe(native.Name);
            terminal.GetProperty("ExitCode").GetInt32().ShouldBe(17);
            terminal.GetProperty("State").GetString().ShouldBe(terminalState);
            terminal.GetProperty("LastObservedPid").GetInt32().ShouldBe(1729);
            terminal.GetProperty("ReportedPid").ValueKind.ShouldBe(JsonValueKind.Null);
            terminal.GetProperty("ExitReason").GetString().ShouldBe("unavailable-from-public-resource-snapshot");
            terminal.GetProperty("ReportedStopUtc").ValueKind.ShouldBe(JsonValueKind.String);
            terminal.GetProperty("ElapsedMilliseconds").GetDouble().ShouldBeGreaterThanOrEqualTo(0);
            string metadata = File.ReadAllText(Path.Combine(evidence, native.Name + ".state.jsonl"));
            metadata.ShouldContain("capture-stopped");
            JsonElement receipt = StateRecords(evidence).Last();
            receipt.GetProperty("TerminalStateObserved").GetBoolean().ShouldBeTrue();
            receipt.GetProperty("ExitCodeObserved").GetBoolean().ShouldBeTrue();
            metadata.ShouldNotContain("never-retain");
            Directory.GetFiles(evidence).Length.ShouldBe(3);
            string hostEvidence = File.ReadAllText(Path.Combine(evidence, "apphost.lifecycle.jsonl"));
            hostEvidence.ShouldContain("capture-started");
            hostEvidence.ShouldContain("unavailable-from-managed-lifetime-hooks");
            hostEvidence.ShouldNotContain("never-retain");
            if (!OperatingSystem.IsWindows())
            {
                File.GetUnixFileMode(evidence).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                foreach (string file in Directory.GetFiles(evidence)) File.GetUnixFileMode(file).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task DrainedPublicStreamsDoNotInventTerminalEvidenceFromAStaleRunningSnapshotAsync()
    {
        string root = TemporaryDirectory();
        try
        {
            IDistributedApplicationBuilder builder = Builder();
            ExecutableResource native = builder.AddExecutable("memories-dapr-cli", "daprd", root).Resource;
            await using DistributedApplication app = builder.Build();
            string directory = Path.Combine(root, "evidence");
            PlatformDaprDiagnostics capture = new(directory, ["memories"], []);
            await StartCaptureAsync(app, builder, capture);
            await app.Services.GetRequiredService<ResourceNotificationService>().PublishUpdateAsync(native, snapshot => snapshot with
            {
                State = new(KnownResourceStates.Running, null), Properties = [new("executable.pid", 1729)],
            });
            await capture.DisposeAsync();
            JsonElement final = StateRecords(directory).Last();
            final.GetProperty("Kind").GetString().ShouldBe("capture-stopped");
            final.GetProperty("CaptureFailed").GetBoolean().ShouldBeFalse();
            final.GetProperty("TerminalStateObserved").GetBoolean().ShouldBeFalse();
            final.GetProperty("ExitCodeObserved").GetBoolean().ShouldBeFalse();
            final.GetProperty("TerminalEvidence").GetString().ShouldBe("unavailable-final-snapshot-nonterminal");
            StateRecords(directory).Any(record => record.TryGetProperty("State", out JsonElement state) && state.GetString() == "Running").ShouldBeTrue();
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task PublicLogTailIsBoundedAndDisclosesTruncationWhilePreservingTheLastNativeErrorAsync()
    {
        string root = TemporaryDirectory();
        try
        {
            IDistributedApplicationBuilder builder = Builder();
            ExecutableResource native = builder.AddExecutable("memories-dapr-cli", "daprd", root).Resource;
            await using DistributedApplication app = builder.Build();
            PlatformDaprDiagnostics capture = new(Path.Combine(root, "evidence"), ["memories"], []);
            await StartCaptureAsync(app, builder, capture);
            ResourceLoggerService logs = app.Services.GetRequiredService<ResourceLoggerService>();
            ILogger logger = logs.GetLogger(native);
            for (int index = 0; index < 160; index++) logger.LogInformation("native record {Index}: {Message}", index, string.Concat(Enumerable.Repeat("bounded native text ", 180)));
            logger.LogError("last native failure before process exit");
            logs.Complete(native);
            await capture.DisposeAsync();
            string path = Path.Combine(root, "evidence", native.Name + ".console.jsonl");
            new FileInfo(path).Length.ShouldBeLessThanOrEqualTo(PlatformDaprDiagnosticResource.LogByteLimit);
            string[] lines = File.ReadAllLines(path);
            JsonElement header = JsonDocument.Parse(lines[0]).RootElement;
            header.GetProperty("Truncated").GetBoolean().ShouldBeTrue();
            header.GetProperty("DroppedRecords").GetInt64().ShouldBeGreaterThan(0);
            string.Join('\n', lines).ShouldContain("last native failure before process exit");
            // Metadata has its own bound and remains available when the console budget is exhausted.
            new FileInfo(Path.Combine(root, "evidence", native.Name + ".state.jsonl")).Length.ShouldBeLessThanOrEqualTo(PlatformDaprDiagnosticResource.StateByteLimit);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task DisposalCancelsIdlePublicReadersDrainsPendingEvidenceAndIsIdempotentAsync()
    {
        string root = TemporaryDirectory();
        try
        {
            IDistributedApplicationBuilder builder = Builder();
            ExecutableResource native = builder.AddExecutable("memories-dapr-cli", "daprd", root).Resource;
            await using DistributedApplication app = builder.Build();
            string directory = Path.Combine(root, "evidence");
            PlatformDaprDiagnostics capture = new(directory, ["memories"], []);
            await StartCaptureAsync(app, builder, capture);
            ResourceLoggerService logs = app.Services.GetRequiredService<ResourceLoggerService>();
            logs.GetLogger(native).LogError("pending final console line");
            await app.Services.GetRequiredService<ResourceNotificationService>().PublishUpdateAsync(native, snapshot => snapshot with { State = new(KnownResourceStates.Exited, null), ExitCode = 23 });
            await capture.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await capture.DisposeAsync();
            byte[][] before = Directory.GetFiles(directory).Order().Select(File.ReadAllBytes).ToArray();
            logs.GetLogger(native).LogError("after disposal must be ignored");
            await app.Services.GetRequiredService<ResourceNotificationService>().PublishUpdateAsync(native, snapshot => snapshot with { ExitCode = 99 });
            byte[][] after = Directory.GetFiles(directory).Order().Select(File.ReadAllBytes).ToArray();
            after.Length.ShouldBe(before.Length);
            for (int index = 0; index < before.Length; index++) after[index].ShouldBe(before[index]);
            File.ReadAllText(Path.Combine(directory, native.Name + ".console.jsonl")).ShouldContain("pending final console line");
            File.ReadAllText(Path.Combine(directory, native.Name + ".state.jsonl")).ShouldContain("23");
            Directory.GetFiles(directory, "*.pending").ShouldBeEmpty();
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task ConsolePersistenceFailureIsExplicitAndDoesNotInventAnExitReasonAsync()
    {
        string root = TemporaryDirectory();
        try
        {
            IDistributedApplicationBuilder builder = Builder();
            ExecutableResource native = builder.AddExecutable("memories-dapr-cli", "daprd", root).Resource;
            await using DistributedApplication app = builder.Build();
            string directory = Path.Combine(root, "evidence");
            PlatformDaprDiagnostics capture = new(directory, ["memories"], []);
            await StartCaptureAsync(app, builder, capture);
            string console = Path.Combine(directory, native.Name + ".console.jsonl");
            File.Delete(console);
            Directory.CreateDirectory(console);
            app.Services.GetRequiredService<ResourceLoggerService>().GetLogger(native).LogInformation("force persistence failure");
            await WaitForAsync(() => File.ReadAllText(Path.Combine(directory, native.Name + ".state.jsonl")).Contains("console-capture-failed", StringComparison.Ordinal));
            await capture.DisposeAsync();
            string metadata = File.ReadAllText(Path.Combine(directory, native.Name + ".state.jsonl"));
            JsonElement stopped = StateRecords(directory).Last();
            stopped.GetProperty("Kind").GetString().ShouldBe("capture-stopped");
            stopped.GetProperty("CaptureFailed").GetBoolean().ShouldBeTrue();
            metadata.ShouldContain("unavailable-from-public-resource-snapshot");
            metadata.ShouldNotContain("Exception");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task ExistingEvidenceIsRefusedWithoutOverwritingItAsync()
    {
        string root = TemporaryDirectory();
        try
        {
            IDistributedApplicationBuilder builder = Builder();
            builder.AddExecutable("memories-dapr-cli", "daprd", root);
            await using DistributedApplication app = builder.Build();
            string directory = Path.Combine(root, "evidence");
            Directory.CreateDirectory(directory);
            string existing = Path.Combine(directory, "memories-dapr-cli.console.jsonl");
            File.WriteAllText(existing, "original evidence");
            PlatformDaprDiagnostics capture = new(directory, ["memories"], []);
            await using var captureDisposal = capture.ConfigureAwait(true);
            await Should.ThrowAsync<IOException>(() => StartCaptureAsync(app, builder, capture));
            File.ReadAllText(existing).ShouldBe("original evidence");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static IDistributedApplicationBuilder Builder()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = [], AssemblyName = typeof(PlatformDaprDiagnostics).Assembly.GetName().Name, DisableDashboard = true, AllowUnsecuredTransport = true,
        });
        builder.Configuration["DcpPublisher:CliPath"] = Path.Combine(Path.GetTempPath(), "native-capture-dcp-not-started");
        builder.Configuration["DcpPublisher:DashboardPath"] = Path.Combine(Path.GetTempPath(), "native-capture-dashboard-not-started");
        return builder;
    }

    private static string TemporaryDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), "platform-native-diagnostics-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static async Task StartCaptureAsync(DistributedApplication app, IDistributedApplicationBuilder builder, PlatformDaprDiagnostics capture)
    {
        IDistributedApplicationEventing eventing = app.Services.GetRequiredService<IDistributedApplicationEventing>();
        await capture.SubscribeAsync(eventing, builder.ExecutionContext, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await eventing.PublishAsync(new BeforeStartEvent(app.Services, app.Services.GetRequiredService<DistributedApplicationModel>()), TestContext.Current.CancellationToken).ConfigureAwait(true);
        // Real DCP allocation publishes a public instance notification after BeforeStart.
        ResourceNotificationService notifications = app.Services.GetRequiredService<ResourceNotificationService>();
        foreach (IResource resource in app.Services.GetRequiredService<DistributedApplicationModel>().Resources)
            await notifications.PublishUpdateAsync(resource, snapshot => snapshot).ConfigureAwait(true);
    }

    private static JsonElement[] StateRecords(string directory) => File.ReadAllLines(Path.Combine(directory, "memories-dapr-cli.state.jsonl"))
        .Select(line => JsonSerializer.Deserialize<JsonElement>(line)).ToArray();

    private static async Task WaitForAsync(Func<bool> condition)
    {
        long start = Stopwatch.GetTimestamp();
        while (!condition())
        {
            if (Stopwatch.GetElapsedTime(start) > TimeSpan.FromSeconds(5)) throw new TimeoutException("Public capture did not retain the expected observation.");
            await Task.Delay(10, TestContext.Current.CancellationToken).ConfigureAwait(true);
        }
    }
}
