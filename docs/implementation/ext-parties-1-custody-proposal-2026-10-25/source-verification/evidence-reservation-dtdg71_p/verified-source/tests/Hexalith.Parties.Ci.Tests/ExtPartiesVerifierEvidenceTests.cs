using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace Hexalith.Parties.Ci.Tests;

/// <summary>Exercises exclusive evidence ownership through isolated verifier child processes.</summary>
/// <param name="output">Reports the fresh fixture paths retained after every test.</param>
public sealed class ExtPartiesVerifierEvidenceTests(ITestOutputHelper output)
{
    private const string AttemptMarker = ".ext-parties-1-attempt";
    private const string Harness = """
        param([string]$Script, [string]$Mode, [string]$EvidenceDirectory,
            [string]$DependencyRegisterPath, [string]$Fixture, [string]$Participant,
            [switch]$Synchronize)
        $ErrorActionPreference = 'Stop'
        $lookupLog = Join-Path $Fixture ($Participant + '.lookups')
        $writeLog = Join-Path $Fixture ($Participant + '.writes')
        function dotnet {
            [IO.File]::AppendAllText($lookupLog, "dotnet`n")
            throw 'Unexpected Local dependency lookup.'
        }
        function Test-Path {
            [CmdletBinding()]
            param([string]$LiteralPath, [string]$Path, [string]$PathType)
            if ($LiteralPath -eq $DependencyRegisterPath -or $Path -eq $DependencyRegisterPath) {
                [IO.File]::AppendAllText($lookupLog, "register`n")
                throw 'Unexpected readiness dependency lookup.'
            }
            Microsoft.PowerShell.Management\Test-Path @PSBoundParameters
        }
        function Set-Content {
            [CmdletBinding()]
            param([string]$Path, [string]$LiteralPath, [object]$Value)
            [IO.File]::AppendAllText($writeLog, "evidence-write`n")
            Microsoft.PowerShell.Management\Set-Content @PSBoundParameters
        }
        if ($Synchronize) {
            function New-Item {
                [CmdletBinding()]
                param([string]$ItemType, [string]$Path)
                if ($ItemType -ne 'Directory' -or $Path -ne $EvidenceDirectory) {
                    throw 'Unexpected fixture directory creation.'
                }
                # Both real scripts have passed their existing-path check before
                # either directory creation returns. Model the nonexclusive API
                # returning success to both contenders, independent of scheduling.
                [IO.File]::WriteAllText((Join-Path $Fixture ($Participant + '.ready')), 'ready')
                $deadline = [DateTime]::UtcNow.AddSeconds(15)
                while (-not [IO.File]::Exists((Join-Path $Fixture 'release'))) {
                    if ([DateTime]::UtcNow -ge $deadline) { throw 'Reservation barrier timed out.' }
                    [Threading.Thread]::Sleep(10)
                }
                [IO.Directory]::CreateDirectory($Path)
            }
        }
        try {
            if ($Mode -eq 'DirectReadiness') {
                & $Script -EvidenceDirectory $EvidenceDirectory -DependencyRegisterPath $DependencyRegisterPath
            }
            else {
                & $Script -Mode $Mode -EvidenceDirectory $EvidenceDirectory -DependencyRegisterPath $DependencyRegisterPath
            }
            if ($null -ne $LASTEXITCODE) { exit $LASTEXITCODE }
        }
        catch {
            [Console]::Error.WriteLine($_.Exception.Message)
            exit 1
        }
        """;

    /// <summary>A refused Live attempt cannot be rewritten by a changed-input retry through any mode.</summary>
    /// <param name="mode">The retry entry point.</param>
    [Theory]
    [InlineData("Live")]
    [InlineData("Local")]
    [InlineData("LiveReadiness")]
    public async Task RefusedLiveAttempt_PreservesEvidenceAcrossChangedInputRetries(string mode)
    {
        string fixture = CreateFixture();
        string evidence = Path.Combine(fixture, "evidence");
        using Process original = Start(fixture, evidence, "original", "Live");
        (int originalExit, string originalError) = await CompleteAsync(original, fixture, "original").ConfigureAwait(true);
        originalExit.ShouldNotBe(0);
        originalError.ShouldContain("Missing live inputs:");
        File.Exists(Path.Combine(evidence, AttemptMarker)).ShouldBeTrue();
        File.ReadAllText(Path.Combine(evidence, "live-gate.txt")).ShouldContain("Missing live inputs:");
        Dictionary<string, string> before = Snapshot(evidence);

        using Process retry = Start(fixture, evidence, "retry", mode, completeInputs: true);
        (int retryExit, string retryError) = await CompleteAsync(retry, fixture, "retry").ConfigureAwait(true);

        retryExit.ShouldNotBe(0);
        retryError.ShouldContain("Evidence directory already exists:");
        Snapshot(evidence).ShouldBe(before);
        File.Exists(Path.Combine(fixture, "retry.lookups")).ShouldBeFalse();
        File.Exists(Path.Combine(fixture, "retry.writes")).ShouldBeFalse();
    }

