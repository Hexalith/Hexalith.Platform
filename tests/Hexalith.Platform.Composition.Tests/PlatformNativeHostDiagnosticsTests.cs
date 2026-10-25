namespace Hexalith.Platform.Composition.Tests;

using System.Diagnostics;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

/// <summary>Exercises native process hooks and real public notifications using controlled children, without a topology.</summary>
public sealed class PlatformNativeHostDiagnosticsTests
{
    [Theory]
    [InlineData("normal")]
    [InlineData("cancellation")]
    [InlineData("resource-failure")]
    [InlineData("signal")]
    [InlineData("abrupt")]
    [InlineData("bounded")]
    public async Task ControlledExitRetainsOnlyObservedPrivateEvidenceAsync(string scenario)
    {
        string root = Path.Combine(Path.GetTempPath(), "platform-native-host-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        ProcessStartInfo start = new("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in new[] { typeof(PlatformNativeHostDiagnosticsTests).Assembly.Location, "-method", typeof(PlatformNativeHostDiagnosticsTests).FullName + ".ControlledChildAsync" }) start.ArgumentList.Add(argument);
        start.Environment["HEXALITH_NATIVE_HOST_SCENARIO"] = scenario;
        start.Environment["HEXALITH_NATIVE_HOST_EVIDENCE"] = root;
        start.Environment["HEXALITH_NATIVE_HOST_SECRET"] = "must-never-enter-native-evidence";
        using Process child = new() { StartInfo = start };
        try
        {
            child.Start().ShouldBeTrue();
            Task<string> stdout = child.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            Task<string> stderr = child.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            string path = Path.Combine(root, "apphost.lifecycle.jsonl");
            await WaitForAsync(() => File.Exists(path) && File.ReadAllText(path).Contains("capture-started", StringComparison.Ordinal));
            if (scenario == "abrupt") child.Kill(entireProcessTree: true);
            if (scenario == "signal")
            {
                using Process signal = Process.Start(new ProcessStartInfo("kill") { ArgumentList = { "-TERM", child.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) }, UseShellExecute = false })!;
                await signal.WaitForExitAsync(TestContext.Current.CancellationToken);
                signal.ExitCode.ShouldBe(0);
            }
            await child.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
            string output = await stdout.ConfigureAwait(true);
            string error = await stderr.ConfigureAwait(true);
            if (scenario is "normal" or "cancellation" or "resource-failure" or "bounded") child.ExitCode.ShouldBe(0, output + error);
            else child.ExitCode.ShouldNotBe(0);
            string bytes = File.ReadAllText(path);
            bytes.ShouldNotContain("must-never-enter-native-evidence");
            JsonElement[] records = File.ReadAllLines(path).Select(line => JsonSerializer.Deserialize<JsonElement>(line)).ToArray();
            records.Where(record => record.TryGetProperty("AppHostPid", out _)).ShouldAllBe(record => record.GetProperty("AppHostPid").GetInt32() == child.Id);
            bytes.ShouldContain("unavailable-from-managed-lifetime-hooks");
            bytes.ShouldContain("SIGKILL-or-runtime-loss-cannot-run-managed-exit-hooks");
            if (!OperatingSystem.IsWindows()) File.GetUnixFileMode(path).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
            new FileInfo(path).Length.ShouldBeLessThanOrEqualTo(65536);
            if (scenario == "abrupt") bytes.ShouldNotContain("process-exit");
            else if (scenario != "signal") bytes.ShouldContain("process-exit");
            if (scenario == "signal") bytes.ShouldContain("SIGTERM");
            if (scenario == "cancellation") bytes.ShouldContain("application-stopping");
            if (scenario == "resource-failure")
            {
                bytes.ShouldContain("resource-terminal-notification");
                bytes.ShouldContain("public-resource-notification");
                records.Any(record => record.TryGetProperty("ReportedExitCode", out JsonElement code) && code.ValueKind == JsonValueKind.Number && code.GetInt32() == 23).ShouldBeTrue();
                bytes.ShouldNotContain("foreign-resource");
            }
            if (scenario == "bounded") bytes.ShouldContain("resource-notifications-truncated");
        }
        finally
        {
            if (!child.HasExited) { child.Kill(entireProcessTree: true); await child.WaitForExitAsync(CancellationToken.None); }
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ControlledChildAsync()
    {
        string? scenario = Environment.GetEnvironmentVariable("HEXALITH_NATIVE_HOST_SCENARIO");
        if (scenario is null) return;
        string root = Environment.GetEnvironmentVariable("HEXALITH_NATIVE_HOST_EVIDENCE")!;
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { Args = [], DisableDashboard = true, AllowUnsecuredTransport = true, AssemblyName = typeof(PlatformNativeHostDiagnostics).Assembly.GetName().Name });
        builder.Configuration["DcpPublisher:CliPath"] = Path.Combine(root, "never-start-dcp");
        builder.Configuration["DcpPublisher:DashboardPath"] = Path.Combine(root, "never-start-dashboard");
        ExecutableResource owned = builder.AddExecutable("owned-resource", "never-executed", root).Resource;
        ExecutableResource foreign = builder.AddExecutable("foreign-resource", "never-executed", root).Resource;
        await using DistributedApplication app = builder.Build();
        ResourceNotificationService notifications = app.Services.GetRequiredService<ResourceNotificationService>();
        _ = new PlatformNativeHostDiagnostics(root, app.Services.GetRequiredService<IHostApplicationLifetime>(), notifications, [owned]);
        if (scenario is "signal" or "abrupt") await Task.Delay(Timeout.InfiniteTimeSpan, TestContext.Current.CancellationToken);
        if (scenario == "cancellation") app.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
        if (scenario is "resource-failure" or "bounded")
        {
            await notifications.PublishUpdateAsync(foreign, snapshot => snapshot with { State = new("Exited", null), ExitCode = 99 });
            int count = scenario == "bounded" ? 200 : 1;
            for (int index = 0; index < count; index++)
                await notifications.PublishUpdateAsync(owned, snapshot => snapshot with { State = new("Exited", null), ExitCode = 23, Properties = [new("executable.pid", 1729 + index), new("private", "must-never-enter-native-evidence")] });
            await WaitForAsync(() => File.ReadAllText(Path.Combine(root, "apphost.lifecycle.jsonl")).Contains(scenario == "bounded" ? "resource-notifications-truncated" : "resource-terminal-notification", StringComparison.Ordinal));
        }
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        long start = Stopwatch.GetTimestamp();
        while (!condition())
        {
            if (Stopwatch.GetElapsedTime(start) > TimeSpan.FromSeconds(10)) throw new TimeoutException("Controlled native host did not publish its observation.");
            await Task.Delay(10, TestContext.Current.CancellationToken).ConfigureAwait(true);
        }
    }
}
