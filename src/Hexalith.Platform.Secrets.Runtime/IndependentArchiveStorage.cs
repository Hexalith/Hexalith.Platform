namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Actual archive mount/path capacity and bounded flushed native write probe; it never creates archive enrollment or replaces an original.</summary>
public sealed class IndependentArchiveStorage:IIndependentArchiveStorage
{
    internal Func<string,CancellationToken,Task>? ProbeOverride { get; init; }
    /// <inheritdoc />
    public long AvailableBytes(string archiveDirectory)=>BoundedDurableFileOperation.RunAsync(_=>Task.FromResult(new DriveInfo(archiveDirectory).AvailableFreeSpace)).GetAwaiter().GetResult();
    /// <inheritdoc />
    public async Task ProbeWritableAsync(string archiveDirectory,CancellationToken token)
    {
        if(ProbeOverride is not null) { await ProbeOverride(archiveDirectory,token).ConfigureAwait(false);return; }
        _=await ArchiveVolumeFence.RunForPublicationAsync(archiveDirectory,async ct=>
        {
            using var directory=HeldArchiveDirectory.Acquire(archiveDirectory);ArchiveVolumeFence.RequireCurrentDestination(archiveDirectory);directory.RequireWritable();directory.VerifyLocator();
                using(var output=new FileStream(directory.OpenUnpublishedFile(),FileAccess.ReadWrite))
                { await output.WriteAsync(new byte[]{0},ct).ConfigureAwait(false);await output.FlushAsync(ct).ConfigureAwait(false);output.Flush(true); }
                directory.Flush();directory.VerifyLocator();ct.ThrowIfCancellationRequested();return true;
        },token).ConfigureAwait(false);
    }
}