    /// <summary>Direct readiness rejects retained evidence before input validation or register lookup.</summary>
    /// <param name="completeInputs">Whether changed synthetic readiness inputs are supplied.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectReadiness_RejectsExistingDirectoryBeforeInputsOrLookups(bool completeInputs)
    {
        string fixture = CreateFixture();
        string evidence = Path.Combine(fixture, "evidence");
        Directory.CreateDirectory(evidence);
        File.WriteAllText(Path.Combine(evidence, "live-readiness.json"), "historical-readiness-evidence");
        Dictionary<string, string> before = Snapshot(evidence);
        using Process attempt = Start(fixture, evidence, "direct", "DirectReadiness", completeInputs);

        (int exitCode, string error) = await CompleteAsync(attempt, fixture, "direct").ConfigureAwait(true);

        exitCode.ShouldNotBe(0);
        error.ShouldContain("Evidence directory already exists:");
        Snapshot(evidence).ShouldBe(before);
        File.Exists(Path.Combine(fixture, "direct.lookups")).ShouldBeFalse();
        File.Exists(Path.Combine(fixture, "direct.writes")).ShouldBeFalse();
    }

    /// <summary>Fresh direct and delegated readiness attempts reserve exactly once before missing-input refusal.</summary>
    /// <param name="mode">The direct helper or parent readiness route.</param>
    [Theory]
    [InlineData("DirectReadiness")]
    [InlineData("LiveReadiness")]
    public async Task FreshReadiness_ReservesOnceBeforeInputRefusal(string mode)
    {
        string fixture = CreateFixture();
        string evidence = Path.Combine(fixture, "evidence");
        using Process attempt = Start(fixture, evidence, "fresh", mode);

        (int exitCode, string error) = await CompleteAsync(attempt, fixture, "fresh").ConfigureAwait(true);

        exitCode.ShouldNotBe(0);
        error.ShouldContain("Missing read-only");
        error.ShouldNotContain("Evidence directory already exists:");
        Directory.GetFiles(evidence).Select(Path.GetFileName).ShouldBe([AttemptMarker]);
        File.Exists(Path.Combine(fixture, "fresh.lookups")).ShouldBeFalse();
    }

    /// <summary>Even when directory creation succeeds for both synchronized children, only one owns the attempt.</summary>
    /// <param name="mode">The parent Live gate or direct readiness helper.</param>
    [Theory]
    [InlineData("Live")]
    [InlineData("DirectReadiness")]
    public async Task SynchronizedReservations_AdmitExactlyOneWriter(string mode)
    {
        string fixture = CreateFixture();
        string evidence = Path.Combine(fixture, "evidence");
        using Process first = Start(fixture, evidence, "first", mode, synchronize: true);
        using Process second = Start(fixture, evidence, "second", mode, synchronize: true);
        Task<(int ExitCode, string Error)> firstResult = CompleteAsync(first, fixture, "first");
        Task<(int ExitCode, string Error)> secondResult = CompleteAsync(second, fixture, "second");
        try
        {
            using var barrier = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            barrier.CancelAfter(TimeSpan.FromSeconds(10));
            while (!File.Exists(Path.Combine(fixture, "first.ready")) || !File.Exists(Path.Combine(fixture, "second.ready")))
            {
                await Task.Delay(10, barrier.Token).ConfigureAwait(true);
            }

            Directory.Exists(evidence).ShouldBeFalse("Both children must pass the existing-directory check before either creates it.");
            File.WriteAllText(Path.Combine(fixture, "release"), "release");
            (int ExitCode, string Error)[] results = await Task.WhenAll(firstResult, secondResult).ConfigureAwait(true);
            results.ShouldAllBe(result => result.ExitCode != 0);
            string refusal = mode == "Live" ? "Missing live inputs:" : "Missing read-only";
            results.Count(result => result.Error.Contains(refusal, StringComparison.Ordinal)).ShouldBe(1);
            results.Count(result => result.Error.Contains(AttemptMarker, StringComparison.Ordinal)).ShouldBe(1);
            File.Exists(Path.Combine(evidence, AttemptMarker)).ShouldBeTrue();
            string[] writes = Directory.GetFiles(fixture, "*.writes");
            writes.Length.ShouldBe(mode == "Live" ? 1 : 0);
            if (mode == "Live")
            {
                File.ReadAllLines(writes.Single()).ShouldBe(["evidence-write"]);
                File.ReadAllText(Path.Combine(evidence, "live-gate.txt")).ShouldContain(refusal);
            }
            Directory.GetFiles(evidence).Length.ShouldBe(mode == "Live" ? 2 : 1);
        }
        finally
        {
            Stop(first);
            Stop(second);
        }
    }

