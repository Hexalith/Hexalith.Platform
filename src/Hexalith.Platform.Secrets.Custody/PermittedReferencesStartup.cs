using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Inventory;
using Microsoft.Win32.SafeHandles;

namespace Hexalith.Platform.Secrets.Custody;

// All StartingAsync callbacks finish before any hosted service starts, including concurrent web hosts.
internal sealed class PermittedReferencesStartup(string? path, JsonArray permitted, PermittedReferencesFileReader reader) : IHostedLifecycleService
{
    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        JsonArray snapshot = await reader.ReadAsync(path, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        foreach (JsonNode? item in snapshot) { permitted.Add(item!.DeepClone()); }
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>Acquires the startup-only projected ConfigMap snapshot without abandoning actual worker ownership.</summary>
internal sealed class PermittedReferencesFileReader
{
    internal const int MaximumBytes = 1024 * 1024;
    // Shared across reader instances. Startup cannot queue or accumulate detached acquisition work.
    private static readonly SemaphoreSlim Workers = new(1, 1);
    private readonly Func<string, Stream> _open;
    private readonly TimeSpan _timeout;
    private readonly Action? _workerFinished;
    private readonly Action? _lateFaultObserved;

    internal PermittedReferencesFileReader() : this(Open, TimeSpan.FromSeconds(2)) { }

    internal PermittedReferencesFileReader(Func<string, Stream> open, TimeSpan timeout,
        Action? workerFinished = null, Action? lateFaultObserved = null)
    { _open = open; _timeout = timeout; _workerFinished = workerFinished; _lateFaultObserved = lateFaultObserved; }

    internal async Task<JsonArray> ReadAsync(string? path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(path) || path.Length > 4096 || path.Contains('\0') || !Path.IsPathFullyQualified(path)) { throw Unavailable(); }
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(_timeout);
        CancellationToken token = budget.Token;
        if (!Workers.Wait(0)) { throw Unavailable(); }
        Task<JsonArray> work;
        try
        {
            // A stalled open/read/close must not consume the shared pool that executes deadlines and host cancellation.
            work = Task.Factory.StartNew(() =>
            {
                try { return ReadSnapshot(path, token); }
                catch (OperationCanceledException) { throw; }
                catch (Exception) { throw Unavailable(); } // Never retain a path, input or underlying exception in diagnostics.
                finally { Workers.Release(); _workerFinished?.Invoke(); }
            }, CancellationToken.None, TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
        }
        catch { Workers.Release(); throw Unavailable(); }
        try
        {
            JsonArray result = await work.WaitAsync(token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            return result;
        }
        catch
        {
            _ = work.ContinueWith(task => { _ = task.Exception; _lateFaultObserved?.Invoke(); }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            if (cancellationToken.IsCancellationRequested) { throw new OperationCanceledException(cancellationToken); }
            throw Unavailable();
        }
    }

    private JsonArray ReadSnapshot(string path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        JsonArray result;
        using (Stream stream = _open(path))
        {
            token.ThrowIfCancellationRequested();
            byte[] bytes = new byte[MaximumBytes + 1];
            try
            {
                int used = 0;
                while (used < bytes.Length)
                {
                    token.ThrowIfCancellationRequested();
                    int count = stream.Read(bytes.AsSpan(used));
                    if (count == 0) { break; }
                    used += count;
                }
                token.ThrowIfCancellationRequested();
                if (used is 0 or > MaximumBytes) { throw Unavailable(); }
                ReadOnlySpan<byte> encoded = bytes.AsSpan(0, used);
                if (encoded.StartsWith(Encoding.UTF8.Preamble)) { encoded = encoded[Encoding.UTF8.Preamble.Length..]; }
                JsonObject? document = InventorySources.ParseObject(new UTF8Encoding(false, true).GetString(encoded));
                if (document?["permittedReferences"] is not JsonArray permitted || !ValidScopes(permitted)) { throw Unavailable(); }
                result = permitted;
            }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
        // Disposal is part of acquisition; a late successful read must never publish after cancellation or timeout.
        token.ThrowIfCancellationRequested();
        return result;
    }

    private static bool ValidScopes(JsonArray permitted)
    {
        if (permitted.Count != 4) { return false; }
        var seen = new HashSet<PlatformHmacScope>();
        foreach (JsonNode? node in permitted)
        {
            if (node is not JsonObject item || item["tenantId"] is not JsonValue tenantValue
                || !tenantValue.TryGetValue(out string? tenant) || string.IsNullOrEmpty(tenant)
                || item["purpose"] is not JsonValue purposeValue || !purposeValue.TryGetValue(out string? purpose)
                || !Enum.TryParse(purpose, out PlatformHmacPurpose parsed)
                || parsed is not (PlatformHmacPurpose.TrustedEnvelope or PlatformHmacPurpose.ContentDigest)
                || parsed.ToString() != purpose) { return false; }
            var scope = new PlatformHmacScope(tenant, parsed);
            if (!scope.IsValid || !seen.Add(scope)) { return false; }
        }
        return true;
    }

    private static IOException Unavailable() => new("A valid bounded public permitted-reference file is required.");

    internal static Stream Open(string path)
    {
        if (!OperatingSystem.IsLinux()) { throw Unavailable(); }
        // Preserve the whole-directory ConfigMap mount: /var/run and AtomicWriter ..data links are approved.
        int descriptor = LinuxOpen(path, 0x800 | 0x80000); // O_RDONLY | O_NONBLOCK | O_CLOEXEC
        if (descriptor < 0) { throw Unavailable(); }
        var handle = new SafeFileHandle((IntPtr)descriptor, ownsHandle: true);
        try
        {
            if (Statx(descriptor, "", 0x1000, 0x201, out Status status) != 0 || (status.Mask & 0x201) != 0x201
                || (status.Mode & 0xf000) != 0x8000 || status.Size is 0 or > MaximumBytes) { throw Unavailable(); }
            return new FileStream(handle, FileAccess.Read, 1, isAsync: false);
        }
        catch { handle.Dispose(); throw; }
    }

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int LinuxOpen([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);
    [DllImport("libc", EntryPoint = "statx", SetLastError = true)]
    private static extern int Statx(int descriptor, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags, uint mask, out Status status);
    [StructLayout(LayoutKind.Explicit, Size = 256)]
    private struct Status
    {
        [FieldOffset(0)] internal uint Mask;
        [FieldOffset(28)] internal ushort Mode;
        [FieldOffset(40)] internal ulong Size;
    }
}
