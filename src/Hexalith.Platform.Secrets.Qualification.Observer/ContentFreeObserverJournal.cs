using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Secrets.Qualification.Observer;

/// <summary>Independent content-free observer journal. Append and read share one lock.</summary>
public sealed class ContentFreeObserverJournal(string? directory = null)
{
    private readonly Lock _gate = new();

    /// <summary>Appends one content-free identifier.</summary>
    public void Append(string contentFreeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentFreeId);
        if (directory is null) { throw new SecretsDependencyNotAvailableException("qualification.observer.independentDurableOriginalStore", "Provision the independent observer journal volume and owner-read identity."); }
        if (contentFreeId.Length > 2048 || contentFreeId.Any(char.IsControl)) { throw new ArgumentException("Malformed content-free identifier.", nameof(contentFreeId)); }
        lock (_gate)
        {
            Directory.CreateDirectory(directory);
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(contentFreeId);
            string path = Path.Combine(directory, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)) + ".id");
            if (File.Exists(path))
            {
                if (File.ReadAllText(path) != contentFreeId) { throw new IOException("Observer journal identity conflict."); }
                return;
            }
            string pending = Path.Combine(directory, ".pending-" + Guid.NewGuid().ToString("N"));
            try
            {
                using (var stream = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                { stream.Write(bytes); stream.Flush(flushToDisk: true); }
                try { File.Move(pending, path, overwrite: false); }
                catch (IOException) when (File.Exists(path))
                { if (File.ReadAllText(path) != contentFreeId) { throw; } }
            }
            finally { File.Delete(pending); }
        }
    }

    /// <summary>Returns a snapshot of the identifiers recorded so far.</summary>
    public IReadOnlyList<string> Read()
    {
        lock (_gate)
        {
            return directory is null || !Directory.Exists(directory) ? []
                : Directory.GetFiles(directory, "*.id").Order(StringComparer.Ordinal).Select(ReadEntry).ToArray();
        }
    }

    private static string ReadEntry(string path)
    {
        if (new FileInfo(path).Length > 8192) { throw new IOException("Observer journal entry exceeds bound."); }
        byte[] bytes = File.ReadAllBytes(path);
        if (Path.GetFileNameWithoutExtension(path) != Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)))
        { throw new IOException("Observer journal identity changed."); }
        return new System.Text.UTF8Encoding(false, true).GetString(bytes);
    }
}

/// <summary>Observe denies before any journal append while required references are missing.</summary>
public static class ObserverEndpoint
{
    /// <summary>Returns 503 without appending when qualification is unavailable.</summary>
    public static int Observe(SecretsRuntimeConfiguration configuration, ContentFreeObserverJournal journal)
    {
        ArgumentNullException.ThrowIfNull(journal);
        QualificationDenial denial = QualificationDenial.Evaluate(configuration);
        return denial.StatusCode == 200 ? StatusCodes.Status503ServiceUnavailable : denial.StatusCode;
    }
}
