namespace Hexalith.Platform.Composition;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.Builds.Tooling.Runtime;

/// <summary>Owns interruptible source measurement processes instead of blocked caller threads.</summary>
internal static class PlatformSourceStateWorker
{
    private static readonly ConcurrentDictionary<int, Process> _stopping = new();

    /// <summary>Observes only this invocation's owned process for focused lifetime qualification.</summary>
    internal static AsyncLocal<Action<int>?> StartedObserver { get; } = new();

    /// <summary>Selects an unavailable runtime only for focused process-start qualification.</summary>
    internal static AsyncLocal<string?> RuntimeOverride { get; } = new();

    /// <summary>Injects owned termination failures or delays for focused cleanup qualification.</summary>
    internal static AsyncLocal<Func<Process, Task>?> StopOverride { get; } = new();

    /// <summary>Reports retained ownership until actual process exit has been observed.</summary>
    internal static bool IsRetained(int processId) => _stopping.ContainsKey(processId);

    /// <summary>Measures the selected paths under the original cumulative deadline.</summary>
    internal static IReadOnlyDictionary<string, string> Read(string[] paths, DateTimeOffset deadline)
        => ReadAsync(paths, deadline).GetAwaiter().GetResult();

    /// <summary>Runs the private child entry point; ordinary composition consumers continue loading this assembly.</summary>
    private static int Main(string[] arguments)
    {
        if (arguments is not ["--source-state-hash"]) return 1;
        try
        {
            string[] paths = JsonSerializer.Deserialize<string[]>(Console.In.ReadToEnd()) ?? throw new InvalidDataException();
            if (paths.Length > 32768) return 1;
            foreach (string path in paths)
            {
                FileInfo file = new(path);
                if (!Path.IsPathFullyQualified(path) || !file.Exists || file.LinkTarget is not null || file.Length > 268435456) return 1;
                for (DirectoryInfo? parent = file.Directory; parent is not null; parent = parent.Parent)
                    if (parent.LinkTarget is not null) return 1;
                using FileStream input = File.OpenRead(path);
                using IncrementalHash digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                byte[] buffer = new byte[65536];
                long total = 0;
                int count;
                while ((count = input.Read(buffer)) != 0)
                {
                    if ((total += count) > 268435456) return 1;
                    digest.AppendData(buffer, 0, count);
                }
                Console.WriteLine(Convert.ToHexStringLower(digest.GetHashAndReset()));
            }
            return 0;
        }
        catch { return 1; }
    }

    private static async Task<IReadOnlyDictionary<string, string>> ReadAsync(string[] paths, DateTimeOffset deadline)
    {
        TimeSpan remaining = deadline - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero) throw Refusal();
        using CancellationTokenSource bound = new(remaining);
        Process process = new() { StartInfo = CompositionProcess.CreateStartInfo(RuntimeOverride.Value ?? "dotnet",
            [typeof(PlatformSourceStateWorker).Assembly.Location, "--source-state-hash"], null, null) };
        bool started = false;
        try
        {
            try { started = process.Start(); }
            catch (System.ComponentModel.Win32Exception) { throw Refusal(); }
            if (!started) throw Refusal();
            StartedObserver.Value?.Invoke(process.Id);
            Task<string[]> output = ReadHashesAsync(process.StandardOutput, paths.Length, bound.Token);
            // The child emits no source bodies, paths or exception diagnostics.
            Task<string> error = process.StandardError.ReadToEndAsync(bound.Token);
            await process.StandardInput.WriteAsync(JsonSerializer.Serialize(paths).AsMemory(), bound.Token).ConfigureAwait(false);
            process.StandardInput.Close();
            await process.WaitForExitAsync(bound.Token).ConfigureAwait(false);
            string[] hashes = await output.ConfigureAwait(false);
            if (process.ExitCode != 0 || (await error.ConfigureAwait(false)).Length != 0 || hashes.Length != paths.Length) throw Refusal();
            return paths.Zip(hashes).ToDictionary(pair => pair.First, pair => pair.Second, StringComparer.Ordinal);
        }
        catch (OperationCanceledException) { throw Refusal(); }
        finally
        {
            bound.Cancel();
            if (started)
            {
                // Ownership is transferred before termination starts. Cleanup never renews
                // the caller deadline, and even a failed Kill cannot discard a live child.
                _stopping[process.Id] = process;
                Func<Process, Task>? terminate = StopOverride.Value;
                _ = Task.Run(() => ReapAsync(process, terminate));
            }
            else process.Dispose();
        }
    }

    private static async Task<string[]> ReadHashesAsync(StreamReader output, int expected, CancellationToken cancellationToken)
    {
        List<string> hashes = [];
        while (await output.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (hashes.Count == expected || line.Length != 64 || line.Any(value => value is not (>= '0' and <= '9' or >= 'a' and <= 'f'))) throw Refusal();
            hashes.Add(line);
        }
        return hashes.ToArray();
    }

    private static async Task ReapAsync(Process process, Func<Process, Task>? terminate)
    {
        while (true)
        {
            try
            {
                if (process.HasExited) break;
                try
                {
                    if (terminate is null) process.Kill(entireProcessTree: true);
                    else await terminate(process).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Permission, exit races and termination failures retain ownership.
                }
                using CancellationTokenSource observation = new(TimeSpan.FromMilliseconds(100));
                try { await process.WaitForExitAsync(observation.Token).ConfigureAwait(false); }
                catch (Exception) { }
            }
            catch (Exception)
            {
                // An unavailable observation is not evidence of exit. Keep retrying
                // independently of the caller until the owned child really exits.
            }
            await Task.Delay(100).ConfigureAwait(false);
        }
        _stopping.TryRemove(process.Id, out _);
        process.Dispose();
    }

    private static InvalidDataException Refusal() => new("The bounded current-owner source measurement is unavailable.");
}
