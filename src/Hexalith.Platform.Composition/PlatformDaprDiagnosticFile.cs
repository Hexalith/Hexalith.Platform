namespace Hexalith.Platform.Composition;

using System.Text.Json;

/// <summary>Persists a private, byte-bounded tail with explicit truncation accounting.</summary>
internal sealed class PlatformDaprDiagnosticFile
{
    private readonly string _path;
    private readonly int _limit;
    private readonly Queue<byte[]> _records = new();
    private readonly object _gate = new();
    private int _bytes;
    private long _dropped;

    /// <summary>Creates a fresh capture file; existing evidence is never reused.</summary>
    internal PlatformDaprDiagnosticFile(string path, int limit)
    {
        _path = path;
        _limit = limit;
        if (File.Exists(path) || Directory.Exists(path) || new FileInfo(path).LinkTarget is not null)
            throw new IOException("Native sidecar diagnostic evidence already exists.");
        Flush();
    }

    /// <summary>Appends one allowlisted record, dropping oldest records at the byte limit.</summary>
    internal void Add(object record)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(record);
        lock (_gate)
        {
            // Reserve enough space for the fixed truncation header and line breaks.
            if (bytes.Length + 1 > _limit - 128) { _dropped++; return; }
            while (_bytes + bytes.Length + 1 > _limit - 128)
            {
                _bytes -= _records.Dequeue().Length + 1;
                _dropped++;
            }
            _records.Enqueue(bytes);
            _bytes += bytes.Length + 1;
        }
    }

    /// <summary>Atomically replaces the private file, retaining diagnostics after host disposal.</summary>
    internal void Flush()
    {
        lock (_gate)
        {
            string pending = _path + ".pending";
            try
            {
                FileStreamOptions options = new() { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
                if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                using (FileStream stream = new(pending, options))
                {
                    stream.Write(JsonSerializer.SerializeToUtf8Bytes(new { Kind = "capture-tail", DroppedRecords = _dropped, Truncated = _dropped > 0 }));
                    stream.WriteByte((byte)'\n');
                    foreach (byte[] record in _records)
                    {
                        stream.Write(record);
                        stream.WriteByte((byte)'\n');
                    }
                }
                File.Move(pending, _path, overwrite: true);
            }
            finally
            {
                if (File.Exists(pending)) File.Delete(pending);
            }
        }
    }
}
