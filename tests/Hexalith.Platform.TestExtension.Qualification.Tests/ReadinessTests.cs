using System.Diagnostics;
using System.Reflection;

namespace Hexalith.Platform.TestExtension.Qualification.Tests;

/// <summary>Observes the one declared package fixture after its legitimate parent supplies a ready endpoint.</summary>
public sealed class ReadinessTests
{
    /// <summary>The native MTP result describes only the declared extension's readiness.</summary>
    [Fact]
    public async Task DeclaredExtensionReportsItsPackageIdentityAndMajorAsync()
    {
        string root = typeof(ReadinessTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "PlatformFixtureRepositoryRoot").Value!;
        string observer = Path.Combine(root, "eng", "test-extension-release", "qualify-host.py");
        Assert.True(File.Exists(observer), "The source-bound readiness observer is unavailable.");
        string report = Path.Combine(Path.GetTempPath(), "hexalith-fixture-observer-" + Guid.NewGuid().ToString("N") + ".trx");
        var start = new ProcessStartInfo("python3")
        {
            WorkingDirectory = root, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true,
        };
        foreach (string argument in new[] { "-E", "-s", "-B", observer }) start.ArgumentList.Add(argument);
        start.Environment.Clear();
        foreach (string key in new[] { "PATH", "HOME", "SYSTEMROOT", "TMPDIR", "TEMP", "TMP",
            "HEXALITH_G4_TEST_OWNER", "HEXALITH_G4_TEST_NAME", "HEXALITH_G4_TEST_MODE",
            "HEXALITH_G4_TEST_MANIFEST_HASH", "HEXALITH_G4_TEST_ENTRY_HASH", "HEXALITH_G4_RUN_ID",
            "HEXALITH_G4_EXECUTION_DEADLINE_UTC", "HEXALITH_G4_EVENTSTORE_URL" })
            if (Environment.GetEnvironmentVariable(key) is { } value) start.Environment[key] = value;
        start.Environment["HEXALITH_G4_TEST_REPORT_PATH"] = report;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var process = new Process { StartInfo = start };
        bool started = false;
        try
        {
            started = process.Start();
            Assert.True(started, "The readiness observer could not start.");
            Task<string> output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            Task<string> error = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(true);
            Assert.True(process.ExitCode == 0, "Declared extension readiness observer failed: "
                + await output.ConfigureAwait(true) + await error.ConfigureAwait(true));
        }
        finally
        {
            if (started && !process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                Assert.True(process.WaitForExit(5000), "The cancelled readiness observer did not terminate.");
            }
            if (File.Exists(report)) File.Delete(report);
        }
    }
}
