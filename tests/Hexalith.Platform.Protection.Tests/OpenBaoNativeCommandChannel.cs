using System.Diagnostics;
using System.Text;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>One owned docker-exec stream removes host-process startup per request. Every request still executes the real bao API command with its own stdin-only credential.</summary>
internal sealed class OpenBaoNativeCommandChannel(string container) : IDisposable
{
    private readonly SemaphoreSlim _serial = new(1, 1);
    private Process? _process;
    private bool _broken;
    private bool _disposed;
    // Input: credential, closed method/path, base64 UTF-8 JSON. Output: exit code,
    // base64 stderr, base64 stdout. FDs 3/4 preserve both native streams without a
    // temporary file. Shell command substitution strips trailing newlines only.
    private const string Script = """
        while IFS= read -r BAO_TOKEN && IFS= read -r method && IFS= read -r path && IFS= read -r payload; do
          export BAO_TOKEN
          set +e
          {
            result=$(
              {
                if [ "$method" = GET ]; then
                  if [ "$path" = sys/health ]; then error=$(bao status -format=json 2>&1 1>&3);
                  else error=$(bao read -format=json "$path" 2>&1 1>&3); fi
                elif [ "$method" = DELETE ]; then error=$(bao delete -format=json "$path" 2>&1 1>&3);
                else error=$(printf '%s' "$payload" | base64 -d | bao write -format=json "$path" - 2>&1 1>&3); fi
                code=$?
                printf '%s\n' "$code" >&4
                printf '%s' "$error" | base64 | tr -d '\n' >&4
                printf '\n' >&4
              } 3>&1
            )
            printf '%s' "$result" | base64 | tr -d '\n'
            printf '\n'
          } 4>&1
          set -e
          unset BAO_TOKEN payload result error
        done
        """;
    internal async Task<(int Code, string Output, string Error)> SendAsync(string method, string path, string credential, string payload, CancellationToken token)
    {
        if (credential.Contains('\n') || credential.Contains('\r')) { throw new InvalidOperationException("Closed native credential required."); }
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(token); budget.CancelAfter(TimeSpan.FromSeconds(30));
        await _serial.WaitAsync(budget.Token).ConfigureAwait(false);
        try
        {
            if (_broken) { throw new InvalidOperationException("Unknown native channel cannot be reused or redispatched."); }
            var process = _process ??= Start();
            foreach (string part in new[] { credential, method, path, Convert.ToBase64String(Encoding.UTF8.GetBytes(payload)) })
            { await process.StandardInput.WriteLineAsync(part.AsMemory(), budget.Token).ConfigureAwait(false); }
            await process.StandardInput.FlushAsync(budget.Token).ConfigureAwait(false);
            string? status = await process.StandardOutput.ReadLineAsync(budget.Token).ConfigureAwait(false);
            string? error = await process.StandardOutput.ReadLineAsync(budget.Token).ConfigureAwait(false);
            string? output = await process.StandardOutput.ReadLineAsync(budget.Token).ConfigureAwait(false);
            if (!int.TryParse(status, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int code) || code is < 0 or > 255)
            { throw new InvalidOperationException("Exact native exit status required."); }
            return (code, Decode(output, 32 * 1024 * 1024), Decode(error, 1024 * 1024));
        }
        catch { _broken = true; Close(); throw; }
        finally { _serial.Release(); }
    }
    private static string Decode(string? frame, int maximum)
    {
        if (frame is null || frame.Length > 4L * ((maximum + 2L) / 3)) { throw new InvalidOperationException("Bounded exact native response frame required."); }
        byte[] bytes = Convert.FromBase64String(frame);
        if (bytes.Length > maximum) { throw new InvalidOperationException("Bounded native response required."); }
        return Encoding.UTF8.GetString(bytes);
    }
    private Process Start()
    {
        var start = new ProcessStartInfo("docker") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string argument in new[] { "--host", "unix:///var/run/docker.sock", "exec", "-i", container, "sh", "-ec", Script }) { start.ArgumentList.Add(argument); }
        var process = Process.Start(start) ?? throw new InvalidOperationException("Owned native channel unavailable.");
        _ = DrainAsync(process.StandardError);
        return process;
    }
    private static async Task DrainAsync(StreamReader error)
    {
        char[] buffer = new char[4096];
        try { while (await error.ReadAsync(buffer.AsMemory()).ConfigureAwait(false) != 0) { } }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
    }
    private void Close()
    {
        var process = _process; _process = null;
        if (process is null) { return; }
        try
        {
            process.StandardInput.Close();
            if (!process.WaitForExit(1000)) { process.Kill(entireProcessTree: true); }
        }
        finally { process.Dispose(); }
    }
    public void Dispose() { if (_disposed) { return; } _disposed = true; _broken = true; Close(); _serial.Dispose(); }
}