    private string CreateFixture()
    {
        string root = Path.Combine(Path.GetTempPath(), $"ext-parties-verifier-evidence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "harness.ps1"), Harness);
        output.WriteLine($"Preserved verifier fixture: {root}");
        return root;
    }

    private static Process Start(string fixture, string evidence, string participant, string mode,
        bool completeInputs = false, bool synchronize = false)
    {
        string? configuredRoot = Environment.GetEnvironmentVariable("HEXALITH_PARTIES_SOURCE_ROOT");
        string repository = string.IsNullOrWhiteSpace(configuredRoot) ? CiTestPaths.RepositoryRoot : configuredRoot;
        string script = Path.Combine(repository, "eng", mode == "DirectReadiness"
            ? "verify-ext-parties-history-readiness.ps1" : "verify-ext-parties-1.ps1");
        var start = new ProcessStartInfo("pwsh")
        {
            WorkingDirectory = fixture,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        // Copy runtime discovery paths only; owner inputs and credentials never reach a child.
        start.Environment.Clear();
        foreach (string name in new[] { "PATH", "DOTNET_ROOT", "SystemRoot", "WINDIR" })
        {
            string? value = Environment.GetEnvironmentVariable(name);
            if (value is not null) { start.Environment[name] = value; }
        }
        start.Environment["POWERSHELL_TELEMETRY_OPTOUT"] = "1";
        start.Environment["PSModuleAnalysisCachePath"] = Path.Combine(fixture, participant + ".module-cache");
        if (completeInputs)
        {
            foreach (string name in new[] { "GATEWAY_URL", "TENANT_A", "TENANT_B", "PROVISIONER_CREDENTIAL",
                "IDENTITY_WRITER_CREDENTIAL", "READER_CREDENTIAL", "POLICY_ID", "RETENTION", "EXPIRY_TRIGGER",
                "CUSTODY_TARGET", "RESTORE_TARGET", "FAILURE_INJECTION_TARGET", "READER_HEADERS_JSON", "TARGET_SHA",
                "COMPATIBILITY_RECEIPT", "HISTORY_PARTY_ID", "HISTORY_ACTOR_ID", "HISTORY_ACTION_AT", "HISTORY_BINDING_VERSION" })
            {
                start.Environment["EXT_PARTIES_" + name] = "synthetic-changed-input";
            }
            start.Environment["EXT_PARTIES_GATEWAY_URL"] = "http://127.0.0.1:1";
            start.Environment["EXT_PARTIES_TARGET_SHA"] = new string('a', 40);
            start.Environment["EXT_PARTIES_COMPATIBILITY_RECEIPT"] = Path.Combine(fixture, "absent-synthetic-receipt.json");
            start.Environment["EXT_PARTIES_READER_HEADERS_JSON"] = "{\"Authorization\":\"Bearer synthetic-test-only\"}";
        }
        foreach (string argument in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-File", Path.Combine(fixture, "harness.ps1"),
            "-Script", script, "-Mode", mode, "-EvidenceDirectory", evidence, "-DependencyRegisterPath",
            Path.Combine(fixture, "absent-synthetic-register.md"), "-Fixture", fixture, "-Participant", participant })
        {
            start.ArgumentList.Add(argument);
        }
        if (synchronize) { start.ArgumentList.Add("-Synchronize"); }
        File.WriteAllText(Path.Combine(fixture, participant + ".command.json"), JsonSerializer.Serialize(new
        {
            executable = start.FileName, arguments = start.ArgumentList.ToArray(), environment = start.Environment,
        }));
        return Process.Start(start) ?? throw new InvalidOperationException("Verifier fixture could not start.");
    }

    private static async Task<(int ExitCode, string Error)> CompleteAsync(Process process, string fixture, string participant)
    {
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
        }
        finally
        {
            Stop(process);
            File.WriteAllText(Path.Combine(fixture, participant + ".stdout.log"), await stdout.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false));
            File.WriteAllText(Path.Combine(fixture, participant + ".stderr.log"), await stderr.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false));
        }
        File.WriteAllText(Path.Combine(fixture, participant + ".exit-code"), process.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return (process.ExitCode, await stderr.ConfigureAwait(false));
    }

    private static void Stop(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5_000).ShouldBeTrue("A timed-out verifier child must terminate.");
        }
    }

    private static Dictionary<string, string> Snapshot(string directory)
        => Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(directory, path), path => Directory.Exists(path)
                ? "directory" : Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
}
