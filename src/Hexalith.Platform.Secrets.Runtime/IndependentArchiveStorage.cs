namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Actual archive mount/path capacity and bounded flushed native write probe; it never creates archive enrollment or replaces an original.</summary>
public sealed class IndependentArchiveStorage:IIndependentArchiveStorage
{
    /// <inheritdoc />
    public long AvailableBytes(string archiveDirectory)=>new DriveInfo(archiveDirectory).AvailableFreeSpace;
    /// <inheritdoc />
    public async Task ProbeWritableAsync(string archiveDirectory,CancellationToken token)
    {
        for(string? path=archiveDirectory;path is not null;path=Path.GetDirectoryName(path))
        {
            var info=new DirectoryInfo(path);if(!info.Exists || info.LinkTarget is not null) throw new IOException("Existing non-symbolic archive directory required.");
            if(Path.GetDirectoryName(path)==path) break;
        }
        if(!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("Installed archive requires Linux durability.");
        var mode=File.GetUnixFileMode(archiveDirectory);
        if((mode&(UnixFileMode.UserWrite|UnixFileMode.GroupWrite|UnixFileMode.OtherWrite))==0) throw new IOException("Archive is read-only.");
        string probe=Path.Combine(archiveDirectory,".write-probe-"+Guid.NewGuid().ToString("N"));
        try
        {
            await using(var output=new FileStream(probe,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.Asynchronous|FileOptions.WriteThrough))
            { await output.WriteAsync(new byte[]{0},token).ConfigureAwait(false);await output.FlushAsync(token).ConfigureAwait(false);output.Flush(true); }
            ImmutableFileArchive.FlushDirectory(archiveDirectory);File.Delete(probe);ImmutableFileArchive.FlushDirectory(archiveDirectory);
        }
        finally { if(File.Exists(probe)) File.Delete(probe); }
    }
}
