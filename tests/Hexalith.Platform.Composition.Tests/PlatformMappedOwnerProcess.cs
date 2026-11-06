namespace Hexalith.Platform.Composition.Tests;

using System.Diagnostics;
using Shouldly;

/// <summary>Owns an inert kernel-mapping child; it never executes a module, opens a listener, or establishes runtime qualification.</summary>
internal sealed class PlatformMappedOwnerProcess : IAsyncDisposable
{
    private readonly Process _process;

    private PlatformMappedOwnerProcess(Process process) => _process = process;

    /// <summary>Gets the actual inert child PID published to the unstarted component model.</summary>
    internal int Id => _process.Id;

    /// <summary>Maps the supplied actual files read-only and confirms the child's readiness without starting any module.</summary>
    internal static async Task<PlatformMappedOwnerProcess> StartAsync(IEnumerable<string> paths)
    {
        Process process = new() { StartInfo = new ProcessStartInfo("python3")
        { RedirectStandardOutput = true, RedirectStandardInput = true, UseShellExecute = false } };
        process.StartInfo.ArgumentList.Add("-c");
        process.StartInfo.ArgumentList.Add("import mmap,sys; fs=[open(p,'rb') for p in sys.argv[1:]]; maps=[mmap.mmap(f.fileno(),0,access=mmap.ACCESS_READ) for f in fs]; print('mapped',flush=True); sys.stdin.buffer.read(1)");
        foreach (string path in paths) process.StartInfo.ArgumentList.Add(path);
        try
        {
            process.Start().ShouldBeTrue();
            using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            bound.CancelAfter(TimeSpan.FromSeconds(20));
            (await process.StandardOutput.ReadLineAsync(bound.Token)).ShouldBe("mapped");
            return new(process);
        }
        catch
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            process.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!_process.HasExited)
            {
                await _process.StandardInput.WriteAsync("x");
                await _process.StandardInput.FlushAsync();
                using CancellationTokenSource bound = new(TimeSpan.FromSeconds(20));
                await _process.WaitForExitAsync(bound.Token);
            }
        }
        finally
        {
            if (!_process.HasExited) _process.Kill(entireProcessTree: true);
            _process.Dispose();
        }
    }
}
