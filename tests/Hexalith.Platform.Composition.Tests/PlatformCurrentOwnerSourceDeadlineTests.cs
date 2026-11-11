namespace Hexalith.Platform.Composition.Tests;

using System.Diagnostics;
using Hexalith.Builds.Tooling.Runtime;
using Shouldly;

/// <summary>Qualifies real regular-file leases through capture, final recheck and owned worker cleanup.</summary>
public sealed class PlatformCurrentOwnerSourceDeadlineTests
{
    private const string LeaseScript = "import fcntl,signal,sys; f=open(sys.argv[1],'r+'); signal.signal(signal.SIGIO,signal.SIG_IGN); fcntl.fcntl(f,fcntl.F_SETLEASE,fcntl.F_WRLCK); print('leased',flush=True); sys.stdin.readline(); fcntl.fcntl(f,fcntl.F_SETLEASE,fcntl.F_UNLCK)";

    /// <summary>The independently copied composition output starts its private worker, retaining exact SHA-256.</summary>
    [Fact]
    public async Task PrivateOutputWorkerAndUnavailableRuntimeRetainDefinedResultsAsync()
    {
        using PlatformPartiesProofFixture fixture = new();
        string input = Path.Combine(fixture.Options.Mapping.Root, "Native.cs");
        using Process child = CopiedWorker(fixture);
        bool started = child.Start();
        try
        {
            started.ShouldBeTrue();
            using CancellationTokenSource bound = new(TimeSpan.FromSeconds(10));
            await child.StandardInput.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new[] { input }).AsMemory(), bound.Token).ConfigureAwait(true);
            child.StandardInput.Close();
            await child.WaitForExitAsync(bound.Token).ConfigureAwait(true);
            child.ExitCode.ShouldBe(0, await child.StandardError.ReadToEndAsync(bound.Token).ConfigureAwait(true));
            (await child.StandardOutput.ReadToEndAsync(bound.Token).ConfigureAwait(true)).Trim().ShouldBe(Convert.ToHexStringLower(
                System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(input))));
        }
        finally { if (started) await StopAsync(child).ConfigureAwait(true); }
        try
        {
            PlatformSourceStateWorker.RuntimeOverride.Value = Path.Combine(fixture.Root, "missing-dotnet");
            Should.Throw<InvalidDataException>(() => PlatformSourceStateWorker.Read([input], DateTimeOffset.UtcNow.AddSeconds(5)));
        }
        finally { PlatformSourceStateWorker.RuntimeOverride.Value = null; }
    }

    /// <summary>Both a stalled copied worker and a failing assertion leave no owned child alive.</summary>
    /// <param name="timeout">Whether the child operation times out or fails an assertion.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CopiedWorkerFailurePathsAlwaysTerminateAndReapAsync(bool timeout)
    {
        using PlatformPartiesProofFixture fixture = new();
        using Process child = CopiedWorker(fixture);
        bool started = false;
        async Task ExerciseAsync()
        {
            try
            {
                started = child.Start();
                started.ShouldBeTrue();
                if (timeout)
                {
                    using CancellationTokenSource bound = new(TimeSpan.FromMilliseconds(100));
                    await child.WaitForExitAsync(bound.Token).ConfigureAwait(true);
                }
                else false.ShouldBeTrue("Controlled assertion failure while the copied child is waiting for input.");
            }
            finally { if (started) await StopAsync(child).ConfigureAwait(true); }
        }
        Exception? failure = await Record.ExceptionAsync(ExerciseAsync).ConfigureAwait(true);
        if (timeout) failure.ShouldBeAssignableTo<OperationCanceledException>();
        else failure.ShouldBeOfType<Shouldly.ShouldAssertException>();
        child.HasExited.ShouldBeTrue();
    }

    /// <summary>A lease acquired at measurement start blocks capture and the final hash worker, never the preceding Git checks.</summary>
    /// <param name="recheck">Whether the lease blocks initial capture or the final recheck.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RegularFileLeaseBoundsCallerAndReapsWorkerAsync(bool recheck)
    {
        OperatingSystem.IsLinux().ShouldBeTrue();
        using PlatformPartiesProofFixture fixture = new();
        string root = fixture.Options.Mapping.Root;
        string revision = PlatformPartiesProofFixture.Git(root, "rev-parse", "HEAD");
        PlatformCurrentOwnerSourceState? state = recheck ? PlatformCurrentOwnerSourceState.Capture(root, revision, DateTimeOffset.UtcNow.AddSeconds(15)) : null;
        using Process lease = Lease(Path.Combine(root, "Native.cs"));
        bool leased = false;
        List<int> workers = [];
        Task release = Task.CompletedTask;
        try
        {
            PlatformSourceStateWorker.StartedObserver.Value = id =>
            {
                workers.Add(id);
                leased = lease.Start();
                leased.ShouldBeTrue();
                lease.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult().ShouldBe("leased");
                release = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(true);
                    lease.StandardInput.Close();
                });
            };
            Stopwatch elapsed = Stopwatch.StartNew();
            DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMilliseconds(750);
            Exception? result = Record.Exception(() =>
            {
                if (recheck) state!.RequireUnchanged(root, revision, deadline);
                else _ = PlatformCurrentOwnerSourceState.Capture(root, revision, deadline);
            });
            TimeSpan returned = elapsed.Elapsed;
            await release.ConfigureAwait(true);
            if (leased) await lease.WaitForExitAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            TestContext.Current.SendDiagnosticMessage($"source lease recheck={recheck}; caller milliseconds={returned.TotalMilliseconds:F0}; worker count={workers.Count}");
            result.ShouldBeOfType<InvalidDataException>();
            returned.ShouldBeLessThan(TimeSpan.FromSeconds(2));
            int worker = workers.ShouldHaveSingleItem();
            await WaitForReapingAsync(worker).ConfigureAwait(true);
            PlatformSourceStateWorker.StartedObserver.Value = null;
            PlatformCurrentOwnerSourceState.Capture(root, revision, DateTimeOffset.UtcNow.AddSeconds(15))
                .RequireUnchanged(root, revision, DateTimeOffset.UtcNow.AddSeconds(15));
        }
        finally
        {
            PlatformSourceStateWorker.StartedObserver.Value = null;
            if (leased) await StopAsync(lease).ConfigureAwait(true);
            await release.ConfigureAwait(true);
        }
    }

    /// <summary>Termination failure and a blocked cleanup cannot extend the caller budget or discard its live worker.</summary>
    [Fact]
    public async Task FailedTerminationRetainsOwnershipAndDoesNotRenewCallerDeadlineAsync()
    {
        OperatingSystem.IsLinux().ShouldBeTrue();
        using PlatformPartiesProofFixture fixture = new();
        string input = Path.Combine(fixture.Options.Mapping.Root, "Native.cs");
        using Process lease = Lease(input);
        bool leased = false;
        int worker = 0;
        int attempts = 0;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            PlatformSourceStateWorker.StartedObserver.Value = id =>
            {
                worker = id;
                leased = lease.Start();
                leased.ShouldBeTrue();
                lease.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult().ShouldBe("leased");
            };
            PlatformSourceStateWorker.StopOverride.Value = async process =>
            {
                if (Interlocked.Increment(ref attempts) == 1) throw new System.ComponentModel.Win32Exception(5);
                entered.TrySetResult();
                await release.Task.ConfigureAwait(true);
                process.Kill(entireProcessTree: true);
            };
            Stopwatch elapsed = Stopwatch.StartNew();
            Should.Throw<InvalidDataException>(() => PlatformSourceStateWorker.Read([input], DateTimeOffset.UtcNow.AddMilliseconds(500)));
            elapsed.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(1.5));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken).ConfigureAwait(true);
            PlatformSourceStateWorker.IsRetained(worker).ShouldBeTrue();
            using (Process live = Process.GetProcessById(worker)) live.HasExited.ShouldBeFalse();
            attempts.ShouldBeGreaterThanOrEqualTo(2);
            release.TrySetResult();
            await WaitForReapingAsync(worker).ConfigureAwait(true);
        }
        finally
        {
            release.TrySetResult();
            PlatformSourceStateWorker.StartedObserver.Value = null;
            PlatformSourceStateWorker.StopOverride.Value = null;
            if (leased) await StopAsync(lease).ConfigureAwait(true);
            if (worker != 0) await WaitForReapingAsync(worker).ConfigureAwait(true);
        }
    }

    private static Process CopiedWorker(PlatformPartiesProofFixture fixture)
    {
        string output = Path.Combine(fixture.Root, "private-model");
        Directory.CreateDirectory(output);
        string source = typeof(PlatformCurrentOwnerSourceState).Assembly.Location;
        foreach (string extension in new[] { ".dll", ".deps.json", ".runtimeconfig.json" })
            File.Copy(Path.ChangeExtension(source, extension), Path.Combine(output, "Hexalith.Platform.Composition" + extension));
        return new() { StartInfo = CompositionProcess.CreateStartInfo("dotnet",
            [Path.Combine(output, "Hexalith.Platform.Composition.dll"), "--source-state-hash"], output, null) };
    }

    private static Process Lease(string input) => new() { StartInfo = CompositionProcess.CreateStartInfo("python3", ["-c", LeaseScript, input], null, null) };

    private static async Task WaitForReapingAsync(int worker)
    {
        using CancellationTokenSource bound = new(TimeSpan.FromSeconds(5));
        while (PlatformSourceStateWorker.IsRetained(worker))
            await Task.Delay(10, bound.Token).ConfigureAwait(false);
        Should.Throw<ArgumentException>(() => Process.GetProcessById(worker));
    }

    private static async Task StopAsync(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) when (process.HasExited) { }
        using CancellationTokenSource bound = new(TimeSpan.FromSeconds(5));
        await process.WaitForExitAsync(bound.Token).ConfigureAwait(false);
    }
}
