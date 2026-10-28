using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Hexalith.Platform.Secrets.Authority;

/// <summary>Reopens the projected Secret per authorized request, with finite acquisition and worker ownership.</summary>
internal sealed class AdministratorChannelTokenReader
{
    internal const int MaximumBytes = 8192;
    internal const int MaximumWorkers = 4;
    private readonly SemaphoreSlim _workers = new(MaximumWorkers, MaximumWorkers);
    private readonly Func<string, Stream> _open;
    private readonly TimeSpan _timeout;
    private readonly Action? _lateFaultObserved;

    public AdministratorChannelTokenReader() : this(Open, TimeSpan.FromSeconds(2)) { }

    internal AdministratorChannelTokenReader(Func<string, Stream> open, TimeSpan timeout, Action? lateFaultObserved = null)
    { _open = open; _timeout = timeout; _lateFaultObserved = lateFaultObserved; }

    internal async Task<bool> MatchesAsync(string? path, string received, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (received.Length is 0 or > MaximumBytes || Encoding.UTF8.GetByteCount(received) > MaximumBytes) { return false; }
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) { throw new IOException("Administrator channel mount unavailable."); }
        // Never queue acquisition. A caller timeout cannot release a still-running kernel call's slot.
        if (!_workers.Wait(0)) { throw new IOException("Administrator channel acquisition capacity exhausted."); }
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(_timeout);
        CancellationToken token = budget.Token;
        // Indefinite filesystem calls must not occupy workers needed by HTTP or deadline continuations.
        Task<bool> work = Task.Factory.StartNew(() =>
        {
            try
            {
                token.ThrowIfCancellationRequested();
                using Stream stream = _open(path);
                token.ThrowIfCancellationRequested();
                return Matches(stream, received, token);
            }
            finally { _workers.Release(); }
        }, CancellationToken.None, TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
        try { return await work.WaitAsync(token).ConfigureAwait(false); }
        catch
        {
            // Late faults are observed without logging paths, headers, or credential bytes.
            _ = work.ContinueWith(task => { _ = task.Exception; _lateFaultObserved?.Invoke(); }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            throw;
        }
    }

    private static bool Matches(Stream stream, string received, CancellationToken token)
    {
        byte[] bytes = new byte[MaximumBytes + 1];
        byte[] expected = new byte[MaximumBytes];
        byte[] actual = Encoding.UTF8.GetBytes(received);
        char[] characters = new char[MaximumBytes];
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
            if (used is 0 or > MaximumBytes) { throw new IOException("Bounded administrator channel file required."); }
            ReadOnlySpan<byte> encoded = bytes.AsSpan(0, used);
            if (encoded.StartsWith(Encoding.UTF8.Preamble)) { encoded = encoded[Encoding.UTF8.Preamble.Length..]; }
            int decoded = new UTF8Encoding(false, true).GetChars(encoded, characters);
            int length = Encoding.UTF8.GetBytes(characters.AsSpan(0, decoded).Trim(), expected);
            if (length == 0) { throw new IOException("Nonempty administrator channel credential required."); }
            return length == actual.Length && CryptographicOperations.FixedTimeEquals(expected.AsSpan(0, length), actual);
        }
        catch (DecoderFallbackException) { throw new IOException("Administrator channel encoding unavailable."); }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            CryptographicOperations.ZeroMemory(expected);
            CryptographicOperations.ZeroMemory(actual);
            Array.Clear(characters);
        }
    }

    private static Stream Open(string path)
    {
        if (!OperatingSystem.IsLinux()) { throw new IOException("Administrator channel mount unavailable on this platform."); }
        // Follow the approved whole-directory Secret projection, including /var/run and ..data links.
        int descriptor = LinuxOpen(path, 0x800 | 0x80000); // O_RDONLY | O_NONBLOCK | O_CLOEXEC
        if (descriptor < 0) { throw new IOException("Administrator channel mount unavailable."); }
        var handle = new SafeFileHandle((IntPtr)descriptor, ownsHandle: true);
        try
        {
            if (Statx(descriptor, "", 0x1000, 0x201, out Status status) != 0 || (status.Mode & 0xf000) != 0x8000
                || status.Size is 0 or > MaximumBytes)
            { throw new IOException("Bounded regular administrator channel file required."); }
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
        [FieldOffset(28)] internal ushort Mode;
        [FieldOffset(40)] internal ulong Size;
    }
}
