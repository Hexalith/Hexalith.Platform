using System.Runtime.InteropServices;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Publishes a complete flushed immutable original atomically, then flushes its directory before authority may advance.</summary>
public static class ImmutableFileArchive
{
    /// <summary>Creates an exact original or accepts an already published byte-identical original. Conflicting originals are never overwritten.</summary>
    public static async Task PublishAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken token = default)
    {
        if (!Path.IsPathFullyQualified(path) || bytes.Length is 0 or > CustodyBindingContract.MaximumOriginalBytes)
            throw new ArgumentException("Bounded absolute immutable original required.");
        string directory = Path.GetDirectoryName(path)!;
        EnsureDirectory(directory);
        string temporary = Path.Combine(directory, ".pending-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await output.WriteAsync(bytes, token).ConfigureAwait(false);
                await output.FlushAsync(token).ConfigureAwait(false);
                output.Flush(flushToDisk: true);
            }
            token.ThrowIfCancellationRequested();
            try { File.Move(temporary, path, overwrite: false); }
            catch (IOException) when (File.Exists(path))
            {
                var info = new FileInfo(path);
                if (info.LinkTarget is not null || info.Length != bytes.Length
                    || !(await File.ReadAllBytesAsync(path, token).ConfigureAwait(false)).AsSpan().SequenceEqual(bytes.Span))
                    throw new IOException("Conflicting immutable original.");
            }
            FlushDirectory(directory);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static void EnsureDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            if (new DirectoryInfo(directory).LinkTarget is not null) throw new IOException("Archive symbolic link denied.");
            return;
        }
        string parent = Path.GetDirectoryName(directory) ?? throw new IOException("Archive root unavailable.");
        EnsureDirectory(parent);
        Directory.CreateDirectory(directory);
        FlushDirectory(parent);
        FlushDirectory(directory);
    }

    /// <summary>Requires Linux directory durability rather than treating a file flush as durable publication.</summary>
    public static void FlushDirectory(string directory)
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("Installed archive requires directory fsync support.");
        int descriptor = Open(directory, 0x10000 | 0x80000); // O_DIRECTORY | O_CLOEXEC; read-only.
        if (descriptor < 0) throw new IOException("Archive directory open failed.");
        try { if (Fsync(descriptor) != 0) throw new IOException("Archive directory flush failed."); }
        finally { Close(descriptor); }
    }
    [DllImport("libc", EntryPoint = "open", SetLastError = true)] private static extern int Open(string path, int flags);
    [DllImport("libc", EntryPoint = "fsync", SetLastError = true)] private static extern int Fsync(int descriptor);
    [DllImport("libc", EntryPoint = "close")] private static extern int Close(int descriptor);
}
