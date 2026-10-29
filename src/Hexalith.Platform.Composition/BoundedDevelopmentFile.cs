namespace Hexalith.Platform.Composition;

/// <summary>Portable, bounded acquisition of public development metadata. Production/private/archive inputs use the held Linux reader.</summary>
internal static class BoundedDevelopmentFile
{
    private static readonly SemaphoreSlim Acquisitions=new(4,4);
    private static readonly AsyncLocal<Action<string>?> Inspections=new();
    internal static Action<string>? Inspection { get=>Inspections.Value;set=>Inspections.Value=value; }
    internal static Task<byte[]> ReadAsync(string path,int maximum,CancellationToken token,Action? beforeAcquire=null,Action? afterInspection=null)
    {
        if(!Path.IsPathFullyQualified(path) || Path.GetFullPath(path)!=path || maximum is <=0 or >1024*1024)
            throw new IOException("Bounded absolute development metadata required.");
        return RunAsync(async ct=>
        {
            beforeAcquire?.Invoke();ct.ThrowIfCancellationRequested();
            var before=Inspect(path,maximum);Inspections.Value?.Invoke(path);
            using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,4096,FileOptions.Asynchronous);
            if(!stream.CanSeek || stream.Length!=before.Length) throw new IOException("Regular development metadata required.");
            afterInspection?.Invoke();byte[] bytes=new byte[(int)before.Length];bool transferred=false;
            try
            {
                int used=0;
                while(used<bytes.Length)
                {
                    ct.ThrowIfCancellationRequested();int count=await stream.ReadAsync(bytes.AsMemory(used),ct).ConfigureAwait(false);
                    if(count==0) throw new IOException("Development metadata changed during acquisition.");used+=count;
                }
                ct.ThrowIfCancellationRequested();
                if(stream.ReadByte()!=-1 || stream.Length!=before.Length || before!=Inspect(path,maximum))
                    throw new IOException("Development metadata changed during acquisition.");
                ct.ThrowIfCancellationRequested();transferred=true;return bytes;
            }
            finally { if(!transferred) System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes); }
        },token);
    }
    private static async Task<byte[]> RunAsync(Func<CancellationToken,Task<byte[]>> acquire,CancellationToken token)
    {
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromSeconds(30));var acquisitionToken=deadline.Token;
        await Acquisitions.WaitAsync(acquisitionToken).ConfigureAwait(false);
        var work=Task.Run(async()=> { try { acquisitionToken.ThrowIfCancellationRequested();return await acquire(acquisitionToken).ConfigureAwait(false); }finally { Acquisitions.Release(); } },CancellationToken.None);
        try { return await work.WaitAsync(acquisitionToken).ConfigureAwait(false); }
        catch
        {
            _=work.ContinueWith(task=> { if(task.Status==TaskStatus.RanToCompletion) System.Security.Cryptography.CryptographicOperations.ZeroMemory(task.Result);else _=task.Exception; },CancellationToken.None,TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);
            throw;
        }
    }
    private static (string Path,long Length,DateTime Written,DateTime Created,FileAttributes Attributes) Inspect(string path,int maximum)
    {
        if((File.GetAttributes(path)&(FileAttributes.Directory|FileAttributes.Device))!=0) throw new IOException("Regular development metadata required.");
        var info=new FileInfo(path);info.Refresh();
        if(!info.Exists) throw new FileNotFoundException("Development metadata unavailable.",path);
        // Public development workspaces may use macOS /var or /tmp links and Windows junctions.
        // Resolve a final symbolic file to inspect its target; production never calls this reader.
        if((info.Attributes&FileAttributes.ReparsePoint)!=0)
            info=info.ResolveLinkTarget(true) as FileInfo??throw new IOException("Regular development metadata target required.");
        if((info.Attributes&(FileAttributes.Directory|FileAttributes.Device))!=0 || info.Length is <1 || info.Length>maximum)
            throw new IOException("Bounded regular development metadata required.");
        return (info.FullName,info.Length,info.LastWriteTimeUtc,info.CreationTimeUtc,info.Attributes);
    }
}
